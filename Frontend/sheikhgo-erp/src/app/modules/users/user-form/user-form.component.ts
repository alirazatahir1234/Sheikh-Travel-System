import { Component, ElementRef, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { forkJoin, of, Subject, switchMap, takeUntil } from 'rxjs';
import { catchError, debounceTime, distinctUntilChanged, filter, map } from 'rxjs/operators';
import { StepperSelectionEvent } from '@angular/cdk/stepper';
import { MatStepper } from '@angular/material/stepper';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { UserService } from '../../../core/services/user.service';
import { PlatformService } from '../../../core/services/platform.service';
import { AuthService } from '../../../core/services/auth.service';
import {
  User,
  UserRole,
  CreateUserDto,
  UpdateUserDto,
  parseUserRole,
  USER_LIFECYCLE_STATUSES,
  EMPLOYEE_TYPES,
  AssignedRole
} from '../../../core/models/user.model';
import { TenantConfigService } from '../../../core/services/tenant-config.service';
import {
  Branch,
  Department,
  RoleSummary,
  EffectivePermission,
  WorkspaceDefinition,
  DashboardDefinition,
  CompanyDataScope,
  AuditEventListItem
} from '../../../core/models/platform.model';
import {
  PRIMARY_ROLE_CATALOG,
  PrimaryRoleDefinition,
  getPrimaryRoleById,
  inferPrimaryRoleId,
  isSystemRoleCode,
  permissionCountForPrimary,
  permissionReadWriteSplit,
  resolvePlatformRoleId,
  defaultEmployeeTypeForPrimary,
  EMPLOYEE_TYPE_DESCRIPTIONS
} from '../user-primary-roles';

const DRAFT_KEY = 'sheikhgo-user-form-draft';
const STEP_FIELD_KEYS = [
  ['fullName', 'email', 'phoneNational', 'password', 'primaryRoleId', 'status'],
  [],
  [],
  [],
  []
] as const;

@Component({
  standalone: false,
  selector: 'app-user-form',
  templateUrl: './user-form.component.html',
  styleUrls: ['./user-form.component.scss']
})
export class UserFormComponent implements OnInit, OnDestroy {
  @ViewChild('stepper') stepper!: MatStepper;
  @ViewChild('avatarFileInput') avatarFileInput?: ElementRef<HTMLInputElement>;

  form: FormGroup;
  loading = false;
  submitting = false;
  isEdit = false;
  userId: number | null = null;
  hidePassword = true;
  companyName = '';
  companyLockedLabel = '';
  loadedUser: User | null = null;
  branches: Branch[] = [];
  departments: Department[] = [];
  private orgLoadSeq = 0;
  assignableRoles: RoleSummary[] = [];
  workspaces: WorkspaceDefinition[] = [];
  dashboards: DashboardDefinition[] = [];
  selectedRoleIds = new Set<number>();
  effectivePermissions: EffectivePermission[] = [];
  dataScope: CompanyDataScope | null = null;
  recentActivity: AuditEventListItem[] = [];
  currentStep = 0;
  avatarPreviewDataUrl: string | null = null;
  avatarDragOver = false;
  showAdvancedAvatar = false;
  emailCheckStatus: 'idle' | 'checking' | 'available' | 'taken' | 'invalid' = 'idle';
  originalEmail = '';

  readonly wizardSteps = [
    { label: 'General' },
    { label: 'Organization' },
    { label: 'Access' },
    { label: 'Preferences' },
    { label: 'Review' }
  ];
  readonly stepCount = 5;
  readonly primaryRoles = PRIMARY_ROLE_CATALOG;
  readonly lifecycleStatuses = USER_LIFECYCLE_STATUSES;
  readonly employeeTypes = EMPLOYEE_TYPES;
  readonly employeeTypeDescriptions = EMPLOYEE_TYPE_DESCRIPTIONS;
  readonly suggestEmployeeType = defaultEmployeeTypeForPrimary;
  readonly themes = ['light', 'dark', 'system'];
  readonly languages = ['en', 'ar'];
  readonly phoneCountries = [
    { flag: '🇦🇪', dial: '+971', label: 'UAE' },
    { flag: '🇵🇰', dial: '+92', label: 'Pakistan' },
    { flag: '🇸🇦', dial: '+966', label: 'Saudi Arabia' },
    { flag: '🇺🇸', dial: '+1', label: 'US' }
  ];

  private readonly destroy$ = new Subject<void>();

  constructor(
    private fb: FormBuilder,
    private userService: UserService,
    private platform: PlatformService,
    private auth: AuthService,
    private tenantConfig: TenantConfigService,
    private route: ActivatedRoute,
    private router: Router,
    private toast: UiToastService
  ) {
    this.form = this.fb.group({
      fullName: ['', [Validators.required, Validators.maxLength(100)]],
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(6)]],
      phoneCountry: ['+971'],
      phoneNational: ['', [Validators.required]],
      phone: ['', [Validators.required]],
      primaryRoleId: ['DISPATCHER', Validators.required],
      role: [UserRole.Dispatcher, Validators.required],
      isActive: [true],
      status: ['Active'],
      companyId: [null as number | null, Validators.required],
      branchId: [null as number | null],
      departmentId: [null as number | null],
      jobTitle: [''],
      employeeCode: [''],
      employeeType: [null as string | null],
      reportingManager: [''],
      driverLicense: [''],
      licenseExpiry: [''],
      assignedVehicleNote: [''],
      gpsDeviceNote: [''],
      shiftPattern: [''],
      costCenter: [''],
      fleetGroupNote: [''],
      defaultWorkspaceKey: [''],
      defaultDashboardKey: [''],
      homeRoute: [''],
      autoWorkspace: [true],
      autoHomeRoute: [true],
      timeZone: [''],
      language: ['en'],
      theme: ['system'],
      avatarUrl: [''],
      requirePasswordChange: [true],
      sendWelcomeEmail: [true],
      notifyManager: [false],
      enableMfa: [false]
    });
  }

  ngOnInit(): void {
    this.form.get('primaryRoleId')?.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(id => this.onPrimaryRoleChanged(id));

    this.form.get('phoneCountry')?.valueChanges.pipe(takeUntil(this.destroy$)).subscribe(() => this.syncPhoneField());
    this.form.get('phoneNational')?.valueChanges.pipe(takeUntil(this.destroy$)).subscribe(() => this.syncPhoneField());

    this.form.get('email')?.valueChanges.pipe(
      takeUntil(this.destroy$),
      debounceTime(450),
      distinctUntilChanged(),
      filter(() => !this.isEdit || this.form.value.email !== this.originalEmail)
    ).subscribe(() => this.checkEmailAvailability());

    this.form.get('companyId')?.valueChanges.pipe(
      takeUntil(this.destroy$),
      distinctUntilChanged()
    ).subscribe((companyId: number | string | null) => {
      const id = companyId == null || companyId === '' ? null : Number(companyId);
      if (id == null || Number.isNaN(id) || id <= 0) return;
      this.onCompanyChanged(id);
    });

    this.route.paramMap.subscribe(params => {
      const id = params.get('id');
      this.loading = true;

      forkJoin({
        workspaces: this.platform.getWorkspaceCatalog().pipe(catchError(() => of([] as WorkspaceDefinition[]))),
        dashboards: this.platform.getDashboardCatalog(true).pipe(catchError(() => of([] as DashboardDefinition[]))),
        branding: this.tenantConfig.loadBranding().pipe(catchError(() => of(null))),
        user: id ? this.userService.getById(+id) : of(null as User | null),
        assigned: id
          ? this.userService.getUserRoles(+id).pipe(catchError(() => of([] as AssignedRole[])))
          : of([] as AssignedRole[]),
        effective: id
          ? this.userService.getUserPermissions(+id).pipe(catchError(() => of([] as EffectivePermission[])))
          : of([] as EffectivePermission[]),
        dataScope: id
          ? this.userService.getUserDataScope(+id).pipe(catchError(() => of(null as CompanyDataScope | null)))
          : of(null as CompanyDataScope | null),
        recentActivity: id
          ? this.platform.getRecentAuditEvents(null, +id, 12).pipe(catchError(() => of([] as AuditEventListItem[])))
          : of([] as AuditEventListItem[])
      }).subscribe({
        next: ({ workspaces, dashboards, branding, user, assigned, effective, dataScope, recentActivity }) => {
          this.workspaces = (workspaces ?? []).filter(w => w.isActive && w.visible);
          this.dashboards = (dashboards ?? []).filter(d => d.isActive && d.visible);
          this.selectedRoleIds = new Set(assigned.map(r => r.roleId));
          this.effectivePermissions = effective;
          this.dataScope = dataScope;
          this.recentActivity = recentActivity ?? [];

          const authTenantId = this.auth.getCurrentUser()?.tenantId ?? null;
          let companyId: number | null = null;

          if (user) {
            this.isEdit = true;
            this.userId = user.id;
            this.loadedUser = user;
            this.companyName = user.companyName || branding?.name || '';
            this.companyLockedLabel = this.companyName;
            this.originalEmail = user.email;
            this.form.get('password')?.clearValidators();
            this.form.get('password')?.updateValueAndValidity();
            if (user.assignedRoles?.length) {
              this.selectedRoleIds = new Set(user.assignedRoles.map(r => r.roleId));
            }
            const { country, national } = this.splitPhone(user.phone);
            const primaryId = inferPrimaryRoleId(parseUserRole(user.role), assigned);
            companyId = user.companyId ?? authTenantId ?? branding?.id ?? null;
            this.form.patchValue({
              fullName: user.fullName,
              email: user.email,
              phoneCountry: country,
              phoneNational: national,
              phone: user.phone,
              primaryRoleId: primaryId,
              role: parseUserRole(user.role),
              isActive: user.isActive,
              status: user.status || (user.isActive ? 'Active' : 'Inactive'),
              companyId,
              branchId: user.branchId ?? null,
              departmentId: user.departmentId ?? null,
              jobTitle: user.jobTitle || '',
              employeeCode: user.employeeCode || '',
              employeeType: user.employeeType || null,
              defaultWorkspaceKey: user.defaultWorkspaceKey || '',
              defaultDashboardKey: user.defaultDashboardKey || '',
              homeRoute: user.homeRoute || '',
              autoWorkspace: false,
              autoHomeRoute: false,
              timeZone: user.timeZone || '',
              language: user.language || 'en',
              theme: user.theme || 'system',
              avatarUrl: user.avatarUrl || ''
            }, { emitEvent: false });
            this.syncPrimaryPlatformRole(false);
          } else {
            companyId = authTenantId ?? branding?.id ?? null;
            this.companyName = branding?.name || '';
            this.companyLockedLabel = this.companyName;
            this.form.patchValue({ companyId }, { emitEvent: false });
            this.tryRestoreDraft();
            companyId = (this.form.getRawValue().companyId as number | null) ?? companyId;
            this.onPrimaryRoleChanged(this.form.value.primaryRoleId, true);
          }

          if (!this.canAssignSuperAdmin) {
            this.form.get('companyId')?.disable({ emitEvent: false });
          } else {
            this.form.get('companyId')?.enable({ emitEvent: false });
          }

          if (companyId != null && companyId > 0) {
            this.reloadOrgForCompany(companyId, { clearSelection: false, preserveRoles: this.isEdit });
          } else {
            this.branches = [];
            this.departments = [];
            this.assignableRoles = [];
          }

          this.loading = false;
        },
        error: () => {
          this.loading = false;
          this.toast.error('Failed to load user form.');
          void this.router.navigate(['/users']);
        }
      });
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  get progressPercent(): number {
    return Math.round(((this.currentStep + 1) / this.stepCount) * 100);
  }

  get isLastStep(): boolean {
    return this.currentStep === this.stepCount - 1;
  }

  get canAssignSuperAdmin(): boolean {
    const user = this.auth.getCurrentUser();
    return user?.roles?.some(r => r.toUpperCase() === 'SUPER_ADMIN') ?? false;
  }

  get summaryCompanyLabel(): string {
    if (this.companyLockedLabel && !this.canAssignSuperAdmin) return this.companyLockedLabel;
    if (this.companyName) return this.companyName;
    const id = this.form.getRawValue().companyId as number | null;
    return id != null ? `Company #${id}` : '—';
  }

  private onCompanyChanged(companyId: number): void {
    this.form.patchValue({ branchId: null, departmentId: null }, { emitEvent: false });
    this.platform.getTenantById(companyId).pipe(
      catchError(() => of(null)),
      takeUntil(this.destroy$)
    ).subscribe(t => {
      if (t?.name) {
        this.companyName = t.name;
        this.companyLockedLabel = t.name;
      }
    });
    this.reloadOrgForCompany(companyId, { clearSelection: true, preserveRoles: false });
  }

  private reloadOrgForCompany(
    companyId: number,
    opts: { clearSelection: boolean; preserveRoles: boolean }
  ): void {
    const seq = ++this.orgLoadSeq;
    const authTenantId = this.auth.getCurrentUser()?.tenantId ?? null;
    const sameTenant = authTenantId != null && companyId === authTenantId;

    // Same-tenant (and Company Admin): use BranchesView / DepartmentsView endpoints.
    // Cross-tenant Super Admin: use tenant-scoped org APIs (TenantsView), with fallback toast.
    const branches$ = (!sameTenant && this.canAssignSuperAdmin)
      ? this.platform.getBranchesForTenant(companyId).pipe(
          map(rows => Array.isArray(rows) ? rows : []),
          catchError(() => {
            this.toast.error('Could not load branches for the selected company.');
            return of([] as Branch[]);
          })
        )
      : this.platform.getBranches().pipe(
          map(rows => Array.isArray(rows) ? rows : []),
          catchError(() => of([] as Branch[]))
        );

    const departments$ = (!sameTenant && this.canAssignSuperAdmin)
      ? this.platform.getDepartmentsForTenant(companyId).pipe(
          map(rows => Array.isArray(rows) ? rows : []),
          catchError(() => {
            this.toast.error('Could not load departments for the selected company.');
            return of([] as Department[]);
          })
        )
      : this.platform.getDepartments().pipe(
          map(rows => Array.isArray(rows) ? rows : []),
          catchError(() => of([] as Department[]))
        );

    const roles$ = this.platform
      .getCompanyRoles((!sameTenant && this.canAssignSuperAdmin) ? companyId : null)
      .pipe(
        map(rows => Array.isArray(rows) ? rows : []),
        catchError(() => of([] as RoleSummary[]))
      );

    forkJoin({
      branches: branches$,
      departments: departments$,
      roles: roles$
    }).pipe(takeUntil(this.destroy$)).subscribe(({ branches, departments, roles }) => {
      if (seq !== this.orgLoadSeq) return;
      this.branches = branches;
      this.departments = departments;
      this.assignableRoles = roles;

      if (opts.clearSelection) {
        const validRoleIds = new Set(roles.map(r => r.id));
        this.selectedRoleIds = new Set([...this.selectedRoleIds].filter(id => validRoleIds.has(id)));
        this.syncPrimaryPlatformRole(true);
      } else {
        this.syncPrimaryPlatformRole(false);
      }
    });
  }

  get visibleAssignableRoles(): RoleSummary[] {
    return this.assignableRoles
      .filter(r => r.isActive !== false && r.visible !== false)
      .filter(r => {
        if (r.code.toUpperCase() === 'SUPER_ADMIN' && !this.canAssignSuperAdmin) {
          return this.selectedRoleIds.has(r.id);
        }
        return true;
      })
      .sort((a, b) => (a.sortOrder ?? 500) - (b.sortOrder ?? 500));
  }

  get systemAccessRoles(): RoleSummary[] {
    return this.visibleAssignableRoles.filter(r => r.isSystem || isSystemRoleCode(r.code));
  }

  get customAccessRoles(): RoleSummary[] {
    return this.visibleAssignableRoles.filter(r => !r.isSystem && !isSystemRoleCode(r.code));
  }

  get primaryRoleDef(): PrimaryRoleDefinition | undefined {
    return getPrimaryRoleById(this.form.value.primaryRoleId);
  }

  get primaryPermissionCount(): number {
    const def = this.primaryRoleDef;
    if (!def) return 0;
    return permissionCountForPrimary(def, this.assignableRoles);
  }

  get primaryPermissionSplit(): { read: number; write: number } {
    return permissionReadWriteSplit(this.primaryPermissionCount);
  }

  get passwordStrength(): 'weak' | 'medium' | 'strong' | '' {
    const pwd = (this.form.value.password as string) || '';
    if (!pwd || this.isEdit) return '';
    let score = 0;
    if (pwd.length >= 8) score++;
    if (pwd.length >= 12) score++;
    if (/[A-Z]/.test(pwd) && /[a-z]/.test(pwd)) score++;
    if (/\d/.test(pwd)) score++;
    if (/[^A-Za-z0-9]/.test(pwd)) score++;
    if (score <= 2) return 'weak';
    if (score <= 4) return 'medium';
    return 'strong';
  }

  get passwordStrengthPercent(): number {
    if (this.passwordStrength === 'weak') return 33;
    if (this.passwordStrength === 'medium') return 66;
    if (this.passwordStrength === 'strong') return 100;
    return 0;
  }

  get userInitials(): string {
    const name = (this.form.value.fullName as string)?.trim() || '';
    if (!name) return '?';
    const parts = name.split(/\s+/).filter(Boolean);
    if (parts.length === 1) return parts[0].charAt(0).toUpperCase();
    return (parts[0].charAt(0) + parts[parts.length - 1].charAt(0)).toUpperCase();
  }

  get avatarDisplayUrl(): string | null {
    return this.avatarPreviewDataUrl
      || (this.form.value.avatarUrl?.startsWith('http') ? this.form.value.avatarUrl : null);
  }

  get isDriverPrimary(): boolean {
    return this.primaryRoleDef?.platformCode === 'DRIVER';
  }

  get isFleetManagerPrimary(): boolean {
    return this.primaryRoleDef?.platformCode === 'FLEET_MANAGER';
  }

  get isAccountantPrimary(): boolean {
    return this.primaryRoleDef?.platformCode === 'ACCOUNTANT';
  }

  get autoAssignedRoles(): RoleSummary[] {
    return this.visibleAssignableRoles.filter(r => this.isPrimaryMappedRole(r.id));
  }

  get additionalAssignedRoles(): RoleSummary[] {
    return this.visibleAssignableRoles.filter(
      r => this.selectedRoleIds.has(r.id) && !this.isPrimaryMappedRole(r.id)
    );
  }

  get summaryWorkspaceLabel(): string {
    const def = this.primaryRoleDef;
    if (!def) return '—';
    if (this.form.value.autoWorkspace) return def.workspaceLabel;
    const key = this.form.value.defaultWorkspaceKey;
    const w = this.workspaces.find(x => x.workspaceKey === key);
    return w?.displayName || def.workspaceLabel;
  }

  get summaryDashboardLabel(): string {
    const def = this.primaryRoleDef;
    if (!def) return '—';
    if (this.form.value.autoWorkspace) return def.dashboardLabel;
    const key = this.form.value.defaultDashboardKey;
    const d = this.dashboards.find(x => x.dashboardKey === key);
    return d?.displayName || def.dashboardLabel;
  }

  isRoleSelected(roleId: number): boolean {
    return this.selectedRoleIds.has(roleId);
  }

  isPrimaryMappedRole(roleId: number): boolean {
    const def = this.primaryRoleDef;
    if (!def) return false;
    const role = this.assignableRoles.find(r => r.id === roleId);
    return !!role && role.code.toUpperCase() === def.platformCode.toUpperCase();
  }

  toggleAccessRole(roleId: number): void {
    if (this.isPrimaryMappedRole(roleId)) {
      this.toast.error('Primary role access cannot be removed. Change Primary Role instead.');
      return;
    }
    if (this.selectedRoleIds.has(roleId)) {
      this.selectedRoleIds.delete(roleId);
    } else {
      const role = this.assignableRoles.find(r => r.id === roleId);
      if (role?.code.toUpperCase() === 'SUPER_ADMIN' && !this.canAssignSuperAdmin) {
        this.toast.error('Only platform owners can assign Super Admin.');
        return;
      }
      this.selectedRoleIds.add(roleId);
    }
  }

  onStepSelection(event: StepperSelectionEvent): void {
    if (event.selectedIndex <= event.previouslySelectedIndex) {
      this.currentStep = event.selectedIndex;
      return;
    }
    for (let i = event.previouslySelectedIndex; i < event.selectedIndex; i++) {
      if (!this.validateStep(i)) {
        setTimeout(() => {
          if (this.stepper) this.stepper.selectedIndex = event.previouslySelectedIndex;
        });
        this.toast.error('Complete required fields on the current step before continuing.');
        return;
      }
    }
    this.currentStep = event.selectedIndex;
  }

  goNext(): void {
    if (!this.validateStep(this.currentStep)) {
      this.toast.error('Please complete required fields.');
      return;
    }
    this.stepper.next();
    this.currentStep = this.stepper.selectedIndex;
  }

  goBack(): void {
    this.stepper.previous();
    this.currentStep = this.stepper.selectedIndex;
  }

  validateStep(step: number): boolean {
    if (step === 1) {
      this.form.get('companyId')?.markAsTouched();
      const companyId = this.form.getRawValue().companyId as number | null;
      return companyId != null && companyId > 0;
    }
    if (step !== 0) return true;
    this.syncPhoneField();
    const keys = [...STEP_FIELD_KEYS[0]];
    if (this.isEdit) {
      const idx = keys.indexOf('password');
      if (idx >= 0) keys.splice(idx, 1);
    }
    let ok = true;
    for (const key of keys) {
      const c = this.form.get(key);
      c?.markAsTouched();
      if (c?.invalid) ok = false;
    }
    if (this.emailCheckStatus === 'taken') ok = false;
    return ok;
  }

  generatePassword(): void {
    const pwd = this.buildGeneratedPassword();
    this.form.patchValue({ password: pwd });
    this.hidePassword = false;
  }

  async copyPassword(): Promise<void> {
    const pwd = this.form.value.password as string;
    if (!pwd) return;
    try {
      await navigator.clipboard.writeText(pwd);
      this.toast.success('Password copied.');
    } catch {
      this.toast.error('Could not copy password.');
    }
  }

  triggerAvatarPick(): void {
    this.avatarFileInput?.nativeElement.click();
  }

  onAvatarFile(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (file) this.loadAvatarFile(file);
  }

  onAvatarDrop(event: DragEvent): void {
    event.preventDefault();
    this.avatarDragOver = false;
    const file = event.dataTransfer?.files?.[0];
    if (file?.type.startsWith('image/')) this.loadAvatarFile(file);
  }

  clearAvatarPhoto(): void {
    this.avatarPreviewDataUrl = null;
    if (this.avatarFileInput?.nativeElement) {
      this.avatarFileInput.nativeElement.value = '';
    }
  }

  saveDraft(): void {
    if (this.isEdit) return;
    const draft = {
      form: this.form.getRawValue(),
      selectedRoleIds: [...this.selectedRoleIds],
      currentStep: this.currentStep,
      avatarPreviewDataUrl: this.avatarPreviewDataUrl
    };
    localStorage.setItem(DRAFT_KEY, JSON.stringify(draft));
    this.toast.success('Draft saved. You can continue later on this device.');
  }

  checkEmailAvailability(): void {
    const email = (this.form.get('email')?.value as string)?.trim().toLowerCase();
    if (!email || this.form.get('email')?.invalid) {
      this.emailCheckStatus = email ? 'invalid' : 'idle';
      return;
    }
    if (this.isEdit && email === this.originalEmail.toLowerCase()) {
      this.emailCheckStatus = 'available';
      return;
    }
    this.emailCheckStatus = 'checking';
    this.userService.getAll(1, 25, null, { search: email }).pipe(
      catchError(() => of({ items: [] as User[], totalCount: 0, page: 1, pageSize: 25 }))
    ).subscribe(result => {
      const taken = result.items.some(u => u.email.toLowerCase() === email && u.id !== this.userId);
      this.emailCheckStatus = taken ? 'taken' : 'available';
    });
  }

  reviewCheck(key: string): boolean {
    switch (key) {
      case 'name': return !!this.form.value.fullName?.trim();
      case 'email': return !!this.form.value.email?.trim() && this.emailCheckStatus !== 'taken';
      case 'org': return !!(this.form.getRawValue().companyId || this.form.value.branchId || this.form.value.departmentId || this.form.value.jobTitle);
      case 'role': return !!this.primaryRoleDef;
      case 'permissions': return this.selectedRoleIds.size > 0;
      case 'notifications': return true;
      case 'dashboard': return !!(this.form.value.defaultDashboardKey || this.form.value.autoWorkspace);
      case 'workspace': return !!(this.form.value.defaultWorkspaceKey || this.form.value.autoWorkspace);
      default: return false;
    }
  }

  onPrimaryRoleChanged(id: string, forceDefaults = false): void {
    const def = getPrimaryRoleById(id);
    if (!def) return;

    this.form.patchValue({ role: def.legacyRole }, { emitEvent: false });

    const autoWs = this.form.value.autoWorkspace || forceDefaults;
    const autoHome = this.form.value.autoHomeRoute || forceDefaults;

    if (autoWs || forceDefaults) {
      this.form.patchValue({
        defaultWorkspaceKey: def.defaultWorkspaceKey,
        defaultDashboardKey: def.defaultDashboardKey
      }, { emitEvent: false });
    }
    if (autoHome || forceDefaults) {
      this.form.patchValue({ homeRoute: def.defaultHomeRoute }, { emitEvent: false });
    }

    if (def.platformCode === 'DRIVER' && !this.form.value.employeeType) {
      this.form.patchValue({ employeeType: 'Driver' }, { emitEvent: false });
    } else if (!this.form.value.employeeType) {
      this.form.patchValue(
        { employeeType: defaultEmployeeTypeForPrimary(def) },
        { emitEvent: false }
      );
    }

    this.syncPrimaryPlatformRole(true);
  }

  private syncPrimaryPlatformRole(applyDefaults: boolean): void {
    const def = this.primaryRoleDef;
    if (!def) return;
    const platformId = resolvePlatformRoleId(def.platformCode, this.assignableRoles);
    if (platformId != null) {
      this.selectedRoleIds.add(platformId);
    } else if (applyDefaults) {
      this.toast.error(
        `Role "${def.label}" requires platform role ${def.platformCode}. Apply role templates in Access Control.`
      );
    }
  }

  private ensurePrimaryInSelection(): void {
    this.syncPrimaryPlatformRole(false);
  }

  submit(): void {
    if (!this.validateStep(0) || this.form.invalid) {
      this.form.markAllAsTouched();
      this.toast.error('Please fix validation errors before saving.');
      return;
    }

    this.syncPhoneField();
    this.ensurePrimaryInSelection();
    this.submitting = true;
    const f = this.form.getRawValue();

    if (f.sendWelcomeEmail && !this.isEdit) {
      this.toast.success('Welcome email will be sent when the notification service is connected.');
    }

    const orgFields = {
      companyId: f.companyId as number | null,
      branchId: f.branchId,
      departmentId: f.departmentId,
      jobTitle: f.jobTitle?.trim() || null,
      employeeCode: f.employeeCode?.trim() || null,
      employeeType: f.employeeType || null,
      status: f.status || null,
      defaultWorkspaceKey: f.defaultWorkspaceKey?.trim() || null,
      defaultDashboardKey: f.defaultDashboardKey?.trim() || null,
      homeRoute: f.homeRoute?.trim() || null,
      timeZone: f.timeZone?.trim() || null,
      language: f.language || null,
      theme: f.theme || null,
      avatarUrl: f.avatarUrl?.trim()?.startsWith('http') ? f.avatarUrl.trim() : null
    };

    const roleIds = [...this.selectedRoleIds];
    const scopes = roleIds.map(roleId => ({
      roleId,
      branchId: f.branchId as number | null,
      departmentId: f.departmentId as number | null
    }));

    if (this.isEdit && this.userId) {
      const payload: UpdateUserDto = {
        fullName: f.fullName.trim(),
        email: f.email.trim(),
        phone: f.phone.trim(),
        role: Number(f.role),
        isActive: f.status === 'Active' ? true : !!f.isActive,
        ...orgFields,
        status: f.status
      };

      this.userService.update({ id: this.userId, user: payload }).pipe(
        switchMap(() => this.userService.setUserRoles(this.userId!, { roleIds, scopes }))
      ).subscribe({
        next: () => this.onSuccess('updated'),
        error: (err) => this.onError(err)
      });
    } else {
      const payload: CreateUserDto = {
        fullName: f.fullName.trim(),
        email: f.email.trim(),
        password: f.password,
        phone: f.phone.trim(),
        role: Number(f.role),
        ...orgFields
      };

      this.userService.create({ user: payload }).pipe(
        switchMap((id) => {
          if (!roleIds.length) return of(true);
          return this.userService.setUserRoles(id, { roleIds, scopes });
        })
      ).subscribe({
        next: () => {
          localStorage.removeItem(DRAFT_KEY);
          this.onSuccess('created');
        },
        error: (err) => this.onError(err)
      });
    }
  }

  financialAccessLabel(access: PrimaryRoleDefinition['financialAccess']): string {
    if (access === 'full') return 'Full financial access';
    if (access === 'restricted') return 'Restricted financial access';
    return 'No financial access';
  }

  private loadAvatarFile(file: File): void {
    if (file.size > 2_500_000) {
      this.toast.error('Image must be under 2.5 MB.');
      return;
    }
    const reader = new FileReader();
    reader.onload = () => {
      this.avatarPreviewDataUrl = reader.result as string;
    };
    reader.readAsDataURL(file);
  }

  private syncPhoneField(): void {
    const dial = (this.form.value.phoneCountry as string) || '';
    const national = (this.form.value.phoneNational as string)?.replace(/\s+/g, ' ').trim() || '';
    const combined = national ? `${dial} ${national}`.trim() : '';
    this.form.patchValue({ phone: combined }, { emitEvent: false });
  }

  private splitPhone(phone: string): { country: string; national: string } {
    const trimmed = (phone || '').trim();
    for (const c of this.phoneCountries) {
      if (trimmed.startsWith(c.dial)) {
        return { country: c.dial, national: trimmed.slice(c.dial.length).trim() };
      }
    }
    return { country: '+971', national: trimmed };
  }

  private tryRestoreDraft(): void {
    try {
      const raw = localStorage.getItem(DRAFT_KEY);
      if (!raw) return;
      const draft = JSON.parse(raw) as {
        form: Record<string, unknown>;
        selectedRoleIds: number[];
        currentStep: number;
        avatarPreviewDataUrl?: string | null;
      };
      this.form.patchValue(draft.form);
      this.selectedRoleIds = new Set(draft.selectedRoleIds ?? []);
      this.currentStep = draft.currentStep ?? 0;
      this.avatarPreviewDataUrl = draft.avatarPreviewDataUrl ?? null;
      setTimeout(() => {
        if (this.stepper) this.stepper.selectedIndex = this.currentStep;
      });
    } catch {
      /* ignore corrupt draft */
    }
  }

  private buildGeneratedPassword(): string {
    const upper = 'ABCDEFGHJKLMNPQRSTUVWXYZ';
    const lower = 'abcdefghijkmnopqrstuvwxyz';
    const digits = '23456789';
    const symbols = '!@#$%';
    const all = upper + lower + digits + symbols;
    const pick = (chars: string) => chars[Math.floor(Math.random() * chars.length)];
    const chars = [pick(upper), pick(lower), pick(digits), pick(symbols)];
    for (let i = 0; i < 8; i++) chars.push(pick(all));
    return chars.sort(() => Math.random() - 0.5).join('');
  }

  private onSuccess(action: string): void {
    this.submitting = false;
    this.toast.success(`User ${action} successfully.`);
    void this.router.navigate(['/users']);
  }

  private onError(err: HttpErrorResponse): void {
    this.submitting = false;
    const msg = (err.error as { message?: string })?.message || 'Failed to save user.';
    this.toast.error(msg);
  }
}
