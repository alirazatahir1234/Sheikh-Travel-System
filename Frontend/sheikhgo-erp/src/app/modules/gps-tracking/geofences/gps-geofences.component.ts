import {
  Component, OnInit, OnDestroy, AfterViewInit, ElementRef, ViewChild
} from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { Subscription } from 'rxjs';
import { GpsTrackingService } from '../../../core/services/gps-tracking.service';
import { VehicleService } from '../../../core/services/vehicle.service';
import { PlatformService } from '../../../core/services/platform.service';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import {
  Geofence,
  GeofenceAssignment,
  GeofenceStats,
  GpsAlertEvent
} from '../../../core/models/gps-tracking.model';
import { VehicleListItem } from '../../../core/models/vehicle.model';
import { Branch, Department } from '../../../core/models/platform.model';
import { MAP_TILE_STACKS } from '../../../core/leaflet/leaflet-map-tiles';
import { L } from '../../../core/leaflet/leaflet-cluster';
import {
  addGeofenceBoundary,
  clearLayerGroup,
  extractGeofenceGeometry,
  fitGeofencesBounds
} from '../../../core/leaflet/geofence-layer';
import '@geoman-io/leaflet-geoman-free';
import type * as LeafletTypes from 'leaflet';

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
  drawHint = 'Draw a circle, polygon, or rectangle on the map — or adjust the default circle below.';

  private map: LeafletTypes.Map | null = null;
  private fenceLayer: LeafletTypes.LayerGroup | null = null;
  private draftLayer: LeafletTypes.Layer | null = null;
  private geomanReady = false;
  private formSub?: Subscription;
  private syncingFromMap = false;

  form!: ReturnType<FormBuilder['group']>;

  constructor(
    private gps: GpsTrackingService,
    private vehiclesApi: VehicleService,
    private platform: PlatformService,
    private fb: FormBuilder,
    private toast: UiToastService,
    private sanitizer: DomSanitizer
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
    setTimeout(() => this.initMap(), 80);
  }

  ngOnDestroy(): void {
    this.formSub?.unsubscribe();
    this.map?.remove();
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

  private initMap(): void {
    if (!this.mapEl?.nativeElement || this.map) return;
    const tiles = MAP_TILE_STACKS['street'][0];
    this.map = L.map(this.mapEl.nativeElement, { zoomControl: true }).setView([31.52, 74.35], 11);
    L.tileLayer(tiles.url, {
      attribution: tiles.attribution,
      subdomains: (tiles.subdomains ?? 'abc') as string,
      maxZoom: tiles.maxZoom ?? 19
    }).addTo(this.map);

    this.fenceLayer = L.layerGroup().addTo(this.map);
    this.setupGeoman();
    this.renderFences();
  }

  private setupGeoman(): void {
    if (!this.map || this.geomanReady) return;
    const map = this.map as LeafletTypes.Map & {
      pm: {
        addControls: (o: object) => void;
        setPathOptions: (o: object) => void;
        enableGlobalEditMode: () => void;
        disableGlobalEditMode: () => void;
        enableGlobalDragMode: () => void;
        disableGlobalDragMode: () => void;
      };
    };
    if (!map.pm) return;

    map.pm.addControls({
      position: 'topleft',
      drawMarker: false,
      drawCircleMarker: false,
      drawPolyline: false,
      drawText: false,
      drawCircle: true,
      drawRectangle: true,
      drawPolygon: true,
      editMode: true,
      dragMode: true,
      removalMode: true,
      cutPolygon: false,
      rotateMode: false
    });
    this.applyGeomanPathColor();

    this.map.on('pm:create', (e: unknown) => {
      if (this.shapeLocked) {
        const ev = e as { layer: LeafletTypes.Layer };
        this.map?.removeLayer(ev.layer);
        this.toast.info('Shape is locked. Unlock to redraw.');
        return;
      }
      const ev = e as { layer: LeafletTypes.Layer };
      if (this.draftLayer && this.map) this.map.removeLayer(this.draftLayer);
      this.draftLayer = ev.layer;
      this.applyDraftStyle();
      this.patchFormFromLayer(ev.layer);
      this.drawHint = 'Drag the shape to reposition, or adjust radius / vertices before saving.';
      if (!this.showForm) {
        this.editGeofence = null;
        this.form.patchValue({
          name: '',
          description: '',
          notes: '',
          category: 'Warehouse',
          isActive: true,
          notifyOnEntry: true,
          notifyOnExit: true
        }, { emitEvent: false });
        this.formVehicleIds = new Set();
        this.showForm = true;
      }
    });

    this.map.on('pm:edit', (e: unknown) => {
      if (this.shapeLocked) return;
      const ev = e as { layer: LeafletTypes.Layer };
      this.patchFormFromLayer(ev.layer);
    });

    this.map.on('pm:dragend', (e: unknown) => {
      if (this.shapeLocked) return;
      const ev = e as { layer: LeafletTypes.Layer };
      this.patchFormFromLayer(ev.layer);
    });

    this.geomanReady = true;
  }

  private applyGeomanPathColor(): void {
    const map = this.map as LeafletTypes.Map & { pm?: { setPathOptions: (o: object) => void } };
    const color = this.form.value.color || '#0f766e';
    map.pm?.setPathOptions({
      color,
      fillColor: color,
      fillOpacity: 0.15,
      weight: 2
    });
  }

  private patchFormFromLayer(layer: LeafletTypes.Layer): void {
    const geom = extractGeofenceGeometry(layer);
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
    this.applyGeomanPathColor();
    this.shapeLocked = !!this.form.value.lockShape;
    this.applyDraftStyle();

    const type = (this.form.value.areaType || 'circle').toLowerCase();
    if (type !== 'circle' || !this.draftLayer || !this.map) return;

    const circle = this.draftLayer as LeafletTypes.Circle;
    if (typeof circle.setRadius !== 'function' || typeof circle.setLatLng !== 'function') return;

    const lat = Number(this.form.value.centerLat);
    const lng = Number(this.form.value.centerLng);
    const radius = Number(this.form.value.radiusMeters);
    if (!Number.isFinite(lat) || !Number.isFinite(lng) || !(radius > 0)) return;

    circle.setLatLng([lat, lng]);
    circle.setRadius(radius);
  }

  private applyDraftStyle(): void {
    if (!this.draftLayer) return;
    const color = this.form.value.color || '#0f766e';
    const layer = this.draftLayer as LeafletTypes.Path;
    if (typeof layer.setStyle === 'function') {
      layer.setStyle({ color, fillColor: color, fillOpacity: 0.18, weight: 2 });
    }
  }

  /** Place / refresh a default editable circle when opening create. */
  private ensureDefaultDraftCircle(): void {
    if (!this.map) return;
    this.clearDraft();
    const center = this.map.getCenter();
    const radius = Number(this.form.value.radiusMeters) || 500;
    const color = this.form.value.color || '#0f766e';
    const circle = L.circle(center, {
      radius,
      color,
      fillColor: color,
      fillOpacity: 0.18,
      weight: 2
    }).addTo(this.map);
    this.draftLayer = circle;
    this.syncingFromMap = true;
    this.form.patchValue({
      areaType: 'circle',
      centerLat: +center.lat.toFixed(6),
      centerLng: +center.lng.toFixed(6),
      radiusMeters: radius,
      geoJson: null
    });
    setTimeout(() => { this.syncingFromMap = false; }, 0);
    this.map.fitBounds(circle.getBounds().pad(0.3));
    this.drawHint = 'Drag the circle to reposition or change the radius below — no need to redraw.';
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
    if (!this.fenceLayer || !this.map) return;
    clearLayerGroup(this.fenceLayer);
    for (const g of this.geofences) {
      const layer = addGeofenceBoundary(this.fenceLayer, g);
      if (!layer) continue;
      layer.bindTooltip(g.name);
      layer.on('click', () => this.selectFence(g));
    }
    if (this.geofences.length && !this.showForm) {
      fitGeofencesBounds(this.map, this.fenceLayer);
    }
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
    setTimeout(() => this.ensureDefaultDraftCircle(), 50);
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
    this.drawHint = 'Update details below. Change radius or color to preview on the map.';
    this.loadFenceDetails(g.id);

    // Mirror existing geometry as draft for live preview edits
    setTimeout(() => {
      if (!this.map) return;
      this.clearDraft();
      if ((g.areaType || 'circle').toLowerCase() === 'circle') {
        const color = g.color || '#0f766e';
        this.draftLayer = L.circle([g.centerLat, g.centerLng], {
          radius: g.radiusMeters || 500,
          color,
          fillColor: color,
          fillOpacity: 0.18,
          weight: 2
        }).addTo(this.map);
        this.map.fitBounds((this.draftLayer as LeafletTypes.Circle).getBounds().pad(0.25));
      } else if (g.geoJson) {
        try {
          const geo = JSON.parse(g.geoJson);
          this.draftLayer = L.geoJSON(geo, {
            style: {
              color: g.color || '#0f766e',
              fillColor: g.color || '#0f766e',
              fillOpacity: 0.18,
              weight: 2
            }
          }).addTo(this.map!);
          const bounds = (this.draftLayer as LeafletTypes.FeatureGroup).getBounds?.();
          if (bounds?.isValid()) this.map.fitBounds(bounds.pad(0.25));
        } catch { /* ignore */ }
      }
    }, 50);
  }

  closeForm(): void {
    this.showForm = false;
    this.clearDraft();
    this.renderFences();
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
      this.gps.updateGeofence(this.editGeofence.id, payload).subscribe({
        next: () => {
          this.saving = false;
          this.toast.success('Geofence updated');
          this.closeForm();
          this.load();
          this.loadStats();
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
          error: () => finish()
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
        error: () => createRuleThenFinish()
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

  private clearDraft(): void {
    if (this.draftLayer && this.map) {
      this.map.removeLayer(this.draftLayer);
      this.draftLayer = null;
    }
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
