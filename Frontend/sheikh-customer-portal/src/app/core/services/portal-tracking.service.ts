import { Injectable, OnDestroy } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CustomerSessionService } from './customer-session.service';

export interface PortalLocationUpdate {
  vehicleId: number;
  latitude: number;
  longitude: number;
  bookingId?: number;
}

@Injectable({ providedIn: 'root' })
export class PortalTrackingService implements OnDestroy {
  private hub?: signalR.HubConnection;
  private readonly updates$ = new Subject<PortalLocationUpdate>();

  readonly locationUpdates$ = this.updates$.asObservable();

  constructor(private readonly session: CustomerSessionService) {}

  async connectAndSubscribeBooking(bookingId: number, vehicleId: number | null): Promise<void> {
    const token = this.session.accessToken();
    if (!token) return;

    if (!this.hub || this.hub.state !== signalR.HubConnectionState.Connected) {
      this.hub = new signalR.HubConnectionBuilder()
        .withUrl(`${environment.hubRoot}/hubs/tracking`, {
          accessTokenFactory: () => token
        })
        .withAutomaticReconnect([0, 2000, 5000])
        .build();

      this.hub.on('ReceiveLocationUpdate', (payload: PortalLocationUpdate) => {
        this.updates$.next(payload);
      });

      await this.hub.start();
    }

    await this.hub.invoke('JoinBookingGroup', bookingId);
    if (vehicleId) {
      await this.hub.invoke('JoinVehicleGroup', vehicleId);
    }
  }

  async disconnect(): Promise<void> {
    if (!this.hub) return;
    try {
      await this.hub.stop();
    } catch {
      // ignore
    }
    this.hub = undefined;
  }

  ngOnDestroy(): void {
    void this.disconnect();
  }
}
