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

    this.hub.on('ReceiveLocationUpdate', (payload: PositionDto) => {
      this.updates$.next({
        id: 0,
        vehicleId: payload.vehicleId,
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

  async disconnect(): Promise<void> {
    if (!this.hub) return;
    try {
      await this.hub.invoke('LeaveDispatcherGroup');
      await this.hub.stop();
    } catch {
      // ignore teardown errors
    }
    this.hub = undefined;
  }

  ngOnDestroy(): void {
    void this.disconnect();
    this.updates$.complete();
  }
}
