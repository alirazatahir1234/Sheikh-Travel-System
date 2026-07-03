import { Injectable, inject, signal, computed } from '@angular/core';
import { Subscription, finalize, map, of, switchMap } from 'rxjs';
import { GpsTrackingService } from '../../../core/services/gps-tracking.service';
import { GpsRealtimeService, GpsConnectionState } from '../../../core/services/gps-realtime.service';
import {
  GpsDashboardSummary,
  TripEvent,
  TripReplayPosition,
  TripStop,
  VehicleLocation
} from '../../../core/models/gps-tracking.model';
import { mergeVehicleLocations } from '../live-map/live-map-state.util';
import { detectTelemetryEvents, LiveMapEvent } from '../live-map/state/live-map-events.util';

@Injectable()
export class GpsLiveFacade {
  private readonly gps = inject(GpsTrackingService);
  private readonly realtime = inject(GpsRealtimeService);

  readonly locations = signal<VehicleLocation[]>([]);
  readonly selectedVehicleId = signal<number | null>(null);
  readonly initialLoading = signal(true);
  readonly refreshing = signal(false);
  readonly syncError = signal<string | null>(null);
  readonly connectionState = signal<GpsConnectionState>('disconnected');
  readonly dashboardSummary = signal<GpsDashboardSummary | null>(null);
  readonly events = signal<LiveMapEvent[]>([]);
  readonly geofenceBreachCount = signal(0);
  readonly lastSyncAt = signal<Date | null>(null);

  readonly selectedLocation = computed(() => {
    const id = this.selectedVehicleId();
    return this.locations().find(l => l.vehicleId === id) ?? null;
  });

  readonly fleetCounts = computed(() => {
    const locs = this.locations();
    const c = (s: string) => locs.filter(l => l.status === s).length;
    const online = locs.filter(l => l.hasGps && ['moving', 'idle', 'parked'].includes(l.status)).length;
    return {
      total: locs.length,
      online,
      offline: c('offline'),
      moving: c('moving'),
      idle: c('idle'),
      parked: c('parked'),
      neverSeen: c('never_seen'),
      sos: c('sos')
    };
  });

  private subs: Subscription[] = [];
  private hasLoadedOnce = false;

  connect(): void {
    this.subs.push(
      this.realtime.connectionState$.subscribe(s => this.connectionState.set(s)),
      this.realtime.locationUpdates$.subscribe(u => this.applyRealtimeUpdate(u)),
      this.realtime.sosAlerts$.subscribe(a => this.applySos(a.vehicleId))
    );
    void this.realtime.connect().catch(() => {});
  }

  disconnect(): void {
    this.subs.forEach(s => s.unsubscribe());
    this.subs = [];
    void this.realtime.disconnect();
  }

  loadAll(silent = false): void {
    if (!silent) this.initialLoading.set(true);
    if (silent && this.hasLoadedOnce) this.refreshing.set(true);

    this.gps.getAllVehicleLocations().pipe(
      finalize(() => {
        this.refreshing.set(false);
        this.initialLoading.set(false);
        this.hasLoadedOnce = true;
      })
    ).subscribe({
      next: locs => {
        const prev = new Map(this.locations().map(l => [l.vehicleId, l]));
        const merged = mergeVehicleLocations(this.locations(), locs);
        if (this.hasLoadedOnce) {
          merged.forEach(loc => {
            const p = prev.get(loc.vehicleId);
            if (p) this.pushEvents(detectTelemetryEvents(p, loc));
          });
        }
        this.locations.set(merged);
        this.syncError.set(null);
        this.lastSyncAt.set(new Date());
        if (this.selectedVehicleId() == null && merged.length) {
          const pick = merged.find(l => l.hasGps) ?? merged[0];
          this.selectedVehicleId.set(pick.vehicleId);
        }
      },
      error: () => this.syncError.set('Could not sync vehicle locations.')
    });

    this.gps.getDashboardSummary().subscribe({
      next: s => this.dashboardSummary.set(s),
      error: () => {}
    });

    this.gps.getGeofenceBreachCount().subscribe({
      next: c => this.geofenceBreachCount.set(c),
      error: () => {}
    });
  }

  selectVehicle(id: number | null): void {
    this.selectedVehicleId.set(id);
    if (id != null) {
      void this.realtime.subscribeVehicle(id);
    }
  }

  pushEvent(message: string, type: LiveMapEvent['type'], icon: string): void {
    this.events.update(list => {
      const next = [{ time: new Date(), message, type, icon }, ...list];
      return next.slice(0, 30);
    });
  }

  private pushEvents(items: LiveMapEvent[]): void {
    if (!items.length) return;
    this.events.update(list => [...items, ...list].slice(0, 30));
  }

  private applyRealtimeUpdate(update: { vehicleId: number; latitude?: number; longitude?: number; speed?: number; ignition?: boolean | null; timestamp?: string; heading?: number; fuelLevel?: number; batteryLevel?: number; gsmSignal?: number; totalDistanceKm?: number; address?: string; alarmType?: string | null }): void {
    this.locations.update(locs => {
      const idx = locs.findIndex(l => l.vehicleId === update.vehicleId);
      if (idx < 0) return locs;
      const prev = locs[idx];
      const next: VehicleLocation = {
        ...prev,
        latitude: update.latitude ?? prev.latitude,
        longitude: update.longitude ?? prev.longitude,
        speed: update.speed ?? prev.speed,
        ignition: update.ignition ?? prev.ignition,
        lastUpdated: update.timestamp ?? prev.lastUpdated,
        heading: update.heading ?? prev.heading,
        fuelLevel: update.fuelLevel ?? prev.fuelLevel,
        batteryLevel: update.batteryLevel ?? prev.batteryLevel,
        gsmSignal: update.gsmSignal ?? prev.gsmSignal,
        totalDistanceKm: update.totalDistanceKm ?? prev.totalDistanceKm,
        address: update.address ?? prev.address,
        alarmType: update.alarmType ?? prev.alarmType,
        hasGps: true
      };
      this.pushEvents(detectTelemetryEvents(prev, next));
      const copy = [...locs];
      copy[idx] = next;
      return copy;
    });
    this.lastSyncAt.set(new Date());
  }

  private applySos(vehicleId: number): void {
    this.locations.update(locs =>
      locs.map(l => l.vehicleId === vehicleId ? { ...l, status: 'sos' as const, alarmType: 'sos' } : l)
    );
    this.pushEvent(`Vehicle #${vehicleId} — SOS / panic alarm!`, 'alert', 'sos');
  }

  loadTripReplay(vehicleId: number, from: Date, to: Date) {
    return this.gps.getTripReplay(vehicleId, from, to).pipe(
      switchMap(bundle => {
        const playback = bundle.playback?.length ? bundle.playback : bundle.route ?? [];
        if (playback.length) {
          return of({ bundle, playback, fromHistory: false });
        }
        return this.gps.getHistory(vehicleId, from, to).pipe(
          map(rows => ({
            bundle,
            playback: rows.map(r => ({
              timestamp: r.timestamp,
              latitude: r.latitude,
              longitude: r.longitude,
              speedKmh: Number(r.speed) || 0,
              heading: r.heading ?? null,
              ignition: r.ignition ?? null
            } as TripReplayPosition)),
            fromHistory: true
          }))
        );
      })
    );
  }
}
