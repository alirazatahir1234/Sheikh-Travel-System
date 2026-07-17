import { AfterViewInit, Component, ElementRef, OnInit, ViewChild } from '@angular/core';
import { Router } from '@angular/router';
import { ChartData } from 'chart.js';
import * as L from 'leaflet';
import { GpsTrackingService } from '../../../core/services/gps-tracking.service';
import { ExportService, ExportColumn, ExportMeta } from '../../../core/services/export.service';
import { AnalyticsOverview, GpsAlertEvent, IdleAnalytics, StopAnalytics, DriverScore, FleetUtilization, FuelAnalytics, GeofenceAnalytics, AlertEventStats, HeatmapPoint, GpsVehicleHealth, GpsDevice, VehicleRanking, CostAnalytics, ComparativeAnalytics, VehicleFuel } from '../../../core/models/gps-tracking.model';
import { UiTableColumn } from '../../../shared/components/ui';
import { AccentColor } from '../../../shared/ui/ui.types';
import { FleetService } from '../../fleet-management/services/fleet.service';
import { QuickAction, CriticalAlert, ActivityEvent, AssignmentItem } from '../../fleet-management/fleet-dashboard/fleet-dashboard.model';
import {
  TripDatePreset,
  TRIP_DATE_PRESETS,
  applyTripDatePreset,
  toDatetimeLocalInput
} from '../utils/trip-date-preset.util';

interface KpiTile {
  label: string;
  value: string | number;
  icon: string;
  color: AccentColor;
  suffix?: string;
  route?: string;
}

@Component({
  standalone: false,
  selector: 'app-gps-analytics',
  templateUrl: './gps-analytics.component.html',
  styleUrls: ['./gps-analytics.component.scss']
})
export class GpsAnalyticsComponent implements OnInit, AfterViewInit {
  @ViewChild('heatmapMap') heatmapMapEl?: ElementRef<HTMLDivElement>;
  readonly datePresets = TRIP_DATE_PRESETS;
  datePreset: TripDatePreset = 'last7Days';
  from = '';
  to = '';

  loading = false;
  overview: AnalyticsOverview | null = null;

  // Precomputed stable fields, set once per load — never a getter/inline template call. Binding a
  // method call here would recompute (and, once sparklines are added in later stages, redraw
  // stb-stat-tile's sparkline canvas) on every change-detection pass instead of only on data load
  // (see live-map.component.ts's kpiTiles field for the documented incident this avoids).
  fleetStatusTiles: KpiTile[] = [];
  totalsTiles: KpiTile[] = [];
  todayTiles: KpiTile[] = [];

  readonly quickActions: QuickAction[] = [
    { id: 'live-map', label: 'Live Map', icon: 'my_location', route: '/gps-tracking/live', tone: 'blue' },
    { id: 'send-command', label: 'Send Command', icon: 'power_settings_new', route: '/gps-tracking/commands', tone: 'orange' },
    { id: 'add-geofence', label: 'Add Geofence', icon: 'fence', route: '/gps-tracking/geofences', tone: 'green' },
    { id: 'register-device', label: 'Register Device', icon: 'sensors', route: '/gps-tracking/devices/register', tone: 'neutral' }
  ];

  criticalAlerts: CriticalAlert[] = [];
  recentActivities: ActivityEvent[] = [];
  assignments: AssignmentItem[] = [];
  assignmentsLoading = false;

  // Precomputed stable ChartData fields — same discipline as the KPI tiles above; ui-chart
  // re-renders whenever its [data] input's reference changes, so these must only be reassigned
  // after a real data load, never derived inline in the template.
  distanceChartData: ChartData = { labels: [], datasets: [] };
  speedHistogramChartData: ChartData = { labels: [], datasets: [] };
  idleAnalytics: IdleAnalytics | null = null;
  stopAnalytics: StopAnalytics | null = null;

  driverRanking: DriverScore[] = [];
  driverRankingLoading = false;
  readonly driverColumns: UiTableColumn[] = [
    { key: 'driverName', label: 'Driver', sortable: true },
    { key: 'score', label: 'Score', sortable: true, align: 'right' },
    { key: 'rating', label: 'Rating' },
    { key: 'trips', label: 'Trips', align: 'right' },
    { key: 'distance', label: 'Distance', align: 'right' },
    { key: 'overspeed', label: 'Overspeed', align: 'right' }
  ];

  utilization: FleetUtilization | null = null;
  fuelAnalytics: FuelAnalytics | null = null;
  fuelChartData: ChartData = { labels: [], datasets: [] };
  readonly fuelColumns: UiTableColumn[] = [
    { key: 'vehicleName', label: 'Vehicle', sortable: true },
    { key: 'liters', label: 'Liters', align: 'right' },
    { key: 'cost', label: 'Cost', align: 'right' },
    { key: 'economy', label: 'L/100km', align: 'right' }
  ];

  geofenceAnalytics: GeofenceAnalytics | null = null;
  eventStats: AlertEventStats | null = null;
  eventCategoryChartData: ChartData = { labels: [], datasets: [] };
  eventTrendChartData: ChartData = { labels: [], datasets: [] };

  private static readonly PIE_COLORS = ['#0f766e', '#3b82f6', '#8b5cf6', '#f59e0b', '#ef4444', '#10b981', '#64748b', '#ec4899'];

  devices: GpsDevice[] = [];
  selectedHeatmapVehicleIds: number[] = [];
  readonly heatmapMaxVehicles = 15;
  heatmapPoints: HeatmapPoint[] = [];
  heatmapLoading = false;
  private heatmapMap: L.Map | null = null;
  private heatmapLayer: L.LayerGroup | null = null;

  vehicleHealth: GpsVehicleHealth[] = [];
  vehicleHealthLoading = false;
  readonly healthColumns: UiTableColumn[] = [
    { key: 'vehicleName', label: 'Vehicle', sortable: true },
    { key: 'battery', label: 'Battery', align: 'right' },
    { key: 'signal', label: 'GPS Signal', align: 'right' },
    { key: 'maintenance', label: 'Maintenance' },
    { key: 'insurance', label: 'Insurance' },
    { key: 'warranty', label: 'Tracker Warranty' }
  ];

  vehicleRanking: VehicleRanking[] = [];
  vehicleRankingLoading = false;
  readonly vehicleRankingColumns: UiTableColumn[] = [
    { key: 'vehicleName', label: 'Vehicle', sortable: true },
    { key: 'distance', label: 'Distance', align: 'right' },
    { key: 'trips', label: 'Trips', align: 'right' },
    { key: 'avgSpeed', label: 'Avg Speed', align: 'right' },
    { key: 'cost', label: 'Fuel + Maint. Cost', align: 'right' },
    { key: 'costPerKm', label: 'Cost/KM', align: 'right' }
  ];

  costAnalytics: CostAnalytics | null = null;
  trendsChartData: ChartData = { labels: [], datasets: [] };
  trendGranularity: 'daily' | 'weekly' | 'monthly' = 'daily';
  comparative: ComparativeAnalytics | null = null;

  constructor(
    private gps: GpsTrackingService,
    private fleet: FleetService,
    private exportService: ExportService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.applyPreset(this.datePreset);
    this.load();
    this.loadWidgets();
    this.loadDevices();
    this.loadVehicleHealth();
    this.loadTrends();
    this.loadComparative();
  }

  ngAfterViewInit(): void {
    if (!this.heatmapMapEl) return;
    this.heatmapMap = L.map(this.heatmapMapEl.nativeElement, { zoomControl: true }).setView([31.52, 74.35], 6);
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      attribution: '© OpenStreetMap contributors',
      maxZoom: 19
    }).addTo(this.heatmapMap);
    this.heatmapLayer = L.layerGroup().addTo(this.heatmapMap);
  }

  private loadDevices(): void {
    this.gps.getDevices().subscribe({
      next: devices => { this.devices = devices.filter(d => d.vehicleId != null); },
      error: () => { this.devices = []; }
    });
  }

  toggleHeatmapVehicle(vehicleId: number, checked: boolean): void {
    if (checked) {
      if (this.selectedHeatmapVehicleIds.length >= this.heatmapMaxVehicles) return;
      this.selectedHeatmapVehicleIds = [...this.selectedHeatmapVehicleIds, vehicleId];
    } else {
      this.selectedHeatmapVehicleIds = this.selectedHeatmapVehicleIds.filter(id => id !== vehicleId);
    }
  }

  loadHeatmap(): void {
    if (this.selectedHeatmapVehicleIds.length === 0) return;
    this.heatmapLoading = true;
    const filters = {
      from: this.from ? new Date(this.from).toISOString() : undefined,
      to: this.to ? new Date(this.to).toISOString() : undefined
    };
    this.gps.getPositionHeatmap(this.selectedHeatmapVehicleIds, filters).subscribe({
      next: points => {
        this.heatmapPoints = points;
        this.renderHeatmap();
        this.heatmapLoading = false;
      },
      error: () => {
        this.heatmapPoints = [];
        this.heatmapLoading = false;
      }
    });
  }

  private renderHeatmap(): void {
    if (!this.heatmapMap || !this.heatmapLayer) return;
    this.heatmapLayer.clearLayers();
    if (this.heatmapPoints.length === 0) return;

    const maxCount = Math.max(...this.heatmapPoints.map(p => p.count));
    for (const point of this.heatmapPoints) {
      const intensity = point.count / maxCount;
      L.circleMarker([point.latitude, point.longitude], {
        radius: 4 + intensity * 12,
        fillColor: intensity > 0.66 ? '#ef4444' : intensity > 0.33 ? '#f59e0b' : '#0f766e',
        fillOpacity: 0.5,
        stroke: false
      }).addTo(this.heatmapLayer);
    }

    const bounds = L.latLngBounds(this.heatmapPoints.map(p => [p.latitude, p.longitude] as [number, number]));
    this.heatmapMap.fitBounds(bounds, { padding: [24, 24] });
  }

  private loadVehicleHealth(): void {
    this.vehicleHealthLoading = true;
    this.gps.getVehicleHealth().subscribe({
      next: data => { this.vehicleHealth = data; this.vehicleHealthLoading = false; },
      error: () => { this.vehicleHealth = []; this.vehicleHealthLoading = false; }
    });
  }

  healthBadgeClass(status: string): string {
    switch (status) {
      case 'Overdue':
      case 'Expired':
        return 'badge-red';
      case 'DueSoon':
      case 'ExpiringSoon':
        return 'badge-amber';
      case 'Valid':
      case 'None':
        return 'badge-green';
      default:
        return 'badge-gray';
    }
  }

  applyPreset(preset: TripDatePreset): void {
    this.datePreset = preset;
    if (preset === 'custom') return;
    const range = applyTripDatePreset(preset);
    this.from = toDatetimeLocalInput(range.from);
    this.to = toDatetimeLocalInput(range.to);
  }

  onPresetChange(preset: TripDatePreset): void {
    this.applyPreset(preset);
    if (preset !== 'custom') this.load();
  }

  load(): void {
    this.loading = true;
    const filters = {
      from: this.from ? new Date(this.from).toISOString() : undefined,
      to: this.to ? new Date(this.to).toISOString() : undefined
    };

    this.gps.getAnalyticsOverview(filters).subscribe({
      next: data => {
        this.overview = data;
        this.recomputeKpiTiles();
        this.loading = false;
      },
      error: () => {
        this.loading = false;
      }
    });

    this.gps.getDistanceAnalytics(filters).subscribe({
      next: data => {
        this.distanceChartData = {
          labels: data.daily.map(d => new Date(d.date).toLocaleDateString(undefined, { day: '2-digit', month: 'short' })),
          datasets: [{
            label: 'Distance (km)',
            data: data.daily.map(d => d.distanceKm),
            borderColor: '#0f766e',
            backgroundColor: '#0f766e33',
            tension: 0.4,
            pointRadius: 0
          }]
        };
      },
      error: () => { this.distanceChartData = { labels: [], datasets: [] }; }
    });

    this.gps.getSpeedAnalytics(filters).subscribe({
      next: data => {
        this.speedHistogramChartData = {
          labels: data.histogram.map(b => b.label + ' km/h'),
          datasets: [{
            label: 'Trips',
            data: data.histogram.map(b => b.count),
            backgroundColor: '#3b82f6'
          }]
        };
      },
      error: () => { this.speedHistogramChartData = { labels: [], datasets: [] }; }
    });

    this.gps.getIdleAnalytics(filters).subscribe({
      next: data => { this.idleAnalytics = data; },
      error: () => { this.idleAnalytics = null; }
    });

    this.gps.getStopAnalytics(filters).subscribe({
      next: data => { this.stopAnalytics = data; },
      error: () => { this.stopAnalytics = null; }
    });

    this.driverRankingLoading = true;
    this.gps.getDriverScoreRanking(filters).subscribe({
      next: data => { this.driverRanking = data; this.driverRankingLoading = false; },
      error: () => { this.driverRanking = []; this.driverRankingLoading = false; }
    });

    this.gps.getFleetUtilization(filters).subscribe({
      next: data => { this.utilization = data; },
      error: () => { this.utilization = null; }
    });

    this.gps.getFuelAnalytics(filters).subscribe({
      next: data => {
        this.fuelAnalytics = data;
        this.fuelChartData = {
          labels: data.daily.map(d => new Date(d.date).toLocaleDateString(undefined, { day: '2-digit', month: 'short' })),
          datasets: [{
            label: 'Fuel (L)',
            data: data.daily.map(d => d.liters),
            backgroundColor: '#8b5cf6'
          }]
        };
      },
      error: () => { this.fuelAnalytics = null; this.fuelChartData = { labels: [], datasets: [] }; }
    });

    this.gps.getGeofenceAnalytics(filters).subscribe({
      next: data => { this.geofenceAnalytics = data; },
      error: () => { this.geofenceAnalytics = null; }
    });

    this.gps.getAlertEventStats(filters).subscribe({
      next: data => {
        this.eventStats = data;
        this.eventCategoryChartData = {
          labels: data.byType.map(t => t.key),
          datasets: [{
            data: data.byType.map(t => t.count),
            backgroundColor: data.byType.map((_, i) => GpsAnalyticsComponent.PIE_COLORS[i % GpsAnalyticsComponent.PIE_COLORS.length]),
            borderWidth: 0
          }]
        };
        this.eventTrendChartData = {
          labels: data.daily.map(d => new Date(d.date).toLocaleDateString(undefined, { day: '2-digit', month: 'short' })),
          datasets: [{
            label: 'Events',
            data: data.daily.map(d => d.count),
            borderColor: '#ef4444',
            backgroundColor: '#ef444433',
            tension: 0.4,
            pointRadius: 0
          }]
        };
      },
      error: () => {
        this.eventStats = null;
        this.eventCategoryChartData = { labels: [], datasets: [] };
        this.eventTrendChartData = { labels: [], datasets: [] };
      }
    });

    this.vehicleRankingLoading = true;
    this.gps.getVehicleRanking(filters).subscribe({
      next: data => { this.vehicleRanking = data; this.vehicleRankingLoading = false; },
      error: () => { this.vehicleRanking = []; this.vehicleRankingLoading = false; }
    });

    this.gps.getCostAnalytics(filters).subscribe({
      next: data => { this.costAnalytics = data; },
      error: () => { this.costAnalytics = null; }
    });
  }

  loadTrends(): void {
    this.gps.getAnalyticsTrends({ from: new Date(Date.now() - 180 * 86_400_000).toISOString() }, this.trendGranularity).subscribe({
      next: data => {
        this.trendsChartData = {
          labels: data.points.map(p => new Date(p.period).toLocaleDateString(undefined, { day: '2-digit', month: 'short' })),
          datasets: [{
            label: 'Distance (km)',
            data: data.points.map(p => p.distanceKm),
            borderColor: '#0f766e',
            backgroundColor: '#0f766e33',
            tension: 0.4,
            pointRadius: 0
          }]
        };
      },
      error: () => { this.trendsChartData = { labels: [], datasets: [] }; }
    });
  }

  onTrendGranularityChange(granularity: 'daily' | 'weekly' | 'monthly'): void {
    this.trendGranularity = granularity;
    this.loadTrends();
  }

  loadComparative(): void {
    const now = new Date();
    const thisMonthStart = new Date(now.getFullYear(), now.getMonth(), 1);
    const lastMonthStart = new Date(now.getFullYear(), now.getMonth() - 1, 1);
    const lastMonthEnd = new Date(now.getFullYear(), now.getMonth(), 0, 23, 59, 59);

    this.gps.getComparativeAnalytics(
      thisMonthStart.toISOString(), now.toISOString(),
      lastMonthStart.toISOString(), lastMonthEnd.toISOString()
    ).subscribe({
      next: data => { this.comparative = data; },
      error: () => { this.comparative = null; }
    });
  }

  utilizationBadgeClass(label: string): string {
    switch (label) {
      case 'Excellent': return 'badge-green';
      case 'Good': return 'badge-blue';
      case 'Fair': return 'badge-amber';
      default: return 'badge-red';
    }
  }

  ratingBadgeClass(rating: string): string {
    switch (rating) {
      case 'Excellent': return 'badge-green';
      case 'Good': return 'badge-blue';
      case 'Fair': return 'badge-amber';
      default: return 'badge-red';
    }
  }

  private recomputeKpiTiles(): void {
    const o = this.overview;
    if (!o) {
      this.fleetStatusTiles = [];
      this.totalsTiles = [];
      this.todayTiles = [];
      return;
    }

    this.fleetStatusTiles = [
      { label: 'Total Vehicles', value: o.totalVehicles, icon: 'directions_car', color: 'blue', route: '/gps-tracking/live' },
      { label: 'Online', value: o.online, icon: 'wifi_tethering', color: 'sky', route: '/gps-tracking/live' },
      { label: 'Offline', value: o.offline, icon: 'wifi_off', color: 'rose', route: '/gps-tracking/live' },
      { label: 'Moving', value: o.moving, icon: 'speed', color: 'green', route: '/gps-tracking/live' },
      { label: 'Idle', value: o.idle, icon: 'pause_circle', color: 'amber', route: '/gps-tracking/live' },
      { label: 'Stopped', value: o.stopped, icon: 'local_parking', color: 'purple', route: '/gps-tracking/live' }
    ];

    this.totalsTiles = [
      { label: 'Total Distance', value: o.distanceKm.toFixed(1), icon: 'route', color: 'teal', suffix: ' km', route: '/gps-tracking/trips' },
      { label: 'Driving Time', value: this.formatMinutes(o.drivingMinutes), icon: 'schedule', color: 'sky', route: '/gps-tracking/trips' },
      { label: 'Idle Time', value: this.formatMinutes(o.idleMinutes), icon: 'hourglass_top', color: 'amber' },
      { label: 'Average Speed', value: o.avgSpeedKmh.toFixed(0), icon: 'speed', color: 'green', suffix: ' km/h' },
      { label: 'Max Speed', value: o.maxSpeedKmh.toFixed(0), icon: 'trending_up', color: 'rose', suffix: ' km/h' },
      { label: 'Fuel Consumed', value: o.fuelLiters != null ? o.fuelLiters.toFixed(1) : '—', icon: 'local_gas_station', color: 'purple', suffix: o.fuelLiters != null ? ' L' : '' }
    ];

    this.todayTiles = [
      { label: 'Trips Today', value: o.tripsToday, icon: 'route', color: 'teal', route: '/gps-tracking/trips' },
      { label: 'Stops Today', value: o.stopsToday, icon: 'stop_circle', color: 'blue', route: '/gps-tracking/trips' },
      { label: 'Geofence Entries', value: o.geofenceEntriesToday, icon: 'fence', color: 'sky', route: '/gps-tracking/geofences' },
      { label: 'Overspeed Events', value: o.overspeedEventsToday, icon: 'warning', color: 'rose', route: '/gps-tracking/alerts' },
      { label: 'Engine Hours', value: o.engineHours != null ? o.engineHours.toFixed(1) : '—', icon: 'timelapse', color: 'amber' },
      { label: 'Utilization', value: o.utilizationPercent != null ? o.utilizationPercent.toFixed(0) : '—', icon: 'insights', color: 'green', suffix: o.utilizationPercent != null ? '%' : '' }
    ];
  }

  formatMinutes(minutes: number): string {
    const h = Math.floor(minutes / 60);
    const m = Math.round(minutes % 60);
    return h > 0 ? `${h}h ${m}m` : `${m}m`;
  }

  private loadWidgets(): void {
    this.gps.getAlertEvents(undefined, undefined, 'critical', 'active', 5).subscribe({
      next: events => { this.criticalAlerts = events.map(e => this.toCriticalAlert(e)); },
      error: () => { this.criticalAlerts = []; }
    });

    this.gps.getAlertEvents(undefined, undefined, undefined, undefined, 8).subscribe({
      next: events => { this.recentActivities = events.map(e => this.toActivityEvent(e)); },
      error: () => { this.recentActivities = []; }
    });

    this.assignmentsLoading = true;
    this.fleet.getAssignments().subscribe({
      next: rows => {
        this.assignments = rows.map(r => ({
          id: r.id,
          vehicleId: r.vehicleName,
          vehicleModel: r.assignmentType,
          driver: r.driverName ?? '—',
          route: r.assignmentType,
          status: r.status,
          eta: r.endAt ? new Date(r.endAt).toLocaleDateString() : '—'
        } satisfies AssignmentItem));
        this.assignmentsLoading = false;
      },
      error: () => {
        this.assignments = [];
        this.assignmentsLoading = false;
      }
    });
  }

  private toCriticalAlert(e: GpsAlertEvent): CriticalAlert {
    return {
      id: String(e.id),
      icon: this.iconForEventType(e.eventType),
      tone: e.severity === 'critical' ? 'error' : 'warning',
      title: e.vehicleName ?? `Vehicle #${e.vehicleId}`,
      detail: e.message
    };
  }

  private toActivityEvent(e: GpsAlertEvent): ActivityEvent {
    return {
      id: String(e.id),
      title: `${e.vehicleName ?? 'Vehicle #' + e.vehicleId} — ${e.message}`,
      timeAgo: this.timeAgo(e.timestamp),
      tone: e.severity === 'critical' ? 'primary' : e.severity === 'high' ? 'secondary' : 'muted'
    };
  }

  private iconForEventType(eventType: string): string {
    if (eventType.includes('sos')) return 'sos';
    if (eventType.includes('speed') || eventType.includes('overspeed')) return 'speed';
    if (eventType.includes('power')) return 'power_off';
    if (eventType.includes('battery')) return 'battery_alert';
    if (eventType.includes('geofence')) return 'fence';
    return 'warning';
  }

  private timeAgo(timestamp: string): string {
    const diffMs = Date.now() - new Date(timestamp).getTime();
    const minutes = Math.floor(diffMs / 60_000);
    if (minutes < 1) return 'Just now';
    if (minutes < 60) return `${minutes}m ago`;
    const hours = Math.floor(minutes / 60);
    if (hours < 24) return `${hours}h ago`;
    return `${Math.floor(hours / 24)}d ago`;
  }

  onKpiTileClick(tile: KpiTile): void {
    if (!tile.route) return;
    this.router.navigateByUrl(tile.route);
  }

  goToVehicleTrips(vehicleId: number): void {
    this.router.navigate(['/gps-tracking/trips'], { queryParams: { vehicleId } });
  }

  goToDriverTrips(driverId: number): void {
    this.router.navigate(['/gps-tracking/trips'], { queryParams: { driverId } });
  }

  private get periodLabel(): string {
    const preset = this.datePresets.find(p => p.id === this.datePreset);
    return preset ? preset.label : `${this.from} — ${this.to}`;
  }

  exportOverview(kind: 'excel' | 'pdf' | 'csv'): void {
    const o = this.overview;
    if (!o) return;
    const rows = [...this.fleetStatusTiles, ...this.totalsTiles, ...this.todayTiles]
      .map(t => ({ metric: t.label, value: `${t.value}${t.suffix ?? ''}` }));
    const cols = [
      { header: 'Metric', accessor: (r: { metric: string; value: string }) => r.metric },
      { header: 'Value', accessor: (r: { metric: string; value: string }) => r.value }
    ];
    const meta = { filename: `analytics-overview-${this.periodLabel.toLowerCase().replace(/\s+/g, '-')}`, sheetName: 'Overview', title: `Analytics Overview — ${this.periodLabel}` };
    this.runExport(kind, rows, cols, meta);
  }

  exportDriverRanking(kind: 'excel' | 'pdf' | 'csv'): void {
    if (!this.driverRanking.length) return;
    const cols = [
      { header: 'Driver', accessor: (r: DriverScore) => r.driverName },
      { header: 'Score', accessor: (r: DriverScore) => r.score },
      { header: 'Rating', accessor: (r: DriverScore) => r.rating },
      { header: 'Trips', accessor: (r: DriverScore) => r.factors.tripCount },
      { header: 'Distance (km)', accessor: (r: DriverScore) => r.factors.distanceKm },
      { header: 'Overspeed', accessor: (r: DriverScore) => r.factors.overspeedCount }
    ];
    const meta = { filename: `driver-ranking-${this.periodLabel.toLowerCase().replace(/\s+/g, '-')}`, sheetName: 'Driver Ranking', title: `Driver Performance Ranking — ${this.periodLabel}` };
    this.runExport(kind, this.driverRanking, cols, meta);
  }

  exportVehicleRanking(kind: 'excel' | 'pdf' | 'csv'): void {
    if (!this.vehicleRanking.length) return;
    const cols = [
      { header: 'Vehicle', accessor: (r: VehicleRanking) => r.vehicleName },
      { header: 'Distance (km)', accessor: (r: VehicleRanking) => r.distanceKm },
      { header: 'Trips', accessor: (r: VehicleRanking) => r.tripCount },
      { header: 'Avg Speed', accessor: (r: VehicleRanking) => r.avgSpeedKmh },
      { header: 'Cost', accessor: (r: VehicleRanking) => r.fuelCost + r.maintenanceCost },
      { header: 'Cost/KM', accessor: (r: VehicleRanking) => r.costPerKm ?? 'N/A' }
    ];
    const meta = { filename: `vehicle-ranking-${this.periodLabel.toLowerCase().replace(/\s+/g, '-')}`, sheetName: 'Vehicle Ranking', title: `Vehicle Ranking — ${this.periodLabel}` };
    this.runExport(kind, this.vehicleRanking, cols, meta);
  }

  exportFuelByVehicle(kind: 'excel' | 'pdf' | 'csv'): void {
    const rows = this.fuelAnalytics?.byVehicle ?? [];
    if (!rows.length) return;
    const cols = [
      { header: 'Vehicle', accessor: (r: VehicleFuel) => r.vehicleName ?? `Vehicle #${r.vehicleId}` },
      { header: 'Liters', accessor: (r: VehicleFuel) => r.liters },
      { header: 'Cost', accessor: (r: VehicleFuel) => r.cost },
      { header: 'L/100km', accessor: (r: VehicleFuel) => r.litersPer100Km ?? '—' }
    ];
    const meta = { filename: `fuel-by-vehicle-${this.periodLabel.toLowerCase().replace(/\s+/g, '-')}`, sheetName: 'Fuel', title: `Fuel by Vehicle — ${this.periodLabel}` };
    this.runExport(kind, rows, cols, meta);
  }

  exportVehicleHealth(kind: 'excel' | 'pdf' | 'csv'): void {
    if (!this.vehicleHealth.length) return;
    const cols = [
      { header: 'Vehicle', accessor: (r: GpsVehicleHealth) => r.vehicleName },
      { header: 'Battery', accessor: (r: GpsVehicleHealth) => r.batteryLevel ?? '—' },
      { header: 'GPS Signal', accessor: (r: GpsVehicleHealth) => r.gpsSignal ?? '—' },
      { header: 'Maintenance', accessor: (r: GpsVehicleHealth) => r.maintenanceStatus },
      { header: 'Insurance', accessor: (r: GpsVehicleHealth) => r.insuranceStatus },
      { header: 'Tracker Warranty', accessor: (r: GpsVehicleHealth) => r.trackerWarrantyStatus }
    ];
    const meta = { filename: 'vehicle-health', sheetName: 'Vehicle Health', title: 'Vehicle Health' };
    this.runExport(kind, this.vehicleHealth, cols, meta);
  }

  private runExport<T>(kind: 'excel' | 'pdf' | 'csv', rows: T[], cols: ExportColumn<T>[], meta: ExportMeta): void {
    if (kind === 'pdf') this.exportService.exportPdf(rows, cols, meta);
    else if (kind === 'csv') this.exportService.exportCsv(rows, cols, meta);
    else this.exportService.exportExcel(rows, cols, meta);
  }
}
