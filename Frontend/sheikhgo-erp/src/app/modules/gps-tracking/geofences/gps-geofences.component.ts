import {
  Component, OnInit, OnDestroy, AfterViewInit, ElementRef, ViewChild, NgZone
} from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { Subscription } from 'rxjs';
import { GpsTrackingService } from '../../../core/services/gps-tracking.service';
import { VehicleService } from '../../../core/services/vehicle.service';
import { PlatformService } from '../../../core/services/platform.service';
import { GoogleMapsLoaderService } from '../../../core/services/google-maps-loader.service';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import {
  Geofence,
  GeofenceAssignment,
  GeofenceStats,
  GpsAlertEvent
} from '../../../core/models/gps-tracking.model';
import { VehicleListItem } from '../../../core/models/vehicle.model';
import { Branch, Department } from '../../../core/models/platform.model';
import {
  applyGmapShapeStyle,
  createGmapFenceOverlay,
  extractGmapGeofenceGeometry,
  fitGmapOverlays,
  setGmapShapeLocked,
  type GmapEditableShape
} from '../../../core/google-maps/gmap-geofence-layer';
import { triggerGmapResize } from '../../../core/google-maps/gmap-theme';

const CATEGORIES = [
  'Warehouse', 'Office', 'Customer', 'Parking', 'Fuel Station',
  'Restricted', 'Home', 'Service Area', 'Delivery Zone'
];

const COLOR_PRESETS: { hex: string; label: string }[] = [
  { hex: '#0f766e', label: 'Teal' },
  { hex: '#2563eb', label: 'Blue' },
  { hex: '#dc2626', label: 'Red' },
  { hex: '#f59e0b', label: 'Amber' },
  { hex: '#16a34a', label: 'Green' },
  { hex: '#7c3aed', label: 'Purple' },
  { hex: '#ea580c', label: 'Orange' },
  { hex: '#64748b', label: 'Slate' }
];

interface FenceOverlayEntry {
  fence: Geofence;
  shape: GmapEditableShape;
  clickListener: google.maps.MapsEventListener;
}

@Component({
  standalone: false,
  selector: 'app-gps-geofences',
  templateUrl: './gps-geofences.component.html',
  styleUrls: ['./gps-geofences.component.scss']
})
export class GpsGeofencesComponent implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild('mapEl', { static: false }) mapEl!: ElementRef<HTMLDivElement>;

  geofences: Geofence[] = [];
  stats: GeofenceStats | null = null;
  loading = false;
  saving = false;
  showForm = false;
  showAssign = false;
  showPostCreatePrompt = false;
  editGeofence: Geofence | null = null;
  selected: Geofence | null = null;
  pendingAssignGeofenceId: number | null = null;
  mapError: string | null = null;
  mapReady = false;
  drawMode: '' | 'circle' | 'polygon' | 'rectangle' = '';

  search = '';
  filterType = '';
  filterStatus: '' | 'active' | 'inactive' = '';

  categories = CATEGORIES;
  colorPresets = COLOR_PRESETS;
  vehicles: VehicleListItem[] = [];
  branches: Branch[] = [];
  departments: Department[] = [];
  assignments: GeofenceAssignment[] = [];
  events: GpsAlertEvent[] = [];
  selectedVehicleIds = new Set<number>();
  formVehicleIds = new Set<number>();
  assignBranchId: number | null = null;
  assignDepartmentId: number | null = null;
  vehiclePickSearch = '';
  formVehicleSearch = '';
  shapeLocked = false;
  drawHint = 'Use Draw tools on the map, or Add Geofence for a default circle you can drag.';

  private map: google.maps.Map | null = null;
  /** Runtime DrawingManager; @types/google.maps omit MVC helpers (setMap/setOptions). */
  private drawingManager: google.maps.drawing.DrawingManager | null = null;
  private get dm(): any {
    return this.drawingManager;
  }
  private draftShape: GmapEditableShape | null = null;
  private draftListeners: google.maps.MapsEventListener[] = [];
  private fenceEntries: FenceOverlayEntry[] = [];
  private formSub?: Subscription;
  private authFailSub?: Subscription;
  private syncingFromMap = false;
  private resizeObserver: ResizeObserver | null = null;

  form!: ReturnType<FormBuilder['group']>;

  constructor(
    private gps: GpsTrackingService,
    private vehiclesApi: VehicleService,
    private platform: PlatformService,
    private fb: FormBuilder,
    private toast: UiToastService,
    private sanitizer: DomSanitizer,
    private googleMapsLoader: GoogleMapsLoaderService,
    private ngZone: NgZone
  ) {
    this.form = this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(100)]],
      description: ['', Validators.maxLength(250)],
      notes: ['', Validators.maxLength(250)],
      color: ['#0f766e', Validators.required],
      category: ['Warehouse', Validators.required],
      areaType: ['circle'],
      centerLat: [31.52, Validators.required],
      centerLng: [74.35, Validators.required],
      radiusMeters: [500, [Validators.required, Validators.min(50), Validators.max(100000)]],
      geoJson: [null as string | null],
      isActive: [true],
      notifyOnEntry: [true],
      notifyOnExit: [true],
      lockShape: [false]
    });
  }

  ngOnInit(): void {
    this.load();
    this.loadStats();
    this.vehiclesApi.getAll(1, 500).subscribe({
      next: r => { this.vehicles = r.items; },
      error: () => {}
    });
    this.platform.getBranches().subscribe({ next: b => { this.branches = b; }, error: () => {} });
    this.platform.getDepartments().subscribe({ next: d => { this.departments = d; }, error: () => {} });

    this.formSub = this.form.valueChanges.subscribe(() => {
      if (this.syncingFromMap || !this.showForm) return;
      this.syncDraftFromForm();
    });
  }

  ngAfterViewInit(): void {
    setTimeout(() => void this.initMap(), 40);
  }

  ngOnDestroy(): void {
    this.formSub?.unsubscribe();
    this.authFailSub?.unsubscribe();
    this.resizeObserver?.disconnect();
    this.clearDraftListeners();
    this.clearFenceOverlays();
    this.clearDraft();
    this.dm?.setMap(null);
    this.drawingManager = null;
    this.map = null;
  }

  get filteredGeofences(): Geofence[] {
    const q = this.search.trim().toLowerCase();
    return this.geofences.filter(g => {
      if (this.filterType && g.areaType?.toLowerCase() !== this.filterType) return false;
      if (this.filterStatus === 'active' && !g.isActive) return false;
      if (this.filterStatus === 'inactive' && g.isActive) return false;
      if (!q) return true;
      return (
        g.name.toLowerCase().includes(q) ||
        (g.category ?? '').toLowerCase().includes(q) ||
        (g.description ?? '').toLowerCase().includes(q)
      );
    });
  }

  get formAssignableVehicles(): VehicleListItem[] {
    return this.filterVehicles(this.formVehicleSearch, 60);
  }

  get selectedFormVehicles(): VehicleListItem[] {
    return this.vehicles.filter(v => this.formVehicleIds.has(v.id));
  }

  get assignableVehicles(): VehicleListItem[] {
    return this.filterVehicles(this.vehiclePickSearch, 80);
  }

  get selectedAssignVehicles(): VehicleListItem[] {
    return this.vehicles.filter(v => this.selectedVehicleIds.has(v.id));
  }

  private filterVehicles(query: string, limit: number): VehicleListItem[] {
    const q = query.trim().toLowerCase();
    const list = !q
      ? this.vehicles
      : this.vehicles.filter(v =>
          v.name.toLowerCase().includes(q) ||
          (v.registrationNumber ?? '').toLowerCase().includes(q) ||
          (v.driverName ?? '').toLowerCase().includes(q) ||
          (v.gpsImei ?? '').toLowerCase().includes(q)
        );
    return list.slice(0, limit);
  }

  highlightMatch(text: string | null | undefined, query: string): SafeHtml {
    const raw = text ?? '';
    const q = query.trim();
    if (!q || !raw) return this.sanitizer.bypassSecurityTrustHtml(this.escapeHtml(raw));
    const idx = raw.toLowerCase().indexOf(q.toLowerCase());
    if (idx < 0) return this.sanitizer.bypassSecurityTrustHtml(this.escapeHtml(raw));
    const before = this.escapeHtml(raw.slice(0, idx));
    const match = this.escapeHtml(raw.slice(idx, idx + q.length));
    const after = this.escapeHtml(raw.slice(idx + q.length));
    return this.sanitizer.bypassSecurityTrustHtml(`${before}<mark>${match}</mark>${after}`);
  }

  private escapeHtml(s: string): string {
    return s
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;');
  }

  get areaSqMeters(): number | null {
    const type = (this.form.value.areaType || 'circle').toLowerCase();
    if (type === 'circle') {
      const r = Number(this.form.value.radiusMeters) || 0;
      return Math.round(Math.PI * r * r);
    }
    return null;
  }

  get radiusKmLabel(): string {
    const r = Number(this.form.value.radiusMeters) || 0;
    if (r >= 1000) return `${(r / 1000).toFixed(2)} km`;
    return `${Math.round(r)} m`;
  }

  get colorLabel(): string {
    const hex = (this.form.value.color || '').toLowerCase();
    return this.colorPresets.find(c => c.hex.toLowerCase() === hex)?.label || 'Custom';
  }

  private async initMap(): Promise<void> {
    if (!this.mapEl?.nativeElement || this.map) return;

    if (!this.googleMapsLoader.isConfigured) {
      this.mapError = 'Google Maps API key is not configured.';
      return;
    }

    this.authFailSub = this.googleMapsLoader.authFailures$.subscribe(msg => {
      this.ngZone.run(() => { this.mapError = msg; });
    });

    try {
      const bootstrapped = await this.googleMapsLoader.load();
      if (!bootstrapped) {
        this.mapError = this.googleMapsLoader.failureMessage || 'Google Maps failed to load.';
        return;
      }
      await this.googleMapsLoader.importLibrary('maps');
      await this.googleMapsLoader.importLibrary('drawing');
      if (this.googleMapsLoader.authFailed) {
        this.mapError = this.googleMapsLoader.failureMessage || 'Google Maps authentication failed.';
        return;
      }
    } catch (err) {
      this.mapError = err instanceof Error ? err.message : 'Google Maps failed to load.';
      return;
    }

    this.map = new google.maps.Map(this.mapEl.nativeElement, {
      center: { lat: 31.52, lng: 74.35 },
      zoom: 11,
      mapTypeControl: true,
      streetViewControl: false,
      fullscreenControl: true,
      zoomControl: true,
      gestureHandling: 'greedy'
    });

    this.setupDrawingManager();
    this.observeMapResize();
    this.mapReady = true;
    this.renderFences();
    setTimeout(() => triggerGmapResize(this.map), 100);
  }

  private setupDrawingManager(): void {
    if (!this.map || this.drawingManager) return;

    const color = this.form.value.color || '#0f766e';
    this.drawingManager = new (google.maps.drawing.DrawingManager as any)({
      drawingMode: null,
      drawingControl: false,
      circleOptions: {
        fillColor: color,
        fillOpacity: 0.18,
        strokeColor: color,
        strokeWeight: 2,
        editable: true,
        draggable: true,
        clickable: true
      },
      polygonOptions: {
        fillColor: color,
        fillOpacity: 0.18,
        strokeColor: color,
        strokeWeight: 2,
        editable: true,
        draggable: true,
        clickable: true
      },
      rectangleOptions: {
        fillColor: color,
        fillOpacity: 0.18,
        strokeColor: color,
        strokeWeight: 2,
        editable: true,
        draggable: true,
        clickable: true
      }
    });
    this.dm.setMap(this.map);

    this.dm.addListener('overlaycomplete', (event: { overlay: GmapEditableShape; type: string }) => {
      this.ngZone.run(() => this.onOverlayComplete(event));
    });
  }

  private onOverlayComplete(event: { overlay: GmapEditableShape; type: string }): void {
    if (this.shapeLocked) {
      event.overlay.setMap(null);
      this.toast.info('Shape is locked. Unlock to redraw.');
      this.setDrawMode('');
      return;
    }

    this.clearDraft(false);
    const overlay = event.overlay as GmapEditableShape;
    this.draftShape = overlay;
    this.attachDraftListeners(overlay);
    this.patchFormFromShape(overlay);
    this.drawHint = 'Drag handles to adjust the zone, then fill in the name and save.';
    this.setDrawMode('');

    if (!this.showForm) {
      this.editGeofence = null;
      this.form.patchValue({
        name: '',
        description: '',
        notes: '',
        category: 'Warehouse',
        isActive: true,
        notifyOnEntry: true,
        notifyOnExit: true,
        lockShape: false
      }, { emitEvent: false });
      this.formVehicleIds = new Set();
      this.showForm = true;
    }

    applyGmapShapeStyle(overlay, this.form.value.color || '#0f766e');
    setGmapShapeLocked(overlay, this.shapeLocked);
  }

  setDrawMode(mode: '' | 'circle' | 'polygon' | 'rectangle'): void {
    this.drawMode = mode;
    if (!this.drawingManager) return;
    if (this.shapeLocked) {
      this.dm.setDrawingMode(null);
      this.drawMode = '';
      return;
    }
    const OverlayType = google.maps.drawing.OverlayType;
    if (mode === 'circle') this.dm.setDrawingMode(OverlayType.CIRCLE);
    else if (mode === 'polygon') this.dm.setDrawingMode(OverlayType.POLYGON);
    else if (mode === 'rectangle') this.dm.setDrawingMode(OverlayType.RECTANGLE);
    else this.dm.setDrawingMode(null);
  }

  private attachDraftListeners(shape: GmapEditableShape): void {
    this.clearDraftListeners();
    const bump = (): void => {
      this.ngZone.run(() => this.patchFormFromShape(shape));
    };

    if (shape instanceof google.maps.Circle) {
      this.draftListeners.push(
        shape.addListener('radius_changed', bump),
        shape.addListener('center_changed', bump),
        shape.addListener('dragend', bump)
      );
    } else if (shape instanceof google.maps.Rectangle) {
      this.draftListeners.push(
        shape.addListener('bounds_changed', bump),
        shape.addListener('dragend', bump)
      );
    } else if (shape instanceof google.maps.Polygon) {
      const path = shape.getPath();
      this.draftListeners.push(
        path.addListener('set_at', bump),
        path.addListener('insert_at', bump),
        path.addListener('remove_at', bump),
        shape.addListener('dragend', bump)
      );
    }
  }

  private clearDraftListeners(): void {
    for (const l of this.draftListeners) l.remove();
    this.draftListeners = [];
  }

  private patchFormFromShape(shape: GmapEditableShape): void {
    const geom = extractGmapGeofenceGeometry(shape);
    if (!geom) return;
    this.syncingFromMap = true;
    this.form.patchValue({
      areaType: geom.areaType,
      centerLat: geom.centerLat,
      centerLng: geom.centerLng,
      radiusMeters: geom.radiusMeters || this.form.value.radiusMeters || 500,
      geoJson: geom.geoJson
    });
    setTimeout(() => { this.syncingFromMap = false; }, 0);
  }

  private syncDraftFromForm(): void {
    const color = this.form.value.color || '#0f766e';
    this.shapeLocked = !!this.form.value.lockShape;
    if (this.draftShape) {
      applyGmapShapeStyle(this.draftShape, color);
      setGmapShapeLocked(this.draftShape, this.shapeLocked);
    }
    if (this.drawingManager) {
      const opts = {
        fillColor: color,
        strokeColor: color,
        fillOpacity: 0.18,
        strokeWeight: 2,
        editable: !this.shapeLocked,
        draggable: !this.shapeLocked
      };
      this.dm.setOptions({
        circleOptions: opts,
        polygonOptions: opts,
        rectangleOptions: opts
      });
      if (this.shapeLocked) {
        this.dm.setDrawingMode(null);
        this.drawMode = '';
      }
    }

    const type = (this.form.value.areaType || 'circle').toLowerCase();
    if (type !== 'circle' || !(this.draftShape instanceof google.maps.Circle)) return;

    const lat = Number(this.form.value.centerLat);
    const lng = Number(this.form.value.centerLng);
    const radius = Number(this.form.value.radiusMeters);
    if (!Number.isFinite(lat) || !Number.isFinite(lng) || !(radius > 0)) return;

    const center = this.draftShape.getCenter();
    if (!center || Math.abs(center.lat() - lat) > 1e-7 || Math.abs(center.lng() - lng) > 1e-7) {
      this.draftShape.setCenter({ lat, lng });
    }
    if (Math.abs(this.draftShape.getRadius() - radius) > 0.5) {
      this.draftShape.setRadius(radius);
    }
  }

  private ensureDefaultDraftCircle(): void {
    if (!this.map) return;
    this.clearDraft();
    const center = this.map.getCenter();
    const lat = center?.lat() ?? 31.52;
    const lng = center?.lng() ?? 74.35;
    const radius = Number(this.form.value.radiusMeters) || 500;
    const color = this.form.value.color || '#0f766e';
    const circle = new google.maps.Circle({
      map: this.map,
      center: { lat, lng },
      radius,
      fillColor: color,
      fillOpacity: 0.18,
      strokeColor: color,
      strokeWeight: 2,
      editable: !this.shapeLocked,
      draggable: !this.shapeLocked
    });
    this.draftShape = circle;
    this.attachDraftListeners(circle);
    this.syncingFromMap = true;
    this.form.patchValue({
      areaType: 'circle',
      centerLat: +lat.toFixed(6),
      centerLng: +lng.toFixed(6),
      radiusMeters: radius,
      geoJson: null
    });
    setTimeout(() => { this.syncingFromMap = false; }, 0);
    fitGmapOverlays(this.map, [circle]);
    this.drawHint = 'Drag the circle or use radius controls — or pick Polygon / Rectangle to redraw.';
  }

  bumpRadius(delta: number): void {
    const current = Number(this.form.value.radiusMeters) || 500;
    const next = Math.min(100000, Math.max(50, current + delta));
    this.form.patchValue({ radiusMeters: next });
  }

  selectColor(hex: string): void {
    this.form.patchValue({ color: hex });
  }

  load(): void {
    this.loading = true;
    this.gps.getGeofences().subscribe({
      next: rows => {
        this.geofences = rows;
        this.loading = false;
        this.renderFences();
      },
      error: () => {
        this.loading = false;
        this.toast.error('Failed to load geofences');
      }
    });
  }

  loadStats(): void {
    this.gps.getGeofenceStats().subscribe({
      next: s => { this.stats = s; },
      error: () => { this.stats = null; }
    });
  }

  private renderFences(): void {
    if (!this.map) return;
    this.clearFenceOverlays();
    const hideId = this.showForm && this.editGeofence ? this.editGeofence.id : null;
    for (const g of this.geofences) {
      if (hideId != null && g.id === hideId) continue;
      const shape = createGmapFenceOverlay(this.map, g, {
        editable: false,
        draggable: false,
        fillOpacity: 0.12
      });
      if (!shape) continue;
      const clickListener = shape.addListener('click', () => {
        this.ngZone.run(() => this.selectFence(g));
      });
      this.fenceEntries.push({ fence: g, shape, clickListener });
    }
    if (this.geofences.length && !this.showForm) {
      fitGmapOverlays(this.map, this.fenceEntries.map(e => e.shape));
    }
  }

  private clearFenceOverlays(): void {
    for (const e of this.fenceEntries) {
      e.clickListener.remove();
      e.shape.setMap(null);
    }
    this.fenceEntries = [];
  }

  selectFence(g: Geofence): void {
    this.selected = g;
    this.loadFenceDetails(g.id);
  }

  private loadFenceDetails(id: number): void {
    this.gps.getGeofenceAssignments(id).subscribe({
      next: a => { this.assignments = a; },
      error: () => { this.assignments = []; }
    });
    this.gps.getGeofenceEvents(id).subscribe({
      next: e => { this.events = e; },
      error: () => { this.events = []; }
    });
  }

  openCreate(): void {
    this.editGeofence = null;
    this.formVehicleIds = new Set();
    this.formVehicleSearch = '';
    this.shapeLocked = false;
    this.form.reset({
      name: '',
      description: '',
      notes: '',
      color: '#0f766e',
      category: 'Warehouse',
      areaType: 'circle',
      centerLat: 31.52,
      centerLng: 74.35,
      radiusMeters: 500,
      geoJson: null,
      isActive: true,
      notifyOnEntry: true,
      notifyOnExit: true,
      lockShape: false
    });
    this.showForm = true;
    this.setDrawMode('');
    setTimeout(() => {
      triggerGmapResize(this.map);
      this.ensureDefaultDraftCircle();
    }, 50);
  }

  openEdit(g: Geofence): void {
    this.editGeofence = g;
    this.selected = g;
    this.clearDraft();
    this.formVehicleIds = new Set();
    this.form.patchValue({
      name: g.name,
      description: g.description ?? '',
      notes: '',
      color: g.color || '#0f766e',
      category: g.category || 'Warehouse',
      areaType: g.areaType || 'circle',
      centerLat: g.centerLat,
      centerLng: g.centerLng,
      radiusMeters: g.radiusMeters || 500,
      geoJson: g.geoJson ?? null,
      isActive: g.isActive,
      notifyOnEntry: true,
      notifyOnExit: true,
      lockShape: false
    });
    this.showForm = true;
    this.shapeLocked = false;
    this.drawHint = 'Drag handles to reshape, or change radius / color below.';
    this.renderFences();
    this.loadFenceDetails(g.id);
    this.gps.getAlertRules().subscribe({
      next: rules => {
        const rule = rules.find(r => r.geofenceId === g.id);
        if (rule) {
          this.form.patchValue({
            notifyOnEntry: !!rule.alertOnEnter,
            notifyOnExit: !!rule.alertOnExit
          });
        }
      },
      error: () => { /* keep defaults */ }
    });

    setTimeout(() => {
      if (!this.map) return;
      triggerGmapResize(this.map);
      this.clearDraft();
      const shape = createGmapFenceOverlay(this.map, g, {
        editable: true,
        draggable: true,
        fillOpacity: 0.18
      });
      if (!shape) return;
      this.draftShape = shape;
      this.attachDraftListeners(shape);
      this.patchFormFromShape(shape);
      fitGmapOverlays(this.map, [shape]);
    }, 50);
  }

  closeForm(): void {
    this.showForm = false;
    this.setDrawMode('');
    this.clearDraft();
    this.renderFences();
    setTimeout(() => triggerGmapResize(this.map), 50);
  }

  openAssign(g: Geofence): void {
    this.selected = g;
    this.showAssign = true;
    this.showPostCreatePrompt = false;
    this.selectedVehicleIds = new Set();
    this.assignBranchId = null;
    this.assignDepartmentId = null;
    this.gps.getGeofenceAssignments(g.id).subscribe({
      next: a => {
        this.assignments = a;
        a.filter(x => x.vehicleId).forEach(x => this.selectedVehicleIds.add(x.vehicleId!));
      }
    });
  }

  toggleVehiclePick(id: number): void {
    if (this.selectedVehicleIds.has(id)) this.selectedVehicleIds.delete(id);
    else this.selectedVehicleIds.add(id);
  }

  toggleFormVehicle(id: number): void {
    if (this.formVehicleIds.has(id)) this.formVehicleIds.delete(id);
    else this.formVehicleIds.add(id);
  }

  saveAssignments(): void {
    if (!this.selected) return;
    this.gps.upsertGeofenceAssignments(this.selected.id, {
      vehicleIds: [...this.selectedVehicleIds],
      branchId: this.assignBranchId || undefined,
      departmentId: this.assignDepartmentId || undefined,
      replaceVehicles: true
    }).subscribe({
      next: () => {
        this.toast.success('Assignments saved');
        this.showAssign = false;
        this.loadFenceDetails(this.selected!.id);
        this.load();
        this.loadStats();
      },
      error: () => this.toast.error('Failed to save assignments')
    });
  }

  removeAssignment(a: GeofenceAssignment): void {
    if (!this.selected) return;
    this.gps.deleteGeofenceAssignment(this.selected.id, a.id).subscribe({
      next: () => {
        this.assignments = this.assignments.filter(x => x.id !== a.id);
        if (a.vehicleId) this.selectedVehicleIds.delete(a.vehicleId);
        this.toast.success('Assignment removed');
        this.loadStats();
      }
    });
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.toast.error('Please fix the highlighted fields');
      return;
    }
    if (this.draftShape) {
      this.patchFormFromShape(this.draftShape);
    }
    const v = this.form.getRawValue();
    const areaType = (v.areaType || 'circle').toLowerCase();
    if ((areaType === 'polygon' || areaType === 'rectangle') && !v.geoJson) {
      this.toast.error('Draw the polygon/rectangle on the map before saving');
      return;
    }
    if (areaType === 'circle' && !(+v.radiusMeters >= 50)) {
      this.toast.error('Radius must be at least 50 meters');
      return;
    }

    const descriptionParts = [v.description, v.notes].filter(Boolean);
    const payload = {
      name: (v.name || '').trim(),
      areaType,
      centerLat: +v.centerLat,
      centerLng: +v.centerLng,
      radiusMeters: +v.radiusMeters || 0,
      geoJson: v.geoJson,
      color: v.color || '#0f766e',
      category: v.category || null,
      description: descriptionParts.length ? descriptionParts.join('\n') : null,
      isActive: !!v.isActive
    };

    this.saving = true;
    if (this.editGeofence) {
      const geofenceId = this.editGeofence.id;
      const notifyOnEntry = !!v['notifyOnEntry'];
      const notifyOnExit = !!v['notifyOnExit'];
      this.gps.updateGeofence(geofenceId, payload).subscribe({
        next: () => {
          const finishEdit = (extraMsg?: string): void => {
            this.saving = false;
            this.toast.success(extraMsg || 'Geofence updated');
            this.closeForm();
            this.load();
            this.loadStats();
          };
          if (notifyOnEntry || notifyOnExit) {
            this.gps.createAlertRule({
              geofenceId,
              alertOnEnter: notifyOnEntry,
              alertOnExit: notifyOnExit
            }).subscribe({
              next: () => finishEdit(),
              error: (err: { error?: { message?: string } }) => {
                finishEdit();
                this.toast.warning(err?.error?.message || 'Geofence saved, but Entry/Exit alert rule failed.');
              }
            });
          } else {
            finishEdit();
          }
        },
        error: (err: { error?: { message?: string } }) => {
          this.saving = false;
          this.toast.error(err?.error?.message || 'Save failed');
        }
      });
    } else {
      this.gps.createGeofence(payload).subscribe({
        next: id => {
          const geofenceId = typeof id === 'number' ? id : Number(id);
          this.afterCreate(geofenceId, v);
        },
        error: (err: { error?: { message?: string } }) => {
          this.saving = false;
          this.toast.error(err?.error?.message || 'Save failed');
        }
      });
    }
  }

  private afterCreate(geofenceId: number, v: Record<string, unknown>): void {
    const vehicleIds = [...this.formVehicleIds];
    const branchId = this.assignBranchId;
    const departmentId = this.assignDepartmentId;
    const notifyOnEntry = !!v['notifyOnEntry'];
    const notifyOnExit = !!v['notifyOnExit'];

    const finish = (): void => {
      this.saving = false;
      this.toast.success('Geofence created');
      this.closeForm();
      this.load();
      this.loadStats();
      this.pendingAssignGeofenceId = geofenceId;
      if (vehicleIds.length || branchId || departmentId) {
        this.selected = { id: geofenceId } as Geofence;
        this.loadFenceDetails(geofenceId);
      } else {
        this.showPostCreatePrompt = true;
      }
    };

    const createRuleThenFinish = (): void => {
      if (notifyOnEntry || notifyOnExit) {
        this.gps.createAlertRule({
          geofenceId,
          alertOnEnter: notifyOnEntry,
          alertOnExit: notifyOnExit
        }).subscribe({
          next: () => finish(),
          error: (err: { error?: { message?: string } }) => {
            finish();
            this.toast.warning(err?.error?.message || 'Geofence created, but Entry/Exit alert rule failed.');
          }
        });
      } else {
        finish();
      }
    };

    if (vehicleIds.length || branchId || departmentId) {
      this.gps.upsertGeofenceAssignments(geofenceId, {
        vehicleIds,
        branchId: branchId || undefined,
        departmentId: departmentId || undefined,
        replaceVehicles: true
      }).subscribe({
        next: () => createRuleThenFinish(),
        error: (err: { error?: { message?: string } }) => {
          this.toast.warning(err?.error?.message || 'Geofence created, but vehicle/branch assignment failed.');
          createRuleThenFinish();
        }
      });
    } else {
      createRuleThenFinish();
    }
  }

  postCreateAssignYes(): void {
    this.showPostCreatePrompt = false;
    if (!this.pendingAssignGeofenceId) return;
    const g = this.geofences.find(x => x.id === this.pendingAssignGeofenceId)
      || ({ id: this.pendingAssignGeofenceId, name: 'New geofence' } as Geofence);
    this.openAssign(g);
  }

  postCreateAssignLater(): void {
    this.showPostCreatePrompt = false;
    this.pendingAssignGeofenceId = null;
  }

  duplicate(g: Geofence): void {
    this.gps.duplicateGeofence(g.id).subscribe({
      next: () => {
        this.toast.success('Geofence duplicated');
        this.load();
        this.loadStats();
      },
      error: () => this.toast.error('Duplicate failed')
    });
  }

  toggleActive(g: Geofence): void {
    this.gps.updateGeofence(g.id, {
      name: g.name,
      areaType: g.areaType,
      centerLat: g.centerLat,
      centerLng: g.centerLng,
      radiusMeters: g.radiusMeters,
      geoJson: g.geoJson,
      color: g.color,
      category: g.category,
      description: g.description,
      isActive: !g.isActive
    }).subscribe({
      next: () => {
        g.isActive = !g.isActive;
        this.loadStats();
        this.renderFences();
      }
    });
  }

  delete(g: Geofence): void {
    if (!confirm(`Delete geofence "${g.name}"?`)) return;
    this.gps.deleteGeofence(g.id).subscribe({
      next: () => {
        this.toast.success('Geofence deleted');
        if (this.selected?.id === g.id) {
          this.selected = null;
          this.assignments = [];
          this.events = [];
        }
        this.load();
        this.loadStats();
      }
    });
  }

  clearDraftShape(): void {
    if (this.shapeLocked) {
      this.toast.info('Shape is locked. Unlock to clear.');
      return;
    }
    this.clearDraft();
    this.form.patchValue({ geoJson: null, areaType: 'circle' });
    this.drawHint = 'Cleared. Draw a new circle, polygon, or rectangle on the map.';
  }

  private clearDraft(removeFromMap = true): void {
    this.clearDraftListeners();
    if (removeFromMap && this.draftShape) {
      this.draftShape.setMap(null);
    }
    this.draftShape = null;
  }

  private observeMapResize(): void {
    if (!this.mapEl?.nativeElement || typeof ResizeObserver === 'undefined') return;
    this.resizeObserver?.disconnect();
    this.resizeObserver = new ResizeObserver(() => triggerGmapResize(this.map));
    this.resizeObserver.observe(this.mapEl.nativeElement);
  }

  shapeLabel(g: Geofence): string {
    const t = (g.areaType || 'circle').toLowerCase();
    if (t === 'polygon') return 'Polygon';
    if (t === 'rectangle') return 'Rectangle';
    return `Circle · ${Math.round(g.radiusMeters)} m`;
  }

  eventLabel(e: GpsAlertEvent): string {
    return e.eventType === 'geofence_enter' ? 'Entered' : e.eventType === 'geofence_exit' ? 'Exited' : e.eventType;
  }

  formatArea(n: number): string {
    return n.toLocaleString(undefined, { maximumFractionDigits: 0 });
  }
}
