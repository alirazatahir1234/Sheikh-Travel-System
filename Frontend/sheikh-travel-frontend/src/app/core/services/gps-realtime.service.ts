import { Injectable, OnDestroy } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';
import { PositionDto } from '../models/gps-tracking.model';

@Injectable({ providedIn: 'root' })
export class GpsRealtimeService implements OnDestroy {
  private hub?: signalR.HubConnection;
  private readonly updates$ = new Subject<PositionDto>();
  private subscribedVehicleId: number | null = null;

  readonly locationUpdates$ = this.updates$.asObservable();

  constructor(private auth: AuthService) {}

  async connect(): Promise<void> {
    if (this.hub?.state === signalR.HubConnectionState.Connected) {
      return;
    }

    const token = this.auth.getToken();
    const hubUrl = environment.apiUrl.replace('/api', '/hubs/tracking');

    this.hub = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: () => token ?? ''
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000])
      .build();

    this.hub.on('ReceiveLocationUpdate', (payload: PositionDto & { bookingId?: number }) => {
      this.updates$.next({
        id: payload.id ?? payload.vehicleId,
        vehicleId: payload.vehicleId,
        bookingId: payload.bookingId,
        latitude: payload.latitude,
        longitude: payload.longitude,
        speed: Number(payload.speed) || 0,
        ignition: payload.ignition,
        timestamp: payload.timestamp ?? new Date().toISOString()
      });
    });

    await this.hub.start();
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
  }

  ngOnDestroy(): void {
    void this.disconnect();
    this.updates$.complete();
  }
}
