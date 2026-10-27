import {
  Component,
  ElementRef,
  EventEmitter,
  HostBinding,
  HostListener,
  Input,
  OnDestroy,
  OnInit,
  Output,
  ViewChild
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Subject, Subscription, debounceTime, distinctUntilChanged, of, switchMap, catchError, finalize } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';
import { PlatformService } from '../../../core/services/platform.service';
import { PlatformTenantContextService } from '../../../core/services/platform-tenant-context.service';
import { VehicleService } from '../../../core/services/vehicle.service';
import { TenantConfigService } from '../../../core/services/tenant-config.service';
import { Tenant } from '../../../core/models/platform.model';
import { VehicleListItem, VehicleStatus, VehicleStatusLabels } from '../../../core/models/vehicle.model';
import { AiAssistantScopeService } from '../services/ai-assistant-scope.service';
import { AI_ASSISTANT_UI } from '../shared/ai-assistant-ui.constants';

@Component({
  selector: 'app-ai-assistant-scope',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatIconModule,
    MatButtonModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './ai-assistant-scope.component.html',
  styleUrls: ['./ai-assistant-scope.component.scss']
})
export class AiAssistantScopeComponent implements OnInit, OnDestroy {
  readonly aiUi = AI_ASSISTANT_UI;

  /** Compact drawer vs full-page toolbar density. */
  @Input() compact = false;
  /**
   * When true (default for fleet inventory), hide entirely for non–SuperAdmin users
   * so regular company users do not see a cross-company picker or redundant label.
   */
  @Input() hideForRegularUsers = true;
  @Output() scopeReadyChange = new EventEmitter<boolean>();
  @Output() companyChanged = new EventEmitter<Tenant | null>();
  @Output() vehicleSelected = new EventEmitter<VehicleListItem | null>();

  @ViewChild('searchInput') searchInput?: ElementRef<HTMLInputElement>;
  @ViewChild('resultsList') resultsList?: ElementRef<HTMLElement>;

  isSuperAdmin = false;
  companyLabel = '';

  companySearch = '';
  companies: Tenant[] = [];
  filteredCompanies: Tenant[] = [];
  companiesLoading = false;
  companiesError: string | null = null;
  dropdownOpen = false;
  activeResultIndex = -1;

  selectedCompany: Tenant | null = null;
  scopeNotice: string | null = null;

  vehicles: VehicleListItem[] = [];
  filteredVehicles: VehicleListItem[] = [];
  vehiclesLoading = false;
  vehiclesError: string | null = null;
  vehicleFilter = '';
  selectedVehicleId: number | null = null;

  private readonly search$ = new Subject<string>();
  private subs = new Subscription();
  private noticeTimer?: ReturnType<typeof setTimeout>;

  constructor(
    private auth: AuthService,
    private platform: PlatformService,
    private tenantContext: PlatformTenantContextService,
    private vehiclesApi: VehicleService,
    private tenantConfig: TenantConfigService,
    private scope: AiAssistantScopeService
  ) {}

  /** Host renders nothing for regular users when hideForRegularUsers is on. */
  get visible(): boolean {
    return this.isSuperAdmin || !this.hideForRegularUsers;
  }

  @HostBinding('hidden')
  get hostHidden(): boolean {
    return !this.visible;
  }

  @HostBinding('attr.aria-hidden')
  get hostAriaHidden(): string | null {
    return this.visible ? null : 'true';
  }

  ngOnInit(): void {
    this.isSuperAdmin = this.detectSuperAdmin();

    if (!this.visible) {
      this.emitReady();
      return;
    }

    this.subs.add(
      this.scope.changes$.subscribe(s => {
        this.selectedCompany = s.company;
        this.selectedVehicleId = s.vehicleId;
        this.scopeNotice = s.scopeNotice;
        if (s.scopeNotice) {
          clearTimeout(this.noticeTimer);
          this.noticeTimer = setTimeout(() => this.scope.clearScopeNotice(), 4500);
        }
        this.emitReady();
      })
    );

    if (this.isSuperAdmin) {
      this.subs.add(
        this.search$.pipe(
          debounceTime(280),
          distinctUntilChanged(),
          switchMap(q => {
            if (!this.companies.length && !this.companiesLoading) {
              this.loadCompanies();
            }
            return of(q);
          })
        ).subscribe(q => this.applyCompanyFilter(q))
      );
      this.loadCompanies();
    } else {
      this.loadCompanyLabel();
      this.emitReady();
    }
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
    clearTimeout(this.noticeTimer);
  }

  get activeVehicleCount(): number {
    return this.vehicles.filter(v =>
      v.status === VehicleStatus.Available || v.status === VehicleStatus.OnTrip
    ).length;
  }

  get canAskFleetQuestions(): boolean {
    return !this.isSuperAdmin || !!this.selectedCompany;
  }

  onSearchInput(value: string): void {
    this.companySearch = value;
    this.dropdownOpen = true;
    this.activeResultIndex = -1;
    this.search$.next(value);
  }

  openDropdown(): void {
    if (!this.isSuperAdmin) return;
    this.dropdownOpen = true;
    if (!this.companies.length) this.loadCompanies();
    this.applyCompanyFilter(this.companySearch);
  }

  closeDropdown(): void {
    this.dropdownOpen = false;
    this.activeResultIndex = -1;
  }

  selectCompany(company: Tenant): void {
    this.scope.selectCompany(company);
    this.tenantContext.selectTenant(company);
    this.companySearch = company.name;
    this.closeDropdown();
    this.companyChanged.emit(company);
    this.loadVehicles(company.id);
  }

  clearCompany(): void {
    this.scope.selectCompany(null);
    this.tenantContext.clear();
    this.companySearch = '';
    this.vehicles = [];
    this.filteredVehicles = [];
    this.vehiclesError = null;
    this.companyChanged.emit(null);
    this.vehicleSelected.emit(null);
    this.emitReady();
  }

  selectAllVehicles(): void {
    this.scope.selectVehicle(null);
    this.vehicleSelected.emit(null);
  }

  selectVehicle(v: VehicleListItem): void {
    this.scope.selectVehicle(v);
    this.vehicleSelected.emit(v);
  }

  onVehicleFilter(value: string): void {
    this.vehicleFilter = value;
    this.applyVehicleFilter();
  }

  retryCompanies(): void {
    this.loadCompanies();
  }

  retryVehicles(): void {
    if (this.selectedCompany) {
      this.loadVehicles(this.selectedCompany.id);
    }
  }

  statusLabel(v: VehicleListItem): string {
    if (v.status === VehicleStatus.Available || v.status === VehicleStatus.OnTrip) return 'Active';
    if (v.status === VehicleStatus.Maintenance) return 'Maintenance';
    return VehicleStatusLabels[v.status] || 'Inactive';
  }

  statusTone(v: VehicleListItem): 'active' | 'warn' | 'inactive' {
    if (v.status === VehicleStatus.Available || v.status === VehicleStatus.OnTrip) return 'active';
    if (v.status === VehicleStatus.Maintenance) return 'warn';
    return 'inactive';
  }

  trackCompany(_i: number, c: Tenant): number { return c.id; }
  trackVehicle(_i: number, v: VehicleListItem): number { return v.id; }

  onSearchKeydown(event: KeyboardEvent): void {
    if (!this.dropdownOpen && (event.key === 'ArrowDown' || event.key === 'Enter')) {
      this.openDropdown();
    }
    const max = this.filteredCompanies.length - 1;
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      this.activeResultIndex = Math.min(max, this.activeResultIndex + 1);
      this.scrollActiveIntoView();
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      this.activeResultIndex = Math.max(0, this.activeResultIndex - 1);
      this.scrollActiveIntoView();
    } else if (event.key === 'Enter' && this.activeResultIndex >= 0) {
      event.preventDefault();
      const c = this.filteredCompanies[this.activeResultIndex];
      if (c) this.selectCompany(c);
    } else if (event.key === 'Escape') {
      this.closeDropdown();
    }
  }

  @HostListener('document:click', ['$event'])
  onDocClick(ev: MouseEvent): void {
    const host = (ev.target as HTMLElement)?.closest?.('app-ai-assistant-scope');
    if (!host) this.closeDropdown();
  }

  private detectSuperAdmin(): boolean {
    const roles = this.auth.getCurrentUser()?.roles ?? [];
    return roles.some(r => {
      const code = (r || '').toUpperCase().replace(/\s+/g, '_');
      return code === 'SUPER_ADMIN' || code === 'SUPERADMIN';
    });
  }

  private loadCompanyLabel(): void {
    this.tenantConfig.loadBranding().subscribe(b => {
      this.companyLabel = b?.name?.trim() || 'Your company';
    });
  }

  private loadCompanies(): void {
    this.companiesLoading = true;
    this.companiesError = null;
    this.platform.getTenants().pipe(
      catchError(() => {
        this.companiesError = "Couldn't load companies. Try again.";
        return of([] as Tenant[]);
      }),
      finalize(() => { this.companiesLoading = false; })
    ).subscribe(list => {
      this.companies = (list ?? []).filter(t => t.isActive !== false);
      this.applyCompanyFilter(this.companySearch);
      const currentId =
        this.scope.snapshot.company?.id
        ?? this.tenantContext.currentTenantId
        ?? null;
      if (currentId) {
        const match = this.companies.find(c => c.id === currentId);
        if (match) {
          if (!this.scope.snapshot.company || this.scope.snapshot.company.id !== match.id) {
            this.scope.selectCompany(match);
            this.tenantContext.selectTenant(match);
          }
          this.selectedCompany = match;
          this.companySearch = match.name;
          this.loadVehicles(match.id);
        }
      }
    });
  }

  private applyCompanyFilter(q: string): void {
    const needle = (q || '').trim().toLowerCase();
    this.filteredCompanies = !needle
      ? this.companies.slice(0, 40)
      : this.companies
          .filter(c =>
            c.name.toLowerCase().includes(needle)
            || (c.slug || '').toLowerCase().includes(needle)
            || (c.code || '').toLowerCase().includes(needle)
          )
          .slice(0, 40);
  }

  private loadVehicles(tenantId: number): void {
    this.vehiclesLoading = true;
    this.vehiclesError = null;
    this.vehicles = [];
    this.filteredVehicles = [];
    // Backend authorizes SuperAdmin cross-tenant via tenantId query; do not invent data.
    this.vehiclesApi.getAll(1, 100, false, tenantId).pipe(
      catchError(() => {
        this.vehiclesError = "Couldn't load vehicles. Try again.";
        return of({ items: [] as VehicleListItem[], totalCount: 0, page: 1, pageSize: 100, totalPages: 0 });
      }),
      finalize(() => { this.vehiclesLoading = false; })
    ).subscribe(page => {
      this.vehicles = page.items ?? [];
      this.applyVehicleFilter();
    });
  }

  private applyVehicleFilter(): void {
    const needle = this.vehicleFilter.trim().toLowerCase();
    this.filteredVehicles = !needle
      ? this.vehicles
      : this.vehicles.filter(v =>
          v.registrationNumber.toLowerCase().includes(needle)
          || (v.name || '').toLowerCase().includes(needle)
          || (v.vehicleCode || '').toLowerCase().includes(needle)
        );
  }

  private emitReady(): void {
    this.scopeReadyChange.emit(this.canAskFleetQuestions);
  }

  private scrollActiveIntoView(): void {
    const list = this.resultsList?.nativeElement;
    if (!list || this.activeResultIndex < 0) return;
    const el = list.children[this.activeResultIndex] as HTMLElement | undefined;
    el?.scrollIntoView({ block: 'nearest' });
  }
}
