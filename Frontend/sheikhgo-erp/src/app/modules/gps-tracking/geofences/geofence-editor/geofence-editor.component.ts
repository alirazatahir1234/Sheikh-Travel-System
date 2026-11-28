/// <reference types="google.maps" />
import {
  AfterViewInit, Component, ElementRef, HostListener, NgZone, OnDestroy, OnInit, ViewChild, inject
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatDialog } from '@angular/material/dialog';
import { Observable, Subject, Subscription, of } from 'rxjs';
import { catchError, debounceTime, distinctUntilChanged, switchMap } from 'rxjs/operators';
import { GeofenceWorkspaceService } from '../geofence-workspace.service';
import {
  GeofenceAppliesTo,
  GeofenceAreaType,
  GeofenceDetail,
  GeofenceGroup,
  GeofenceListItem,
  GeofenceNotifyChannel,
  GeofenceRecipientOptions,
  GeofenceShape,
  GeofenceTypeOption,
  TestGeofenceShapeResult
} from '../geofence.models';
import {
  EditorErrors,
  GeofenceDraft,
  GeofenceEditorModel,
  RESTRICTED_RED,
  ShapeHistory,
  buildSavePayload,
  clearDraft,
  defaultEditorModel,
  draftKey,
  extraAssignments,
  loadDraft,
  modelFromDetail,
  saveDraft,
  validateEditor
} from '../geofence-editor.model';
import {
  LatLng,
  LatLngBounds,
  MAX_CIRCLE_RADIUS_M,
  MIN_CIRCLE_RADIUS_M,
  circlePath,
  completeShape,
  formatArea,
  formatCoordinate,
  formatDistance,
  haversineMeters,
  isShapeComplete,
  overlapFraction,
  parseCoordinateList,
  parseLatLng,
  polygonAreaSqMeters,
  rectanglePath,
  shapeBounds,
  shapeCenter,
  shapeFromApi,
  shapeMetrics,
  shapeToApi
} from '../geofence-geometry.util';
import {
  APPLIES_TO_OPTIONS,
  NOTIFY_CHANNELS,
  SHAPE_LABELS,
  WEEK_DAYS,
  daysLabel,
  plural
} from '../geofence-format.util';
import {
  DEFAULT_DRAWN_RADIUS_M,
  EditableShapeLayer,
  GeofenceDrawTool,
  GmapHtmlLabel,
  RenderedFence,
  createHtmlLabel,
  renderFence
} from '../geofence-map';
import { VehicleService } from '../../../../core/services/vehicle.service';
import { PlatformService } from '../../../../core/services/platform.service';
import { DriverService } from '../../../../core/services/driver.service';
import { AuthService } from '../../../../core/services/auth.service';
import { TenantTimeService } from '../../../../core/services/tenant-time.service';
import { GoogleMapsLoaderService } from '../../../../core/services/google-maps-loader.service';
import { PlaceSearchService, PlaceSuggestion } from '../../../../core/services/place-search.service';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import { ConfirmDialogComponent, ConfirmDialogData } from '../../../../shared/components/confirm-dialog/confirm-dialog.component';
import { triggerGmapResize } from '../../../../core/google-maps/gmap-theme';
import {
  SheikhGoPlaceSelection,
  SheikhGoPlacesAutocompleteHandle,
  attachSheikhGoPlacesAutocomplete
} from '../../../../core/google-maps/place-autocomplete.new';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import { VehicleListItem } from '../../../../core/models/vehicle.model';
import { Branch, Department } from '../../../../core/models/platform.model';

type PickerKind = 'vehicles' | 'branches' | 'departments' | 'drivers' | 'roles' | 'users';

interface PickOption {
  id: number;
  label: string;
  sub?: string;
}

/** The design's five swatches; red only for a restricted type. */
export const SWATCHES: { hex: string; label: string }[] = [
  { hex: '#0f766e', label: 'Teal' },
  { hex: '#2563eb', label: 'Blue' },
  { hex: '#b45309', label: 'Amber' },
  { hex: RESTRICTED_RED, label: 'Red' },
  { hex: '#7c3aed', label: 'Purple' }
];

const DEFAULT_CORRIDOR_WIDTH_M = 200;
const OVERLAP_MIN = 0.01;

/**
 * Converts a shape when the user picks another shape type, so work is not thrown away: rectangle ↔ polygon keep
 * their corners, a circle becomes a 16-point polygon or its bounding square, a polygon becomes a circle of the same
 * area. Corridors are lines, so switching to or from one starts a new drawing (null).
 */
export function convertShape(shape: GeofenceShape | null, to: GeofenceAreaType): GeofenceShape | null {
  const s = completeShape(shape);
  if (!s || s.areaType === to) return shape;
  if (to === 'corridor' || s.areaType === 'corridor') return null;
  if (to === 'circle') {
    const center = shapeCenter(s);
    const area = polygonAreaSqMeters(s.path ?? []);
    const radius = Math.min(MAX_CIRCLE_RADIUS_M, Math.max(MIN_CIRCLE_RADIUS_M, Math.round(Math.sqrt(area / Math.PI))));
    return center ? { areaType: 'circle', center, radiusMeters: radius } : null;
  }
  if (s.areaType === 'circle') {
    if (to === 'polygon') return { areaType: 'polygon', path: circlePath(s.center!, s.radiusMeters ?? 0, 16) };
    const b = shapeBounds(s)!;
    return { areaType: 'rectangle', path: rectanglePath({ lat: b.maxLat, lng: b.maxLng }, { lat: b.minLat, lng: b.minLng }) };
  }
  if (to === 'rectangle') {
    const b = shapeBounds(s)!;
    return { areaType: 'rectangle', path: rectanglePath({ lat: b.maxLat, lng: b.maxLng }, { lat: b.minLat, lng: b.minLng }) };
  }
  return { areaType: 'polygon', path: [...(s.path ?? [])] };
}

/** A starting shape dropped at a searched place: a 250 m circle, or a 300 m square for polygons and rectangles. */
export function shapeAt(point: LatLng, type: GeofenceAreaType, radius = DEFAULT_DRAWN_RADIUS_M): GeofenceShape | null {
  if (type === 'circle') return { areaType: 'circle', center: point, radiusMeters: radius };
  if (type === 'corridor') return null;
  const half = 150;
  const dLat = half / 111_320;
  const dLng = half / (111_320 * Math.max(Math.cos((point.lat * Math.PI) / 180), 0.01));
  const path = rectanglePath({ lat: point.lat + dLat, lng: point.lng + dLng }, { lat: point.lat - dLat, lng: point.lng - dLng });
  return { areaType: type, path };
}

/** Shortest text the map search box sends to the place search. */
export const PLACE_SEARCH_MIN_CHARS = 3;
/** How long the map search box waits after the last keystroke. */
export const PLACE_SEARCH_DEBOUNCE_MS = 350;
/** A place smaller than this across is shown like a point at zoom 16, which already shows it whole. */
export const PLACE_FIT_MIN_METERS = 1000;

/**
 * Suggestions for the map search box as the user types. null: nothing was searched (under three characters, or a
 * pasted "lat, lng", which Enter handles). []: nothing was found, or the search failed.
 */
export function placeSuggestions(
  query$: Observable<string>,
  search: (text: string) => Observable<PlaceSuggestion[]>
): Observable<PlaceSuggestion[] | null> {
  return query$.pipe(
    debounceTime(PLACE_SEARCH_DEBOUNCE_MS),
    distinctUntilChanged((a, b) => a.trim() === b.trim()),
    switchMap(raw => {
      const text = raw.trim();
      return text.length < PLACE_SEARCH_MIN_CHARS || parseLatLng(text)
        ? of(null)
        : search(text).pipe(catchError(() => of<PlaceSuggestion[]>([])));
    })
  );
}

/** The area to fit the map to for a picked place: a town or a district gets its box, anything smaller none. */
export function placeBounds(place: PlaceSuggestion): LatLngBounds | null {
  const b = place.bbox;
  if (!b) return null;
  const bounds: LatLngBounds = {
    minLat: Math.min(b.lat1, b.lat2),
    maxLat: Math.max(b.lat1, b.lat2),
    minLng: Math.min(b.lon1, b.lon2),
    maxLng: Math.max(b.lon1, b.lon2)
  };
  const midLat = (bounds.minLat + bounds.maxLat) / 2;
  const midLng = (bounds.minLng + bounds.maxLng) / 2;
  const height = haversineMeters({ lat: bounds.minLat, lng: midLng }, { lat: bounds.maxLat, lng: midLng });
  const width = haversineMeters({ lat: midLat, lng: bounds.minLng }, { lat: midLat, lng: bounds.maxLng });
  return Math.max(height, width) < PLACE_FIT_MIN_METERS ? null : bounds;
}

@Component({
  selector: 'app-geofence-editor',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, MatIconModule, MatAutocompleteModule],
  templateUrl: './geofence-editor.component.html',
  styleUrls: ['./geofence-editor.component.scss']
})
export class GeofenceEditorComponent implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild('mapEl') mapEl?: ElementRef<HTMLDivElement>;
  @ViewChild('addressInput') addressInput?: ElementRef<HTMLInputElement>;

  private readonly api = inject(GeofenceWorkspaceService);
  private readonly vehiclesApi = inject(VehicleService);
  private readonly platform = inject(PlatformService);
  private readonly driversApi = inject(DriverService);
  private readonly auth = inject(AuthService);
  private readonly time = inject(TenantTimeService);
  private readonly mapsLoader = inject(GoogleMapsLoaderService);
  private readonly toast = inject(UiToastService);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly zone = inject(NgZone);
  private readonly placeSearch = inject(PlaceSearchService);

  /** What is typed in the map search box. */
  readonly placeQuery$ = new Subject<string>();
  /** Geoapify places for the map search box, nearest to the map's centre first (null: nothing searched). */
  readonly placeResults$ = placeSuggestions(this.placeQuery$, text => this.placeSearch.search(text, this.mapCenter()));
  /** No Geoapify key: the map search box only takes a pasted "lat, lng". */
  readonly placeSearchUnavailable = !this.placeSearch.available;
  readonly placeLabel = (place: PlaceSuggestion | null): string => place?.label ?? '';

  readonly swatches = SWATCHES;
  readonly shapeLabels = SHAPE_LABELS;
  readonly shapeTypes: GeofenceAreaType[] = ['circle', 'polygon', 'rectangle', 'corridor'];
  readonly weekDays = WEEK_DAYS;
  readonly appliesOptions = APPLIES_TO_OPTIONS;
  readonly channels = NOTIFY_CHANNELS;
  readonly minRadius = MIN_CIRCLE_RADIUS_M;
  readonly maxRadius = MAX_CIRCLE_RADIUS_M;

  id: number | null = null;
  model: GeofenceEditorModel = defaultEditorModel();
  shape: GeofenceShape | null = null;
  shapeMode: GeofenceAreaType = 'circle';
  corridorWidth = DEFAULT_CORRIDOR_WIDTH_M;
  detail: GeofenceDetail | null = null;
  private keepChannels: string[] = [];

  loading = false;
  loadError: string | null = null;
  saving = false;
  submitted = false;
  errors: EditorErrors = {};

  types: GeofenceTypeOption[] = [];
  groups: GeofenceGroup[] = [];
  recipients: GeofenceRecipientOptions = { roles: [], users: [] };
  vehicles: VehicleListItem[] = [];
  branches: Branch[] = [];
  departments: Department[] = [];
  drivers: { id: number; name: string }[] = [];
  private otherFences: GeofenceListItem[] = [];

  pasteOpen = false;
  pasteText = '';
  pasteError = '';
  addingType: { name: string; color: string; restricted: boolean } | null = null;
  addingGroup: string | null = null;
  picker: PickerKind | null = null;
  pickerSearch = '';

  overlap: { name: string; pct: number; more: number } | null = null;
  test: { loading: boolean; result: TestGeofenceShapeResult | null; error: boolean } = { loading: false, result: null, error: false };
  draftSavedAt: string | null = null;
  draftOffer: GeofenceDraft | null = null;
  hint = '';

  mapError: string | null = null;
  mapReady = false;
  satellite = false;

  readonly history = new ShapeHistory();
  private burstBefore: GeofenceShape | null | undefined = undefined;
  private burstTimer: ReturnType<typeof setTimeout> | null = null;
  private draftTimer: ReturnType<typeof setTimeout> | null = null;
  private overlapTimer: ReturnType<typeof setTimeout> | null = null;
  private testTimer: ReturnType<typeof setTimeout> | null = null;
  private measureTimer: ReturnType<typeof setTimeout> | null = null;
  private lastSnapshot = '';
  private typeTouched = false;
  private incomingShape: GeofenceShape | null = null;

  private map: google.maps.Map | null = null;
  private layer: EditableShapeLayer | null = null;
  private drawTool: GeofenceDrawTool | null = null;
  private references: RenderedFence[] = [];
  private referencesFitted = false;
  private measureLabel: GmapHtmlLabel | null = null;
  private placeHandles: SheikhGoPlacesAutocompleteHandle[] = [];
  private resizeObserver: ResizeObserver | null = null;
  private readonly subs = new Subscription();

  constructor() {
    const state = this.router.currentNavigation()?.extras.state ?? (typeof window !== 'undefined' ? window.history.state : null);
    const shape = (state as { shape?: GeofenceShape } | null)?.shape;
    this.incomingShape = completeShape(shape);
  }

  private get draftStorageKey(): string {
    const user = this.auth.getCurrentUser();
    return draftKey(user?.tenantId, user?.userId, this.id);
  }

  ngOnInit(): void {
    const raw = this.route.snapshot.paramMap.get('id');
    this.id = raw && Number.isFinite(Number(raw)) ? Number(raw) : null;
    this.loadLookups();
    if (this.id != null) {
      this.loadDetail(this.id);
    } else {
      if (this.incomingShape) this.applyIncomingShape(this.incomingShape);
      this.snapshot();
      this.offerDraft();
    }
  }

  ngAfterViewInit(): void {
    setTimeout(() => void this.initMap(), 0);
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
    for (const t of [this.burstTimer, this.draftTimer, this.overlapTimer, this.testTimer, this.measureTimer]) if (t) clearTimeout(t);
    this.flushDraft();
    this.placeHandles.forEach(h => h.destroy());
    this.resizeObserver?.disconnect();
    this.layer?.destroy();
    this.drawTool?.destroy();
    this.references.forEach(r => r.remove());
    this.measureLabel?.setMap(null);
    this.map = null;
  }

  get isEdit(): boolean {
    return this.id != null;
  }

  // ---- loading ----------------------------------------------------------------------------------------------------

  private loadDetail(id: number): void {
    this.loading = true;
    this.subs.add(this.api.detail(id).subscribe({
      next: d => {
        this.loading = false;
        this.detail = d;
        this.model = modelFromDetail(d);
        this.keepChannels = [...(d.notifyChannels ?? [])];
        const shape = shapeFromApi(d);
        this.shape = shape;
        this.shapeMode = shape?.areaType ?? 'circle';
        if (shape?.areaType === 'corridor') this.corridorWidth = shape.corridorWidthMeters ?? DEFAULT_CORRIDOR_WIDTH_M;
        if (!shape) this.hint = 'The saved shape could not be read. Draw it again, then save.';
        this.snapshot();
        this.offerDraft();
        this.layer?.setShape(this.shape);
        this.layer?.setStyle(this.model.color, this.model.bufferMeters);
        this.layer?.fit();
        this.renderReferences();
        this.afterShapeChange(false);
        if (!shape) this.startDrawing();
      },
      error: err => {
        this.loading = false;
        this.loadError = apiErrorMessage(err, 'This geofence could not be loaded.');
      }
    }));
  }

  private loadLookups(): void {
    this.subs.add(this.api.types().pipe(catchError(() => of([] as GeofenceTypeOption[]))).subscribe(t => (this.types = t ?? [])));
    this.subs.add(this.api.groups().pipe(catchError(() => of([] as GeofenceGroup[]))).subscribe(g => (this.groups = g ?? [])));
    this.subs.add(this.api.recipientOptions().pipe(catchError(() => of(null))).subscribe(r => (this.recipients = r ?? { roles: [], users: [] })));
    this.subs.add(this.api.list().pipe(catchError(() => of([] as GeofenceListItem[]))).subscribe(rows => {
      this.otherFences = (rows ?? []).filter(f => f.id !== this.id);
      this.renderReferences();
      this.scheduleOverlap();
    }));
    this.subs.add(this.vehiclesApi.getAll(1, 500).pipe(catchError(() => of(null))).subscribe(r => (this.vehicles = r?.items ?? [])));
    this.subs.add(this.platform.getBranches().pipe(catchError(() => of([] as Branch[]))).subscribe(b => (this.branches = b ?? [])));
    this.subs.add(this.platform.getDepartments().pipe(catchError(() => of([] as Department[]))).subscribe(d => (this.departments = d ?? [])));
    this.subs.add(this.driversApi.getAll({ page: 1, pageSize: 500 }).pipe(catchError(() => of(null))).subscribe(r => {
      this.drivers = (r?.items ?? []).map(d => ({ id: d.id, name: d.fullName }));
    }));
  }

  /** A shape drawn on the main map: keep it, and pick a matching type for a corridor. */
  private applyIncomingShape(shape: GeofenceShape): void {
    this.shape = shape;
    this.shapeMode = shape.areaType;
    if (shape.areaType === 'corridor') {
      this.corridorWidth = shape.corridorWidthMeters ?? DEFAULT_CORRIDOR_WIDTH_M;
      this.model.type = 'Route corridor';
      this.model.color = '#7c3aed';
    }
  }

  // ---- drafts -----------------------------------------------------------------------------------------------------

  private snapshotOf(): string {
    return JSON.stringify({ model: this.model, shape: this.shape });
  }

  private snapshot(): void {
    this.lastSnapshot = this.snapshotOf();
  }

  get dirty(): boolean {
    return this.snapshotOf() !== this.lastSnapshot;
  }

  private offerDraft(): void {
    const draft = loadDraft(this.draftStorageKey);
    if (draft && JSON.stringify({ model: draft.model, shape: draft.shape }) !== this.lastSnapshot) this.draftOffer = draft;
  }

  restoreDraft(): void {
    const d = this.draftOffer;
    if (!d) return;
    this.draftOffer = null;
    this.model = { ...defaultEditorModel(), ...d.model };
    this.setShape(d.shape ?? null, true);
    if (d.shape?.areaType) this.shapeMode = d.shape.areaType;
    this.draftSavedAt = d.savedAt;
    this.layer?.setStyle(this.model.color, this.model.bufferMeters);
    this.layer?.fit();
  }

  discardDraft(): void {
    this.draftOffer = null;
    clearDraft(this.draftStorageKey);
  }

  /** Called on any input, change or click inside the form; saves a draft a moment later when something changed. */
  markDirty(): void {
    this.layer?.setStyle(this.model.color, this.model.bufferMeters);
    if (this.submitted) this.errors = this.validate();
    if (this.draftTimer) clearTimeout(this.draftTimer);
    this.draftTimer = setTimeout(() => this.flushDraft(), 800);
    this.scheduleTest();
  }

  private flushDraft(): void {
    this.draftTimer = null;
    if (!this.dirty || this.saving || this.draftOffer) return;
    const savedAt = new Date().toISOString();
    if (saveDraft(this.draftStorageKey, { savedAt, model: this.model, shape: this.shape })) this.draftSavedAt = savedAt;
  }

  draftTime(iso: string): string {
    return this.time.formatTime(iso, { hour: 'numeric', minute: '2-digit' });
  }

  // ---- details ----------------------------------------------------------------------------------------------------

  get restrictedType(): boolean {
    const t = this.types.find(x => x.name.toLowerCase() === (this.model.type ?? '').toLowerCase());
    return !!t?.isRestricted;
  }

  get swatchList(): { hex: string; label: string }[] {
    const custom = this.model.color && !SWATCHES.some(s => s.hex === this.model.color.toLowerCase())
      ? [{ hex: this.model.color.toLowerCase(), label: 'Type colour' }]
      : [];
    return [...SWATCHES, ...custom];
  }

  /** From the Type select; a type the user picked is never changed for them afterwards. */
  onTypeChange(value: string, fromUser = true): void {
    if (fromUser) this.typeTouched = true;
    if (value === '__add__') {
      this.addingType = { name: '', color: '#2563eb', restricted: false };
      // Keep the select on the previous type until the new one exists.
      const previous = this.model.type;
      this.model.type = null;
      setTimeout(() => (this.model.type = previous));
      return;
    }
    this.model.type = value || null;
    const t = this.types.find(x => x.name === value);
    if (t?.color) this.model.color = t.color.toLowerCase();
    else if (this.model.color === RESTRICTED_RED && !this.restrictedType) this.model.color = '#0f766e';
    this.markDirty();
  }

  saveNewType(): void {
    const t = this.addingType;
    if (!t || !t.name.trim()) return;
    const color = t.restricted ? RESTRICTED_RED : t.color;
    this.subs.add(this.api.createType(t.name.trim(), color, t.restricted).subscribe({
      next: () => {
        this.addingType = null;
        this.subs.add(this.api.types().pipe(catchError(() => of(this.types))).subscribe(types => {
          this.types = types ?? [];
          this.onTypeChange(t.name.trim());
        }));
        this.toast.success(`Type “${t.name.trim()}” added.`);
      },
      error: err => this.toast.error(apiErrorMessage(err, 'The type could not be added.'))
    }));
  }

  onGroupChange(value: string): void {
    if (value === '__add__') {
      this.addingGroup = '';
      const previous = this.model.groupId;
      this.model.groupId = null;
      setTimeout(() => (this.model.groupId = previous));
      return;
    }
    this.model.groupId = value ? Number(value) : null;
    this.markDirty();
  }

  saveNewGroup(): void {
    const name = (this.addingGroup ?? '').trim();
    if (!name) return;
    this.subs.add(this.api.createGroup(name, this.groups.length).subscribe({
      next: id => {
        this.addingGroup = null;
        this.subs.add(this.api.groups().pipe(catchError(() => of(this.groups))).subscribe(groups => {
          this.groups = groups ?? [];
          this.model.groupId = Number(id);
          this.markDirty();
        }));
      },
      error: err => this.toast.error(apiErrorMessage(err, 'The group could not be created.'))
    }));
  }

  pickColor(hex: string): void {
    if (hex === RESTRICTED_RED && !this.restrictedType) return;
    this.model.color = hex;
    this.markDirty();
  }

  // ---- shape ------------------------------------------------------------------------------------------------------

  get metrics() {
    return shapeMetrics(this.shape);
  }

  formatArea(v: number): string {
    return formatArea(v);
  }

  formatDistance(v: number): string {
    return formatDistance(v);
  }

  get centerText(): string {
    const c = shapeCenter(this.shape);
    return c ? `${formatCoordinate(c.lat)}, ${formatCoordinate(c.lng)}` : '—';
  }

  chooseShape(type: GeofenceAreaType): void {
    this.pasteOpen = false;
    if (this.shapeMode === type && this.shape) return;
    this.shapeMode = type;
    if (this.shape && this.shape.areaType !== type) {
      const next = convertShape(this.shape, type);
      this.setShape(next, true);
    }
    // A new geofence whose type was never picked follows the shape: a corridor is a route corridor.
    if (type === 'corridor' && !this.isEdit && !this.typeTouched && this.model.type !== 'Route corridor') {
      this.onTypeChange('Route corridor', false);
    }
    if (!this.shape) this.startDrawing();
    else this.hint = '';
    this.markDirty();
  }

  togglePaste(): void {
    this.pasteOpen = !this.pasteOpen;
    this.pasteError = '';
    if (this.pasteOpen && !this.pasteText && this.shape) {
      const pts = this.shape.areaType === 'circle' ? [this.shape.center!] : this.shape.path ?? [];
      this.pasteText = pts.map(p => `${formatCoordinate(p.lat)}, ${formatCoordinate(p.lng)}`).join('\n');
    }
  }

  applyPaste(): void {
    const { points, badLines } = parseCoordinateList(this.pasteText);
    if (badLines.length) {
      this.pasteError = `Line ${badLines[0]} is not a “lat, lng” pair.`;
      return;
    }
    let next: GeofenceShape | null = null;
    if (this.shapeMode === 'corridor') {
      if (points.length < 2) {
        this.pasteError = 'A corridor needs at least two points along the route.';
        return;
      }
      next = { areaType: 'corridor', path: points, corridorWidthMeters: this.corridorWidth };
    } else if (points.length === 1) {
      next = { areaType: 'circle', center: points[0], radiusMeters: this.shape?.areaType === 'circle' ? this.shape.radiusMeters : DEFAULT_DRAWN_RADIUS_M };
    } else if (points.length >= 3) {
      next = { areaType: this.shapeMode === 'rectangle' ? 'rectangle' : 'polygon', path: points };
      if (this.shapeMode === 'rectangle') {
        const b = shapeBounds(next)!;
        next = { areaType: 'rectangle', path: rectanglePath({ lat: b.maxLat, lng: b.maxLng }, { lat: b.minLat, lng: b.minLng }) };
      }
    } else {
      this.pasteError = points.length === 2
        ? 'Two points make a line: pick Corridor first, or add a third point for an area.'
        : 'Paste one “lat, lng” per line.';
      return;
    }
    this.drawTool?.cancel();
    this.shapeMode = next.areaType;
    this.setShape(next, true);
    this.layer?.fit();
    this.pasteOpen = false;
    this.pasteError = '';
    this.markDirty();
  }

  onRadiusInput(value: number): void {
    if (this.shape?.areaType !== 'circle' || !Number.isFinite(Number(value))) return;
    this.setShape({ ...this.shape, radiusMeters: Number(value) }, true);
  }

  onCorridorWidthInput(value: number): void {
    const w = Number(value);
    if (!Number.isFinite(w)) return;
    this.corridorWidth = w;
    if (this.shape?.areaType === 'corridor') this.setShape({ ...this.shape, corridorWidthMeters: w }, true);
  }

  clearShape(): void {
    if (!this.shape) return;
    this.setShape(null, true);
    this.startDrawing();
    this.markDirty();
  }

  undo(): void {
    this.flushBurst();
    const prev = this.history.undo(this.shape);
    if (prev === undefined) return;
    this.setShape(prev, false);
    if (prev?.areaType) this.shapeMode = prev.areaType;
    if (!prev) this.startDrawing();
    this.markDirty();
  }

  redo(): void {
    this.flushBurst();
    const next = this.history.redo(this.shape);
    if (next === undefined) return;
    this.setShape(next, false);
    if (next?.areaType) this.shapeMode = next.areaType;
    this.markDirty();
  }

  @HostListener('document:keydown', ['$event'])
  onKey(e: KeyboardEvent): void {
    const target = e.target as HTMLElement | null;
    const typing = !!target && (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA' || target.tagName === 'SELECT' || target.isContentEditable);
    if (typing || !(e.ctrlKey || e.metaKey)) return;
    const key = e.key.toLowerCase();
    if (key === 'z' && !e.shiftKey) {
      e.preventDefault();
      this.undo();
    } else if ((key === 'z' && e.shiftKey) || key === 'y') {
      e.preventDefault();
      this.redo();
    }
  }

  /** Programmatic change (paste, convert, radius box, undo): redraw, and record history unless undoing. */
  private setShape(next: GeofenceShape | null, record: boolean): void {
    this.flushBurst();
    if (record) this.history.push(this.shape);
    this.shape = next ? JSON.parse(JSON.stringify(next)) as GeofenceShape : null;
    this.layer?.setShape(this.shape);
    this.afterShapeChange(true);
  }

  /** A user edit on the map. Drags fire many events; they become one undo step. */
  private onUserShapeChange(next: GeofenceShape): void {
    if (this.burstBefore === undefined) this.burstBefore = this.shape ? JSON.parse(JSON.stringify(this.shape)) as GeofenceShape : null;
    this.shape = next;
    if (this.burstTimer) clearTimeout(this.burstTimer);
    this.burstTimer = setTimeout(() => this.flushBurst(), 400);
    this.afterShapeChange(true);
  }

  private flushBurst(): void {
    if (this.burstTimer) clearTimeout(this.burstTimer);
    this.burstTimer = null;
    if (this.burstBefore !== undefined) {
      this.history.push(this.burstBefore);
      this.burstBefore = undefined;
    }
  }

  private onShapeDrawn(shape: GeofenceShape): void {
    const next = shape.areaType === 'corridor' ? { ...shape, corridorWidthMeters: this.corridorWidth } : shape;
    this.hint = '';
    this.setShape(next, true);
    this.markDirty();
  }

  private afterShapeChange(touched: boolean): void {
    if (this.submitted) this.errors = this.validate();
    this.scheduleOverlap();
    this.scheduleTest();
    if (touched) this.markDirtyLight();
  }

  /** Draft save without re-validating twice. */
  private markDirtyLight(): void {
    if (this.draftTimer) clearTimeout(this.draftTimer);
    this.draftTimer = setTimeout(() => this.flushDraft(), 800);
  }

  private startDrawing(): void {
    if (!this.drawTool) return;
    this.drawTool.start(this.shapeMode);
  }

  // ---- checks while drawing ---------------------------------------------------------------------------------------

  private scheduleOverlap(): void {
    if (this.overlapTimer) clearTimeout(this.overlapTimer);
    this.overlapTimer = setTimeout(() => this.zone.run(() => this.computeOverlap()), 400);
  }

  private computeOverlap(): void {
    this.overlapTimer = null;
    const shape = completeShape(this.shape);
    if (!shape) {
      this.overlap = null;
      return;
    }
    const hits: { name: string; f: number }[] = [];
    for (const fence of this.otherFences) {
      if (!fence.isActive) continue;
      const other = shapeFromApi(fence);
      if (!other) continue;
      const f = overlapFraction(shape, other);
      if (f >= OVERLAP_MIN) hits.push({ name: fence.name, f });
    }
    hits.sort((a, b) => b.f - a.f);
    this.overlap = hits.length ? { name: hits[0].name, pct: Math.max(1, Math.round(hits[0].f * 100)), more: hits.length - 1 } : null;
  }

  private scheduleTest(): void {
    if (this.testTimer) clearTimeout(this.testTimer);
    this.testTimer = setTimeout(() => this.zone.run(() => this.runTest()), 1200);
  }

  private lastTestKey = '';

  private runTest(): void {
    this.testTimer = null;
    const shape = completeShape(this.shape);
    if (!shape) {
      this.test = { loading: false, result: null, error: false };
      this.lastTestKey = '';
      return;
    }
    const geometry = shapeToApi(shape);
    const vehicleIds = this.testVehicleIds();
    const payload = { ...geometry, bufferMeters: Math.round(Number(this.model.bufferMeters) || 0), vehicleIds };
    const key = JSON.stringify(payload);
    if (key === this.lastTestKey) return;
    this.lastTestKey = key;
    this.test = { loading: true, result: this.test.result, error: false };
    this.subs.add(this.api.testShape(payload).subscribe({
      next: r => {
        if (key === this.lastTestKey) this.test = { loading: false, result: r, error: false };
      },
      error: () => {
        if (key === this.lastTestKey) this.test = { loading: false, result: null, error: true };
      }
    }));
  }

  /** Vehicles "test with history" replays: the picked ones, or everyone when the geofence covers all or by group. */
  private testVehicleIds(): number[] | null {
    if (this.model.appliesTo === 'vehicles') return this.model.vehicleIds.length ? [...this.model.vehicleIds] : null;
    if (this.model.appliesTo === 'groups' && this.model.branchIds.length && !this.model.departmentIds.length) {
      const ids = this.vehicles.filter(v => v.branchId != null && this.model.branchIds.includes(v.branchId)).map(v => v.id);
      return ids.length ? ids : null;
    }
    return null;
  }

  get testText(): string {
    const r = this.test.result;
    if (!isShapeComplete(this.shape)) return 'Test with history: draw the shape first';
    if (this.test.loading && !r) return 'Test with history: checking the last 7 days…';
    if (this.test.error) return 'Test with history: not available right now';
    if (!r) return 'Test with history: …';
    const n = `${r.capped ? 'at least ' : ''}${plural(r.entries, 'entry', 'entries')}`;
    return `Test with history: ${n} in the last ${r.days} days${r.vehicles ? ` by ${plural(r.vehicles, 'vehicle')}` : ''}`;
  }

  get statusText(): string {
    if (this.hint) return this.hint;
    if (!this.shape) return 'Pick a shape and draw it on the map.';
    const buffer = this.model.bufferMeters > 0
      ? `light band shows the ${this.model.bufferMeters} m tolerance buffer`
      : 'no tolerance buffer';
    const what = this.shape.areaType === 'corridor' ? 'corridor' : SHAPE_LABELS[this.shape.areaType].toLowerCase();
    return `Editing ${what} — ${buffer}. Other geofences are dashed for reference.`;
  }

  // ---- rules ------------------------------------------------------------------------------------------------------

  toggleDay(bit: number): void {
    this.model.scheduleDays = this.model.scheduleDays & bit ? this.model.scheduleDays & ~bit : this.model.scheduleDays | bit;
    this.markDirty();
  }

  isDayOn(bit: number): boolean {
    return (this.model.scheduleDays & bit) !== 0;
  }

  get daysText(): string {
    return daysLabel(this.model.scheduleDays);
  }

  setSchedule(mode: 'always' | 'schedule'): void {
    this.model.scheduleMode = mode;
    if (mode === 'always') this.model.afterHoursExitCritical = false;
    this.markDirty();
  }

  // ---- applies to -------------------------------------------------------------------------------------------------

  setAppliesTo(value: GeofenceAppliesTo): void {
    this.model.appliesTo = value;
    this.picker = null;
    this.markDirty();
  }

  get coverageText(): string {
    const m = this.model;
    switch (m.appliesTo) {
      case 'all':
        return `${plural(this.vehicles.length, 'vehicle')} covered. New vehicles are included automatically.`;
      case 'vehicles':
        return `${plural(m.vehicleIds.length, 'vehicle')} covered.`;
      case 'groups': {
        const inBranches = this.vehicles.filter(v => v.branchId != null && m.branchIds.includes(v.branchId)).length;
        const dept = m.departmentIds.length ? `, plus the vehicles of ${plural(m.departmentIds.length, 'department')}` : '';
        return `${plural(inBranches, 'vehicle')} in the picked branches${dept}. New vehicles added to these groups are included automatically.`;
      }
      default:
        return `Follows ${plural(m.driverIds.length, 'driver')} in whatever vehicle they drive.`;
    }
  }

  /** Lists outside the chosen kind that will be saved too. */
  get extras(): { list: 'vehicleIds' | 'branchIds' | 'departmentIds' | 'driverIds'; text: string }[] {
    return extraAssignments(this.model).map(list => {
      const n = this.model[list].length;
      const text = list === 'vehicleIds' ? plural(n, 'vehicle')
        : list === 'branchIds' ? plural(n, 'branch', 'branches')
        : list === 'departmentIds' ? plural(n, 'department')
        : plural(n, 'driver');
      return { list, text };
    });
  }

  removeExtra(list: 'vehicleIds' | 'branchIds' | 'departmentIds' | 'driverIds'): void {
    this.model[list] = [];
    this.markDirty();
  }

  /** Tags for the chosen kind, with the list they come from. */
  get scopeTags(): { kind: PickerKind; id: number; label: string }[] {
    const m = this.model;
    if (m.appliesTo === 'vehicles') return m.vehicleIds.map(id => ({ kind: 'vehicles' as const, id, label: this.vehicleLabel(id) }));
    if (m.appliesTo === 'drivers') return m.driverIds.map(id => ({ kind: 'drivers' as const, id, label: this.drivers.find(d => d.id === id)?.name ?? `Driver #${id}` }));
    if (m.appliesTo === 'groups') {
      const count = (branchId: number) => this.vehicles.filter(v => v.branchId === branchId).length;
      return [
        ...m.branchIds.map(id => ({ kind: 'branches' as const, id, label: `${this.branches.find(b => b.id === id)?.name ?? `Branch #${id}`} · ${count(id)}` })),
        ...m.departmentIds.map(id => ({ kind: 'departments' as const, id, label: this.departments.find(d => d.id === id)?.name ?? `Department #${id}` }))
      ];
    }
    return [];
  }

  private vehicleLabel(id: number): string {
    const v = this.vehicles.find(x => x.id === id);
    return v ? v.registrationNumber || v.name : `Vehicle #${id}`;
  }

  openPicker(kind: PickerKind): void {
    this.picker = this.picker === kind ? null : kind;
    this.pickerSearch = '';
  }

  get pickerOptions(): PickOption[] {
    const q = this.pickerSearch.trim().toLowerCase();
    let options: PickOption[] = [];
    switch (this.picker) {
      case 'vehicles':
        options = this.vehicles.map(v => ({ id: v.id, label: v.registrationNumber || v.name, sub: [v.name, v.driverName].filter(Boolean).join(' · ') }));
        break;
      case 'branches':
        options = this.branches.map(b => ({ id: b.id, label: b.name }));
        break;
      case 'departments':
        options = this.departments.map(d => ({ id: d.id, label: d.name }));
        break;
      case 'drivers':
        options = this.drivers.map(d => ({ id: d.id, label: d.name }));
        break;
      case 'roles':
        options = this.recipients.roles.map(r => ({ id: r.id, label: r.name }));
        break;
      case 'users':
        options = this.recipients.users.map(u => ({ id: u.id, label: u.name }));
        break;
    }
    return (q ? options.filter(o => `${o.label} ${o.sub ?? ''}`.toLowerCase().includes(q)) : options).slice(0, 200);
  }

  private listFor(kind: PickerKind): number[] {
    const m = this.model;
    return kind === 'vehicles' ? m.vehicleIds
      : kind === 'branches' ? m.branchIds
      : kind === 'departments' ? m.departmentIds
      : kind === 'drivers' ? m.driverIds
      : kind === 'roles' ? m.recipientRoleIds
      : m.recipientUserIds;
  }

  isPicked(kind: PickerKind, id: number): boolean {
    return this.listFor(kind).includes(id);
  }

  togglePick(kind: PickerKind, id: number): void {
    const list = this.listFor(kind);
    const next = list.includes(id) ? list.filter(x => x !== id) : [...list, id];
    switch (kind) {
      case 'vehicles': this.model.vehicleIds = next; break;
      case 'branches': this.model.branchIds = next; break;
      case 'departments': this.model.departmentIds = next; break;
      case 'drivers': this.model.driverIds = next; break;
      case 'roles': this.model.recipientRoleIds = next; break;
      case 'users': this.model.recipientUserIds = next; break;
    }
    this.markDirty();
  }

  // ---- notify -----------------------------------------------------------------------------------------------------

  isChannelOn(c: GeofenceNotifyChannel): boolean {
    return this.model.notifyChannels.includes(c);
  }

  toggleChannel(c: GeofenceNotifyChannel): void {
    this.model.notifyChannels = this.isChannelOn(c) ? this.model.notifyChannels.filter(x => x !== c) : [...this.model.notifyChannels, c];
    this.markDirty();
  }

  get recipientTags(): { kind: PickerKind; id: number; label: string }[] {
    return [
      ...this.model.recipientRoleIds.map(id => ({ kind: 'roles' as const, id, label: this.recipients.roles.find(r => r.id === id)?.name ?? `Role #${id}` })),
      ...this.model.recipientUserIds.map(id => ({ kind: 'users' as const, id, label: this.recipients.users.find(u => u.id === id)?.name ?? `User #${id}` }))
    ];
  }

  // ---- save -------------------------------------------------------------------------------------------------------

  private validate(): EditorErrors {
    return validateEditor(this.model, this.shape, { restrictedType: this.restrictedType });
  }

  get errorCount(): number {
    return Object.keys(this.errors).length;
  }

  save(): void {
    this.flushBurst();
    this.submitted = true;
    this.errors = this.validate();
    if (this.errorCount) {
      this.toast.error('Some fields need attention before saving.');
      setTimeout(() => document.querySelector('.ge [aria-invalid="true"], .ge .ge-error')?.scrollIntoView({ behavior: 'smooth', block: 'center' }));
      return;
    }
    const payload = buildSavePayload(this.model, this.shape!, this.keepChannels);
    this.saving = true;
    const request = this.id != null ? this.api.update(this.id, payload) : this.api.create(payload);
    this.subs.add(request.subscribe({
      next: id => {
        this.saving = false;
        clearDraft(this.draftStorageKey);
        this.snapshot();
        this.toast.success(this.isEdit ? 'Geofence saved.' : 'Geofence created.');
        void this.router.navigate(['/gps-tracking/geofences'], { queryParams: { selected: this.id ?? Number(id) } });
      },
      error: err => {
        this.saving = false;
        this.toast.error(apiErrorMessage(err, 'The geofence could not be saved.'));
      }
    }));
  }

  cancel(): void {
    const leave = () => {
      clearDraft(this.draftStorageKey);
      this.snapshot();
      void this.router.navigate(['/gps-tracking/geofences'], { queryParams: this.id != null ? { selected: this.id } : {} });
    };
    if (!this.dirty) {
      leave();
      return;
    }
    const data: ConfirmDialogData = {
      title: 'Discard changes?',
      message: 'Your changes and the saved draft will be thrown away.',
      confirmText: 'Discard',
      cancelText: 'Keep editing',
      confirmColor: 'warn'
    };
    this.subs.add(this.dialog.open(ConfirmDialogComponent, { data, width: '420px' }).afterClosed().subscribe(ok => {
      if (ok) leave();
    }));
  }

  // ---- map --------------------------------------------------------------------------------------------------------

  private async initMap(): Promise<void> {
    if (!this.mapEl?.nativeElement || this.map) return;
    if (!this.mapsLoader.isConfigured) {
      this.mapError = 'Google Maps is not set up for this site. Paste coordinates in step 2 to set the shape.';
      return;
    }
    this.subs.add(this.mapsLoader.authFailures$.subscribe(msg => this.zone.run(() => (this.mapError = msg))));
    try {
      const ok = await this.mapsLoader.load();
      if (!ok) throw new Error(this.mapsLoader.failureMessage || 'Google Maps failed to load.');
      await this.mapsLoader.importLibrary('maps');
      if (this.mapsLoader.authFailed) throw new Error(this.mapsLoader.failureMessage || 'Google Maps rejected the key.');
    } catch (err) {
      this.zone.run(() => (this.mapError = err instanceof Error ? err.message : 'Google Maps failed to load.'));
      return;
    }

    const map = new google.maps.Map(this.mapEl.nativeElement, {
      center: { lat: 31.52, lng: 74.35 },
      zoom: 12,
      renderingType: google.maps.RenderingType.RASTER,
      mapTypeControl: false,
      streetViewControl: false,
      fullscreenControl: false,
      zoomControl: true,
      zoomControlOptions: { position: google.maps.ControlPosition.RIGHT_CENTER },
      gestureHandling: 'greedy',
      clickableIcons: false
    });
    this.map = map;
    this.layer = new EditableShapeLayer({
      map,
      onChange: shape => this.zone.run(() => this.onUserShapeChange(shape)),
      onMeasure: (text, at) => this.zone.run(() => this.showMeasure(text, at))
    });
    this.drawTool = new GeofenceDrawTool({
      map,
      color: () => this.model.color,
      corridorWidth: () => this.corridorWidth,
      onHint: text => this.zone.run(() => (this.hint = text)),
      onComplete: shape => this.zone.run(() => this.onShapeDrawn(shape))
    });
    if (typeof ResizeObserver !== 'undefined') {
      this.resizeObserver = new ResizeObserver(() => triggerGmapResize(map));
      this.resizeObserver.observe(this.mapEl.nativeElement);
    }
    this.zone.run(() => {
      this.mapReady = true;
      this.layer!.setStyle(this.model.color, this.model.bufferMeters);
      this.layer!.setShape(this.shape);
      this.renderReferences();
      if (this.shape) this.layer!.fit();
      else if (!this.loading) this.startDrawing();
    });
    void this.initPlaces();
  }

  private renderReferences(): void {
    if (!this.map) return;
    this.references.forEach(r => r.remove());
    this.references = [];
    for (const fence of this.otherFences) {
      const shape = shapeFromApi(fence);
      if (!shape) continue;
      this.references.push(renderFence(this.map, shape, { color: fence.color || '#2563eb', restricted: fence.isRestricted, active: fence.isActive, reference: true }));
    }
    if (!this.shape && !this.isEdit && this.references.length && !this.referencesFitted) {
      this.referencesFitted = true;
      const bounds = new google.maps.LatLngBounds();
      this.references.forEach(r => {
        const b = r.bounds();
        if (b) bounds.union(b);
      });
      this.map.fitBounds(bounds, 48);
    }
  }

  private showMeasure(text: string, at: LatLng): void {
    if (!this.map) return;
    if (this.measureLabel) {
      this.measureLabel.update(text, '#0f172a');
      this.measureLabel.setPosition(at);
    } else {
      this.measureLabel = createHtmlLabel(this.map, at, text, '#0f172a', undefined, 'tip');
    }
    if (this.measureTimer) clearTimeout(this.measureTimer);
    this.measureTimer = setTimeout(() => {
      this.measureLabel?.setMap(null);
      this.measureLabel = null;
    }, 2500);
  }

  /** Google Places on the address field. The map search box uses Geoapify (placeResults$). */
  private async initPlaces(): Promise<void> {
    try {
      const ok = await this.mapsLoader.load();
      if (!ok) throw new Error('maps');
      await this.mapsLoader.importLibrary('places');
      const input = this.addressInput?.nativeElement;
      if (!input) return;
      this.placeHandles.push(await attachSheikhGoPlacesAutocomplete({
        input,
        ngZone: this.zone,
        mapsLoader: this.mapsLoader,
        onSelect: place => this.zone.run(() => this.onPlace(place, true))
      }));
    } catch {
      // Without Google Places the address field still takes a typed address.
    }
  }

  /** Picking a place pans there; with no shape yet it also drops one. The address field also fills the address. */
  onPlace(place: SheikhGoPlaceSelection, fromAddress: boolean): void {
    if (!Number.isFinite(place.lat) || !Number.isFinite(place.lng)) return;
    if (fromAddress) {
      this.model.address = (place.address || place.name || '').slice(0, 300);
      if (!this.model.name.trim() && place.name) this.model.name = place.name.slice(0, 100);
    }
    this.goTo({ lat: place.lat, lng: place.lng });
    this.markDirty();
  }

  /** A Geoapify place picked in the map search box. */
  onPlaceSuggestion(place: PlaceSuggestion): void {
    if (!Number.isFinite(place.lat) || !Number.isFinite(place.lng)) return;
    this.goTo({ lat: place.lat, lng: place.lng }, placeBounds(place));
    this.markDirty();
  }

  /** Enter in the map search: a pasted "lat, lng" goes straight there. */
  onSearchEnter(text: string): void {
    const p = parseLatLng(text);
    if (p) this.goTo(p);
  }

  /**
   * Shows a place. A town or a district (bounds) fills the view; anything else is centred at zoom 16 or closer.
   * With no shape yet it also drops one, and the view then closes in on it unless a whole area is in view.
   */
  private goTo(point: LatLng, bounds: LatLngBounds | null = null): void {
    if (bounds && this.map) {
      this.map.fitBounds(new google.maps.LatLngBounds({ lat: bounds.minLat, lng: bounds.minLng }, { lat: bounds.maxLat, lng: bounds.maxLng }), 48);
    } else {
      this.map?.panTo(point);
      this.map?.setZoom(Math.max(this.map.getZoom() ?? 15, 16));
    }
    if (!this.shape) {
      const dropped = shapeAt(point, this.shapeMode, this.shapeMode === 'circle' ? DEFAULT_DRAWN_RADIUS_M : undefined);
      if (dropped) {
        this.drawTool?.cancel();
        this.setShape(dropped, true);
        if (!bounds) this.layer?.fit();
        this.hint = '';
      }
    }
  }

  private mapCenter(): LatLng | null {
    const c = this.map?.getCenter();
    return c ? { lat: c.lat(), lng: c.lng() } : null;
  }

  toggleSatellite(): void {
    this.satellite = !this.satellite;
    this.map?.setMapTypeId(this.satellite ? 'hybrid' : 'roadmap');
  }

  jumpToMap(): void {
    this.mapEl?.nativeElement.closest('.ge-map')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  trackTag = (_: number, t: { kind: string; id: number }) => `${t.kind}:${t.id}`;
}
