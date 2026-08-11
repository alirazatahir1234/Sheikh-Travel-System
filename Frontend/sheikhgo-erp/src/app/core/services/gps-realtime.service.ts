import { Injectable, NgZone, OnDestroy } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { BehaviorSubject, Subject } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';
import { PositionDto, SosAlertPayload } from '../models/gps-tracking.model';

export type GpsConnectionState = 'connected' | 'reconnecting' | 'disconnected';

export interface GpsRealtimeConnectOptions {
  /**
   * Join the fleet-wide `dispatchers` SignalR group.
   * Live Map / Devices need this; Vehicle Profile should pass false
   * and use {@link subscribeVehicle} instead.
   */
  asDispatcher?: boolean;
}

@Injectable({ providedIn: 'root' })
export class GpsRealtimeService implements OnDestroy {
  private hub?: signalR.HubConnection;
  private connectPromise?: Promise<void>;
  private readonly updates$ = new Subject<PositionDto>();
  private readonly sos$ = new Subject<SosAlertPayload>();
  private readonly connectionState = new BehaviorSubject<GpsConnectionState>('disconnected');
  private subscribedVehicleId: number | null = null;
  private dispatcherRefCount = 0;
  private inDispatcherGroup = false;

  readonly locationUpdates$ = this.updates$.asObservable();
  readonly sosAlerts$ = this.sos$.asObservable();
  readonly connectionState$ = this.connectionState.asObservable();

  constructor(
    private auth: AuthService,
    private ngZone: NgZone
  ) {}

  async connect(options?: GpsRealtimeConnectOptions): Promise<void> {
    const asDispatcher = options?.asDispatcher !== false;

    await this.ensureHubStarted();

    if (asDispatcher) {
      await this.acquireDispatcher();
    }
  }

  /**
   * Decrement dispatcher membership. Leaves the group when the last consumer releases.
   * Safe to call when the caller never acquired dispatcher membership.
   */
  async releaseDispatcher(): Promise<void> {
    if (this.dispatcherRefCount <= 0) return;
    this.dispatcherRefCount -= 1;
    if (this.dispatcherRefCount === 0) {
      await this.leaveDispatcherGroup();
    }
  }

  async subscribeVehicle(vehicleId: number | null): Promise<void> {
    await this.ensureHubStarted();
    if (!this.hub) return;

    if (this.subscribedVehicleId !== null && this.subscribedVehicleId !== vehicleId) {
      try {
        await this.hub.invoke('LeaveVehicleGroup', this.subscribedVehicleId);
      } catch {
        // ignore leave errors
      }
    }

    this.subscribedVehicleId = vehicleId;

    if (vehicleId !== null && this.hub.state === signalR.HubConnectionState.Connected) {
      try {
        await this.hub.invoke('JoinVehicleGroup', vehicleId);
      } catch {
        // ignore join errors; poll fallback handles display
      }
    }
  }

  async disconnect(): Promise<void> {
    if (!this.hub) return;
    try {
      if (this.subscribedVehicleId !== null) {
        await this.hub.invoke('LeaveVehicleGroup', this.subscribedVehicleId);
      }
      if (this.inDispatcherGroup) {
        await this.hub.invoke('LeaveDispatcherGroup');
      }
      await this.hub.stop();
    } catch {
      // ignore teardown errors
    }
    this.hub = undefined;
    this.connectPromise = undefined;
    this.subscribedVehicleId = null;
    this.dispatcherRefCount = 0;
    this.inDispatcherGroup = false;
    this.ngZone.run(() => this.connectionState.next('disconnected'));
  }

  ngOnDestroy(): void {
    void this.disconnect();
    this.updates$.complete();
    this.sos$.complete();
    this.connectionState.complete();
  }

  private async ensureHubStarted(): Promise<void> {
    const state = this.hub?.state;
    if (state === signalR.HubConnectionState.Connected) {
      return;
    }
    if (this.connectPromise) {
      await this.connectPromise;
      return;
    }
    if (
      state === signalR.HubConnectionState.Connecting ||
      state === signalR.HubConnectionState.Reconnecting
    ) {
      // Wait until the existing hub settles without creating a second connection.
      await this.waitForHubSettled();
      return;
    }

    this.connectPromise = this.startHub();
    try {
      await this.connectPromise;
    } finally {
      this.connectPromise = undefined;
    }
  }

  private async startHub(): Promise<void> {
    const hubUrl = environment.apiUrl.replace('/api', '/hubs/tracking');

    this.hub = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, {
        // Always read latest token so reconnect uses refreshed credentials.
        accessTokenFactory: () => this.auth.getToken() ?? ''
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000])
      .build();

    this.hub.onreconnecting(() => {
      this.ngZone.run(() => this.connectionState.next('reconnecting'));
    });
    this.hub.onreconnected(() => {
      this.ngZone.run(() => this.connectionState.next('connected'));
      void this.rejoinGroupsAfterReconnect();
    });
    this.hub.onclose(() => {
      this.inDispatcherGroup = false;
      this.ngZone.run(() => this.connectionState.next('disconnected'));
    });

    // Emit outside Angular Zone so fleet traffic does not force Application.tick()
    // on every ReceiveLocationUpdate. Consumers re-enter the zone only when applying UI.
    this.hub.on('ReceiveLocationUpdate', (payload: PositionDto & { bookingId?: number }) => {
      this.ngZone.runOutsideAngular(() => {
        if (!payload?.timestamp) return;
        this.updates$.next({
          id: payload.id ?? payload.vehicleId,
          vehicleId: payload.vehicleId,
          bookingId: payload.bookingId,
          latitude: payload.latitude,
          longitude: payload.longitude,
          speed: Number(payload.speed) || 0,
          ignition: payload.ignition,
          timestamp: payload.timestamp,
          heading: payload.heading,
          fuelLevel: payload.fuelLevel,
          batteryLevel: payload.batteryLevel,
          gsmSignal: payload.gsmSignal,
          totalDistanceKm: payload.totalDistanceKm,
          address: payload.address,
          alarmType: payload.alarmType,
          temperature: payload.temperature
        });
      });
    });

    this.hub.on('ReceiveSosAlert', (payload: SosAlertPayload) => {
      this.ngZone.runOutsideAngular(() => {
        this.sos$.next(payload);
      });
    });

    await this.hub.start();
    this.ngZone.run(() => this.connectionState.next('connected'));
  }

  private async waitForHubSettled(): Promise<void> {
    const hub = this.hub;
    if (!hub) return;
    const deadline = Date.now() + 15_000;
    while (Date.now() < deadline) {
      const s = hub.state;
      if (s === signalR.HubConnectionState.Connected) return;
      if (
        s !== signalR.HubConnectionState.Connecting &&
        s !== signalR.HubConnectionState.Reconnecting
      ) {
        return;
      }
      await new Promise(r => setTimeout(r, 50));
    }
  }

  private async acquireDispatcher(): Promise<void> {
    this.dispatcherRefCount += 1;
    if (this.dispatcherRefCount === 1) {
      await this.joinDispatcherGroup();
    }
  }

  private async joinDispatcherGroup(): Promise<void> {
    if (!this.hub || this.hub.state !== signalR.HubConnectionState.Connected) return;
    if (this.inDispatcherGroup) return;
    try {
      await this.hub.invoke('JoinDispatcherGroup');
      this.inDispatcherGroup = true;
    } catch {
      // leave refcount; next reconnect will retry via rejoinGroupsAfterReconnect
      this.inDispatcherGroup = false;
    }
  }

  private async leaveDispatcherGroup(): Promise<void> {
    if (!this.hub || !this.inDispatcherGroup) {
      this.inDispatcherGroup = false;
      return;
    }
    try {
      if (this.hub.state === signalR.HubConnectionState.Connected) {
        await this.hub.invoke('LeaveDispatcherGroup');
      }
    } catch {
      // ignore
    }
    this.inDispatcherGroup = false;
  }

  private async rejoinGroupsAfterReconnect(): Promise<void> {
    if (!this.hub || this.hub.state !== signalR.HubConnectionState.Connected) return;
    this.inDispatcherGroup = false;
    if (this.dispatcherRefCount > 0) {
      await this.joinDispatcherGroup();
    }
    if (this.subscribedVehicleId !== null) {
      try {
        await this.hub.invoke('JoinVehicleGroup', this.subscribedVehicleId);
      } catch {
        // ignore
      }
    }
  }
}
