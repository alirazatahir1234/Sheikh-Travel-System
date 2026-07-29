import { Component, HostListener, OnInit, OnDestroy } from '@angular/core';
import { FormBuilder, FormGroup } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { Subject, forkJoin, takeUntil, catchError, of } from 'rxjs';
import { PlatformTenantContextService } from '../../../core/services/platform-tenant-context.service';
import { PlatformService } from '../../../core/services/platform.service';
import { UserService } from '../../../core/services/user.service';
import {
  Permission,
  EffectivePermission,
  RoleSummary,
  RoleTemplate,
  Tenant,
  TenantSecuritySettings,
  CompanyDataScope
} from '../../../core/models/platform.model';
import { User, UserRole, UserRoleLabels } from '../../../core/models/user.model';
import { apiErrorMessage } from '../../../core/utils/api-error.util';
import {
  ROLE_CATEGORY_OPTIONS,
  inferRoleCategory,
  isValidRoleCode,
  permissionDescription,
  permissionFriendlyLabel,
  roleVisibilityLabel,
  slugifyRoleCode
} from './access-control-role.util';

@Component({
  standalone: false,
  selector: 'app-access-control',
  templateUrl: './access-control.component.html',
  styleUrls: ['./access-control.component.scss']
})
export class AccessControlComponent implements OnInit, OnDestroy {
  loading = false;
  tabLoading = false;
  saving = false;
  selectedTenant: Tenant | null = null;
  activeTab = 0;
  tenantId: number | null = null;

  users: User[] = [];
  usersTotal = 0;
  usersPage = 1;
  readonly usersPageSize = 10;

  roles: RoleSummary[] = [];
  filteredRoles: RoleSummary[] = [];
  permissions: Permission[] = [];
  filteredPermissions: Permission[] = [];
  effectivePermissions: EffectivePermission[] = [];
  myDataScope: CompanyDataScope | null = null;
  selectedRole: RoleSummary | null = null;
  selectedPermissionCodes = new Set<string>();
  newRoleName = '';
  newRoleCode = '';
  newRoleCategory = 'Operations';
  newRoleDescription = '';
  newRoleCodeLocked = true;
  createRolePanelOpen = false;
  roleSearchQuery = '';
  rolePermissionSearch = '';
  readonly roleCategoryOptions = [...ROLE_CATEGORY_OPTIONS];
  readonly roleTableColumns = ['name', 'category', 'status', 'visibility', 'users', 'perms', 'actions'];
  /** Mat-tab index for the Roles tab (Users = 0). */
  readonly rolesTabIndex = 1;
  collapsedPermissionModules = new Set<string>();
  rolePermissionGroups: { module: string; items: Permission[] }[] = [];
  private baselinePermissionCodes = new Set<string>();
  private baselineRoleDetails = {
    name: '',
    active: true,
    description: '',
    category: ''
  };
  editingRoleName = '';
  editingRoleActive = true;
  editingRoleDescription = '';
  editingRoleCategory = '';
  roleCategoryFilter: string | 'ALL' = 'ALL';
  roleTypeFilter: 'ALL' | 'System' | 'Custom' = 'ALL';
  roleVisibleFilter: 'ALL' | 'Visible' | 'Hidden' = 'ALL';
  roleDetailFullscreen = false;

  permCategoryFilter: string | 'ALL' = 'ALL';
  permModuleFilter: string | 'ALL' = 'ALL';
  permActionFilter: string | 'ALL' = 'ALL';
  permVisibleFilter: 'ALL' | 'Visible' | 'Hidden' = 'ALL';

  securityForm!: FormGroup;
  roleTemplates: RoleTemplate[] = [];

  get roleCategories(): string[] {
    const set = new Set<string>(this.roleCategoryOptions);
    for (const r of this.roles) {
      const c = this.displayRoleCategory(r);
      if (c) set.add(c);
    }
    return [...set].sort((a, b) => a.localeCompare(b));
  }

  get totalPermissionCount(): number {
    return this.permissions.length;
  }

  get selectedRolePermissionCount(): number {
    return this.selectedPermissionCodes.size;
  }

  get hasUnsavedPermissionChanges(): boolean {
    if (this.baselinePermissionCodes.size !== this.selectedPermissionCodes.size) return true;
    for (const code of this.selectedPermissionCodes) {
      if (!this.baselinePermissionCodes.has(code)) return true;
    }
    return false;
  }

  get hasUnsavedDetailChanges(): boolean {
    return (
      this.editingRoleName.trim() !== this.baselineRoleDetails.name ||
      this.editingRoleActive !== this.baselineRoleDetails.active ||
      this.editingRoleDescription.trim() !== this.baselineRoleDetails.description ||
      this.editingRoleCategory.trim() !== this.baselineRoleDetails.category
    );
  }

  get hasUnsavedRoleChanges(): boolean {
    return !!this.selectedRole && (this.hasUnsavedPermissionChanges || this.hasUnsavedDetailChanges);
  }

  @HostListener('document:keydown.escape')
  onEscapeKey(): void {
    if (this.roleDetailFullscreen) {
      this.setRoleDetailFullscreen(false);
    }
  }

  @HostListener('window:beforeunload', ['$event'])
  onBeforeUnload(event: BeforeUnloadEvent): void {
    if (this.hasUnsavedRoleChanges) {
      event.preventDefault();
    }
  }

  get permissionCategories(): string[] {
    const set = new Set(
      this.permissions.map(p => p.category).filter((c): c is string => !!c && c.trim().length > 0)
    );
    return [...set].sort((a, b) => a.localeCompare(b));
  }

  get permissionModules(): string[] {
    const set = new Set(
      this.permissions
        .map(p => p.moduleKey || p.moduleName)
        .filter((c): c is string => !!c && c.trim().length > 0)
    );
    return [...set].sort((a, b) => a.localeCompare(b));
  }

  get permissionActions(): string[] {
    const set = new Set(
      this.permissions.map(p => p.action).filter((c): c is string => !!c && c.trim().length > 0)
    );
    return [...set].sort((a, b) => a.localeCompare(b));
  }

  get effectiveCategoryChips(): string[] {
    const set = new Set(
      this.effectivePermissions
        .map(p => p.category)
        .filter((c): c is string => !!c && c.trim().length > 0)
    );
    return [...set].sort((a, b) => a.localeCompare(b)).slice(0, 8);
  }

  private destroy$ = new Subject<void>();

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private fb: FormBuilder,
    private toast: UiToastService,
    private tenantContext: PlatformTenantContextService,
    private platform: PlatformService,
    private usersApi: UserService
  ) {
    this.securityForm = this.fb.group({
      isMfaRequired: [false],
      passwordExpiryDays: [90],
      sessionTimeoutMinutes: [30],
      isGdprEnabled: [true],
      isAuditLoggingEnabled: [true],
      isVatEnabled: [false]
    });
  }

  ngOnInit(): void {
    const tab = this.route.snapshot.queryParamMap.get('tab')
      ?? (this.route.snapshot.data['defaultTab'] as string | undefined);
    if (tab === 'policies') {
      const q: Record<string, string> = {};
      const tid = this.route.snapshot.queryParamMap.get('tenantId');
      if (tid) q['tenantId'] = tid;
      this.router.navigate(['/platform/security-center'], { queryParams: q });
      return;
    }
    if (tab === 'roles') this.activeTab = 1;
    else if (tab === 'permissions') this.activeTab = 2;
    else if (tab === 'scope' || tab === 'data-scope') this.activeTab = 3;
    else if (tab === 'templates') this.activeTab = 5;

    const tenantIdParam = this.route.snapshot.queryParamMap.get('tenantId');
    if (tenantIdParam) {
      const id = parseInt(tenantIdParam, 10);
      if (Number.isFinite(id)) {
        this.tenantContext.selectTenantById(id);
      }
    }

    this.platform.getPermissions().subscribe(perms => {
      this.permissions = perms;
      this.applyPermissionFilters();
      this.rebuildRolePermissionGroups();
    });

    this.platform.getEffectivePermissions().pipe(
      catchError(() => of([] as EffectivePermission[]))
    ).subscribe(rows => {
      this.effectivePermissions = rows;
    });

    this.platform.getRoleTemplates().subscribe(templates => {
      this.roleTemplates = templates;
    });

    this.tenantContext.tenant$
      .pipe(takeUntil(this.destroy$))
      .subscribe(tenant => {
        this.selectedTenant = tenant;
      });

    this.tenantContext.tenantId$
      .pipe(takeUntil(this.destroy$))
      .subscribe(id => {
        this.tenantId = id;
        if (id) {
          this.loadTenantData(id);
        } else {
          this.selectedTenant = null;
          this.resetData();
        }
      });
  }

  ngOnDestroy(): void {
    this.setRoleDetailFullscreen(false);
    this.destroy$.next();
    this.destroy$.complete();
  }

  onTabChange(index: number): void {
    if (index !== this.rolesTabIndex) {
      this.exitRoleDetailFullscreen();
    }
    this.activeTab = index;
    if (this.tenantId) {
      this.loadTabData(this.tenantId, index);
    }
  }

  private resetData(): void {
    this.users = [];
    this.roles = [];
    this.filteredRoles = [];
    this.selectedRole = null;
    this.setRoleDetailFullscreen(false);
    this.roleTemplates = [];
  }

  private loadTenantData(tenantId: number): void {
    this.loading = true;
    this.platform.getTenantById(tenantId).subscribe({
      next: (tenant) => {
        this.tenantContext.setTenantDetails(tenant as unknown as Tenant);
        this.selectedTenant = tenant as unknown as Tenant;
        this.loading = false;
        this.loadTabData(tenantId, this.activeTab);
      },
      error: () => {
        this.loading = false;
      }
    });
  }

  private loadTabData(tenantId: number, tab: number): void {
    this.tabLoading = true;
    switch (tab) {
      case 0:
        this.loadUsers(tenantId);
        break;
      case 1:
        this.loadRoles(tenantId);
        break;
      case 2:
        this.loadRoles(tenantId);
        break;
      case 3:
        this.loadDataScope();
        break;
      case 4:
        this.tabLoading = false;
        break;
      case 5:
        this.tabLoading = false;
        break;
      default:
        this.tabLoading = false;
    }
  }

  private loadDataScope(): void {
    this.platform.getMyDataScope().pipe(
      catchError(() => of(null as CompanyDataScope | null))
    ).subscribe({
      next: scope => {
        this.myDataScope = scope;
        this.tabLoading = false;
      },
      error: () => {
        this.myDataScope = null;
        this.tabLoading = false;
      }
    });
  }

  private loadUsers(tenantId: number): void {
    this.usersApi.getAll(this.usersPage, this.usersPageSize, tenantId).subscribe({
      next: (result) => {
        this.users = result.items;
        this.usersTotal = result.totalCount;
        this.tabLoading = false;
      },
      error: (err) => {
        this.tabLoading = false;
        this.toast.error(apiErrorMessage(err, 'Failed to load users.'));
      }
    });
  }

  private loadRoles(tenantId: number): void {
    this.platform.getRolesForTenant(tenantId).subscribe({
      next: (roles) => {
        this.roles = roles;
        this.applyRoleFilters();
        if (this.selectedRole) {
          const updated = roles.find(r => r.id === this.selectedRole!.id) ?? null;
          if (updated) {
            this.selectedRole = updated;
          } else {
            this.selectedRole = null;
          }
        }
        this.tabLoading = false;
      },
      error: (err) => {
        this.tabLoading = false;
        this.toast.error(apiErrorMessage(err, 'Failed to load roles.'));
      }
    });
  }

  applyRoleFilters(): void {
    const q = this.roleSearchQuery.trim().toLowerCase();
    this.filteredRoles = this.roles.filter(r => {
      if (q) {
        const hay = `${r.displayName || ''} ${r.name} ${r.code}`.toLowerCase();
        if (!hay.includes(q)) return false;
      }
      if (this.roleCategoryFilter !== 'ALL' && this.displayRoleCategory(r) !== this.roleCategoryFilter) {
        return false;
      }
      const type = (r.roleType || (r.isSystem ? 'System' : 'Custom')).toLowerCase();
      if (this.roleTypeFilter === 'System' && type !== 'system') return false;
      if (this.roleTypeFilter === 'Custom' && type === 'system') return false;
      if (this.roleVisibleFilter === 'Visible' && r.visible === false) return false;
      if (this.roleVisibleFilter === 'Hidden' && r.visible !== false) return false;
      return true;
    });
  }

  onRoleSearchChange(): void {
    this.applyRoleFilters();
  }

  displayRoleCategory(role: RoleSummary): string {
    return inferRoleCategory(role);
  }

  roleStatusLabel(role: RoleSummary): string {
    return role.isActive ? 'Active' : 'Disabled';
  }

  roleVisibilityLabel(role: RoleSummary): string {
    return roleVisibilityLabel(role);
  }

  permissionLabel(p: Permission): string {
    return permissionFriendlyLabel(p);
  }

  permissionDesc(p: Permission): string {
    return permissionDescription(p);
  }

  onNewRoleNameChange(): void {
    if (this.newRoleCodeLocked) {
      this.newRoleCode = slugifyRoleCode(this.newRoleName);
    }
  }

  unlockNewRoleCode(): void {
    this.newRoleCodeLocked = false;
  }

  lockNewRoleCode(): void {
    this.newRoleCodeLocked = true;
    this.newRoleCode = slugifyRoleCode(this.newRoleName);
  }

  get createRoleNameError(): string | null {
    const name = this.newRoleName.trim();
    if (!name) return 'Name is required.';
    if (name.length < 2) return 'Name must be at least 2 characters.';
    if (name.length > 100) return 'Name cannot exceed 100 characters.';
    const dup = this.roles.some(
      r => (r.displayName || r.name).toLowerCase() === name.toLowerCase()
    );
    if (dup) return 'A role with this name already exists.';
    return null;
  }

  get createRoleCodeError(): string | null {
    const code = this.newRoleCode.trim();
    if (!code) return 'Code is required.';
    if (!isValidRoleCode(code)) return 'Use SCREAMING_SNAKE_CASE (letters, numbers, underscores).';
    if (this.roles.some(r => r.code.toUpperCase() === code.toUpperCase())) {
      return 'This role code is already in use.';
    }
    return null;
  }

  get canSubmitCreateRole(): boolean {
    return !this.createRoleNameError && !this.createRoleCodeError && !!this.newRoleCategory;
  }

  toggleCreateRolePanel(): void {
    this.createRolePanelOpen = !this.createRolePanelOpen;
  }

  onRoleFiltersChange(): void {
    this.applyRoleFilters();
  }

  private loadSecurity(tenantId: number): void {
    this.platform.getTenantSecuritySettings(tenantId).subscribe({
      next: (settings) => {
        this.securityForm.patchValue(settings);
        this.tabLoading = false;
      },
      error: (err) => {
        this.tabLoading = false;
        this.toast.error(apiErrorMessage(err, 'Failed to load security settings.'));
      }
    });
  }

  refreshTab(): void {
    if (this.tenantId) {
      this.loadTabData(this.tenantId, this.activeTab);
    }
  }

  usersPageChange(page: number): void {
    this.usersPage = page;
    if (this.tenantId) this.loadUsers(this.tenantId);
  }

  openUsersModule(): void {
    void this.router.navigate(['/users']);
  }

  selectRole(role: RoleSummary): void {
    if (this.hasUnsavedRoleChanges && this.selectedRole?.id !== role.id) {
      if (!confirm('You have unsaved changes. Switch roles anyway?')) return;
    }
    this.selectedRole = role;
    this.selectedPermissionCodes = new Set(role.permissions);
    this.baselinePermissionCodes = new Set(role.permissions);
    this.editingRoleName = role.displayName || role.name;
    this.editingRoleActive = role.isActive;
    this.editingRoleDescription = role.description || '';
    this.editingRoleCategory = role.category || inferRoleCategory(role);
    this.baselineRoleDetails = {
      name: this.editingRoleName.trim(),
      active: this.editingRoleActive,
      description: this.editingRoleDescription.trim(),
      category: this.editingRoleCategory.trim()
    };
    this.rolePermissionSearch = '';
    this.collapseAllPermissionModules();
    this.rebuildRolePermissionGroups();
  }

  onRolePermissionSearchChange(): void {
    this.rebuildRolePermissionGroups();
  }

  trackRolePermissionGroup(_index: number, group: { module: string }): string {
    return group.module;
  }

  trackPermissionCode(_index: number, permission: Permission): string {
    return permission.permissionCode;
  }

  private rebuildRolePermissionGroups(): void {
    const map = new Map<string, Permission[]>();
    for (const p of this.roleMatrixPermissions()) {
      const key = p.moduleName || p.moduleKey || p.category || 'Other';
      if (!map.has(key)) map.set(key, []);
      map.get(key)!.push(p);
    }
    this.rolePermissionGroups = [...map.entries()]
      .sort((a, b) => a[0].localeCompare(b[0]))
      .map(([module, items]) => ({ module, items }));
  }

  private mutateSelectedPermissions(mutator: (codes: Set<string>) => void): void {
    const scrollState = this.captureRolePanelScrollTops();
    mutator(this.selectedPermissionCodes);
    this.selectedPermissionCodes = new Set(this.selectedPermissionCodes);
    this.restoreRolePanelScrollTops(scrollState);
  }

  private captureRolePanelScrollTops(): { panes: Map<HTMLElement, number>; windowY: number } {
    const panes = new Map<HTMLElement, number>();
    if (typeof document === 'undefined') {
      return { panes, windowY: 0 };
    }
    for (const el of Array.from(document.querySelectorAll<HTMLElement>('.role-detail-card, .role-detail-card__body'))) {
      if (el.scrollHeight > el.clientHeight + 1) {
        panes.set(el, el.scrollTop);
      }
    }
    const shellMain = document.querySelector<HTMLElement>('.stb-content, .stb-main');
    if (shellMain && shellMain.scrollHeight > shellMain.clientHeight + 1) {
      panes.set(shellMain, shellMain.scrollTop);
    }
    const windowY = typeof window !== 'undefined' ? window.scrollY : 0;
    return { panes, windowY };
  }

  private restoreRolePanelScrollTops(state: { panes: Map<HTMLElement, number>; windowY: number }): void {
    const apply = (): void => {
      state.panes.forEach((top, el) => {
        el.scrollTop = top;
      });
      if (typeof window !== 'undefined') {
        window.scrollTo(0, state.windowY);
      }
    };
    queueMicrotask(apply);
    requestAnimationFrame(() => requestAnimationFrame(apply));
  }

  private collapseAllPermissionModules(): void {
    const modules = new Set<string>();
    for (const p of this.permissions) {
      modules.add(p.moduleName || p.moduleKey || p.category || 'Other');
    }
    this.collapsedPermissionModules = modules;
  }

  openUsersForRole(role: RoleSummary, event?: Event): void {
    event?.stopPropagation();
    const label = role.displayName || role.name;
    void this.router.navigate(['/users'], { queryParams: { search: label } });
  }

  focusRolePermissions(role: RoleSummary, event?: Event): void {
    event?.stopPropagation();
    this.selectRole(role);
    this.setRoleDetailFullscreen(true);
  }

  toggleRoleDetailFullscreen(): void {
    this.setRoleDetailFullscreen(!this.roleDetailFullscreen);
  }

  exitRoleDetailFullscreen(): void {
    this.setRoleDetailFullscreen(false);
  }

  private setRoleDetailFullscreen(fullscreen: boolean): void {
    if (this.roleDetailFullscreen === fullscreen) return;
    this.roleDetailFullscreen = fullscreen;
    if (typeof document !== 'undefined') {
      document.body.style.overflow = fullscreen ? 'hidden' : '';
    }
  }

  setPermissionChecked(code: string, checked: boolean): void {
    this.mutateSelectedPermissions(codes => {
      if (checked) codes.add(code);
      else codes.delete(code);
    });
  }

  createRole(): void {
    if (!this.tenantId) return;
    const nameErr = this.createRoleNameError;
    const codeErr = this.createRoleCodeError;
    if (nameErr || codeErr) {
      this.toast.warning(nameErr || codeErr || 'Fix validation errors before creating.');
      return;
    }

    const name = this.newRoleName.trim();
    const code = this.newRoleCode.trim().toUpperCase();
    const category = this.newRoleCategory.trim();
    const description = this.newRoleDescription.trim();

    this.saving = true;
    this.platform.createRoleForTenant(this.tenantId, name, code).subscribe({
      next: (roleId) => {
        const finish = () => {
          this.saving = false;
          this.newRoleName = '';
          this.newRoleCode = '';
          this.newRoleDescription = '';
          this.newRoleCategory = 'Operations';
          this.newRoleCodeLocked = true;
          this.createRolePanelOpen = false;
          this.toast.success('Role created.');
          this.loadRoles(this.tenantId!);
        };

        if (!description && !category) {
          finish();
          return;
        }

        this.platform.updateRoleForTenant(this.tenantId!, roleId, name, true, {
          displayName: name,
          description: description || null,
          category: category || null
        }).subscribe({
          next: () => finish(),
          error: (err) => {
            this.saving = false;
            this.toast.warning(apiErrorMessage(err, 'Role created but metadata update failed.'));
            this.loadRoles(this.tenantId!);
          }
        });
      },
      error: (err) => {
        this.saving = false;
        this.toast.error(apiErrorMessage(err, 'Create failed.'));
      }
    });
  }

  saveRoleDetails(): void {
    if (!this.tenantId || !this.selectedRole) return;

    this.saving = true;
    this.platform.updateRoleForTenant(
      this.tenantId,
      this.selectedRole.id,
      this.editingRoleName.trim() || this.selectedRole.name,
      this.editingRoleActive,
      {
        displayName: this.editingRoleName.trim() || this.selectedRole.name,
        description: this.editingRoleDescription.trim() || null,
        category: this.editingRoleCategory.trim() || null
      }
    ).subscribe({
      next: () => {
        this.saving = false;
        this.toast.success('Role updated.');
        this.baselineRoleDetails = {
          name: this.editingRoleName.trim(),
          active: this.editingRoleActive,
          description: this.editingRoleDescription.trim(),
          category: this.editingRoleCategory.trim()
        };
        this.loadRoles(this.tenantId!);
      },
      error: (err) => {
        this.saving = false;
        this.toast.error(apiErrorMessage(err, 'Update failed.'));
      }
    });
  }

  saveRolePermissions(): void {
    if (!this.tenantId || !this.selectedRole) return;

    const added = [...this.selectedPermissionCodes].filter(c => !this.baselinePermissionCodes.has(c)).length;
    const removed = [...this.baselinePermissionCodes].filter(c => !this.selectedPermissionCodes.has(c)).length;
    if (added + removed > 0) {
      const lines = [];
      if (added) lines.push(`${added} permission(s) added`);
      if (removed) lines.push(`${removed} permission(s) removed`);
      if (!confirm(`Save permission changes?\n\n${lines.join('\n')}`)) return;
    }

    this.saving = true;
    this.platform.updateRolePermissionsForTenant(
      this.tenantId,
      this.selectedRole.id,
      [...this.selectedPermissionCodes]
    ).subscribe({
      next: () => {
        this.saving = false;
        this.toast.success('Permissions saved.');
        this.baselinePermissionCodes = new Set(this.selectedPermissionCodes);
        this.loadRoles(this.tenantId!);
      },
      error: (err) => {
        this.saving = false;
        this.toast.error(apiErrorMessage(err, 'Save failed.'));
      }
    });
  }

  deleteRole(role: RoleSummary): void {
    if (!this.tenantId) return;
    if (role.isSystem) {
      this.toast.warning('System roles cannot be deleted. Disable the role instead.');
      return;
    }
    if (!confirm(`Delete role "${role.name}"?`)) return;

    this.platform.deleteRoleForTenant(this.tenantId, role.id).subscribe({
      next: () => {
        if (this.selectedRole?.id === role.id) this.selectedRole = null;
        this.toast.success('Role deleted.');
        this.loadRoles(this.tenantId!);
      },
      error: (err) => this.toast.error(apiErrorMessage(err, 'Delete failed.'))
    });
  }

  saveSecuritySettings(): void {
    if (!this.tenantId) return;

    this.saving = true;
    const payload = this.securityForm.value as TenantSecuritySettings;
    this.platform.updateTenantSecuritySettings(this.tenantId, payload).subscribe({
      next: () => {
        this.saving = false;
        this.toast.success('Security settings saved.');
      },
      error: (err) => {
        this.saving = false;
        this.toast.error(apiErrorMessage(err, 'Save failed.'));
      }
    });
  }

  applyTemplate(template: RoleTemplate): void {
    if (!this.tenantId) return;
    if (!confirm(`Apply "${template.name}" template? This will reset permissions for that role.`)) return;

    this.saving = true;
    this.platform.applyRoleTemplate(this.tenantId, template.code).subscribe({
      next: () => {
        this.saving = false;
        this.toast.success(`Template "${template.name}" applied.`);
        this.loadRoles(this.tenantId!);
      },
      error: (err) => {
        this.saving = false;
        this.toast.error(apiErrorMessage(err, 'Apply failed.'));
      }
    });
  }

  permissionsByModule(): { module: string; items: Permission[] }[] {
    const map = new Map<string, Permission[]>();
    for (const p of this.filteredPermissions.length ? this.filteredPermissions : this.permissions) {
      const key = p.category || p.moduleName || 'Other';
      if (!map.has(key)) map.set(key, []);
      map.get(key)!.push(p);
    }
    return [...map.entries()].map(([module, items]) => ({ module, items }));
  }

  roleMatrixPermissions(): Permission[] {
    const q = this.rolePermissionSearch.trim().toLowerCase();
    if (!q) return this.permissions;
    return this.permissions.filter(p => {
      const label = permissionFriendlyLabel(p).toLowerCase();
      return (
        p.permissionCode.toLowerCase().includes(q) ||
        label.includes(q) ||
        (p.description || '').toLowerCase().includes(q)
      );
    });
  }

  rolePermissionsByModule(): { module: string; items: Permission[] }[] {
    return this.rolePermissionGroups;
  }

  isPermissionModuleCollapsed(module: string): boolean {
    return this.collapsedPermissionModules.has(module);
  }

  togglePermissionModule(module: string): void {
    const scrollState = this.captureRolePanelScrollTops();
    const next = new Set(this.collapsedPermissionModules);
    if (next.has(module)) next.delete(module);
    else next.add(module);
    this.collapsedPermissionModules = next;
    this.restoreRolePanelScrollTops(scrollState);
  }

  modulePermissionState(module: string): 'all' | 'some' | 'none' {
    const group = this.rolePermissionGroups.find(g => g.module === module);
    if (!group?.items.length) return 'none';
    let selected = 0;
    for (const p of group.items) {
      if (this.selectedPermissionCodes.has(p.permissionCode)) selected++;
    }
    if (selected === 0) return 'none';
    if (selected === group.items.length) return 'all';
    return 'some';
  }

  setModulePermissions(module: string, checked: boolean): void {
    const group = this.rolePermissionGroups.find(g => g.module === module);
    if (!group) return;
    this.mutateSelectedPermissions(codes => {
      for (const p of group.items) {
        if (checked) codes.add(p.permissionCode);
        else codes.delete(p.permissionCode);
      }
    });
  }

  setAllRoleMatrixPermissions(checked: boolean): void {
    this.mutateSelectedPermissions(codes => {
      for (const p of this.roleMatrixPermissions()) {
        if (checked) codes.add(p.permissionCode);
        else codes.delete(p.permissionCode);
      }
    });
  }

  roleMatrixAllSelected(): boolean {
    const items = this.roleMatrixPermissions();
    return items.length > 0 && items.every(p => this.selectedPermissionCodes.has(p.permissionCode));
  }

  roleMatrixSomeSelected(): boolean {
    const items = this.roleMatrixPermissions();
    const n = items.filter(p => this.selectedPermissionCodes.has(p.permissionCode)).length;
    return n > 0 && n < items.length;
  }

  applyPermissionFilters(): void {
    this.filteredPermissions = this.permissions.filter(p => {
      if (this.permCategoryFilter !== 'ALL' && (p.category || '') !== this.permCategoryFilter) {
        return false;
      }
      const mod = p.moduleKey || p.moduleName || '';
      if (this.permModuleFilter !== 'ALL' && mod !== this.permModuleFilter) {
        return false;
      }
      if (this.permActionFilter !== 'ALL' && (p.action || '') !== this.permActionFilter) {
        return false;
      }
      if (this.permVisibleFilter === 'Visible' && p.visible === false) return false;
      if (this.permVisibleFilter === 'Hidden' && p.visible !== false) return false;
      return true;
    });
  }

  onPermissionFiltersChange(): void {
    this.applyPermissionFilters();
  }

  rolesWithPermission(code: string): string[] {
    return this.roles
      .filter(r => r.permissions.some(p => p.toLowerCase() === code.toLowerCase()))
      .map(r => r.displayName || r.name);
  }

  roleLabel(role: UserRole): string {
    return UserRoleLabels[role] ?? String(role);
  }

  goBack(): void {
    this.router.navigate(['/platform/tenants']);
  }
}
