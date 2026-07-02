import { Injectable, OnDestroy } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { BehaviorSubject, Subject } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';
import { PositionDto, SosAlertPayload } from '../models/gps-tracking.model';

export type GpsConnectionState = 'connected' | 'reconnecting' | 'disconnected';

@Injectable({ providedIn: 'root' })
export class GpsRealtimeService implements OnDestroy {
  private hub?: signalR.HubConnection;
  private readonly updates$ = new Subject<PositionDto>();
  private readonly sos$ = new Subject<SosAlertPayload>();
  private readonly connectionState = new BehaviorSubject<GpsConnectionState>('disconnected');
  private subscribedVehicleId: number | null = null;

  readonly locationUpdates$ = this.updates$.asObservable();
  readonly sosAlerts$ = this.sos$.asObservable();
  readonly connectionState$ = this.connectionState.asObservable();

  constructor(private auth: AuthService) {}

  async connect(): Promise<void> {
    if (this.hub?.state === signalR.HubConnectionState.Connected) {
      return;
    }

    const hubUrl = environment.apiUrl.replace('/api', '/hubs/tracking');

    this.hub = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, {
        // Always read latest token so reconnect uses refreshed credentials.
        accessTokenFactory: () => this.auth.getToken() ?? ''
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000])
      .build();

    this.hub.onreconnecting(() => this.connectionState.next('reconnecting'));
    this.hub.onreconnected(() => this.connectionState.next('connected'));
    this.hub.onclose(() => this.connectionState.next('disconnected'));

    this.hub.on('ReceiveLocationUpdate', (payload: PositionDto & { bookingId?: number }) => {
      this.updates$.next({
        id: payload.id ?? payload.vehicleId,
        vehicleId: payload.vehicleId,
        bookingId: payload.bookingId,
        latitude: payload.latitude,
        longitude: payload.longitude,
        speed: Number(payload.speed) || 0,
        ignition: payload.ignition,
        timestamp: payload.timestamp ?? new Date().toISOString(),
        heading: payload.heading,
        fuelLevel: payload.fuelLevel,
        batteryLevel: payload.batteryLevel,
        gsmSignal: payload.gsmSignal,
        totalDistanceKm: payload.totalDistanceKm,
        address: payload.address,
        alarmType: payload.alarmType
      });
    });

    this.hub.on('ReceiveSosAlert', (payload: SosAlertPayload) => {
      this.sos$.next(payload);
    });

    await this.hub.start();
    this.connectionState.next('connected');
    await this.hub.invoke('JoinDispatcherGroup');
  }

  async subscribeVehicle(vehicleId: number | null): Promise<void> {
    if (!this.hub || this.hub.state !== signalR.HubConnectionState.Connected) {
      await this.connect();
    }
    if (!this.hub) return;

    if (this.subscribedVehicleId !== null && this.subscribedVehicleId !== vehicleId) {
      await this.hub.invoke('LeaveVehicleGroup', this.subscribedVehicleId);
    }

    this.subscribedVehicleId = vehicleId;

    if (vehicleId !== null) {
      await this.hub.invoke('JoinVehicleGroup', vehicleId);
    }
  }

  async disconnect(): Promise<void> {
    if (!this.hub) return;
    try {
      if (this.subscribedVehicleId !== null) {
        await this.hub.invoke('LeaveVehicleGroup', this.subscribedVehicleId);
      }
      await this.hub.invoke('LeaveDispatcherGroup');
      await this.hub.stop();
    } catch {
      // ignore teardown errors
    }
    this.hub = undefined;
    this.subscribedVehicleId = null;
    this.connectionState.next('disconnected');
  }

  ngOnDestroy(): void {
    void this.disconnect();
    this.updates$.complete();
    this.sos$.complete();
    this.connectionState.complete();
  }
}
