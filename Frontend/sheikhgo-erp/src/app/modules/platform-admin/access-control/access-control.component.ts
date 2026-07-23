import { Component, OnInit, OnDestroy } from '@angular/core';
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
  editingRoleName = '';
  editingRoleActive = true;
  editingRoleDescription = '';
  editingRoleCategory = '';
  roleCategoryFilter: string | 'ALL' = 'ALL';
  roleTypeFilter: 'ALL' | 'System' | 'Custom' = 'ALL';
  roleVisibleFilter: 'ALL' | 'Visible' | 'Hidden' = 'ALL';

  permCategoryFilter: string | 'ALL' = 'ALL';
  permModuleFilter: string | 'ALL' = 'ALL';
  permActionFilter: string | 'ALL' = 'ALL';
  permVisibleFilter: 'ALL' | 'Visible' | 'Hidden' = 'ALL';

  securityForm!: FormGroup;
  roleTemplates: RoleTemplate[] = [];

  get roleCategories(): string[] {
    const set = new Set(
      this.roles.map(r => r.category).filter((c): c is string => !!c && c.trim().length > 0)
    );
    return [...set].sort((a, b) => a.localeCompare(b));
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
    if (tab === 'roles') this.activeTab = 1;
    else if (tab === 'permissions') this.activeTab = 2;
    else if (tab === 'scope' || tab === 'data-scope') this.activeTab = 3;
    else if (tab === 'policies') this.activeTab = 4;
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
    this.destroy$.next();
    this.destroy$.complete();
  }

  onTabChange(index: number): void {
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
        this.loadSecurity(tenantId);
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
          this.selectedRole = roles.find(r => r.id === this.selectedRole!.id) ?? null;
          if (this.selectedRole) {
            this.selectRole(this.selectedRole);
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
    this.filteredRoles = this.roles.filter(r => {
      if (this.roleCategoryFilter !== 'ALL' && (r.category || '') !== this.roleCategoryFilter) {
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
    this.selectedRole = role;
    this.selectedPermissionCodes = new Set(role.permissions);
    this.editingRoleName = role.displayName || role.name;
    this.editingRoleActive = role.isActive;
    this.editingRoleDescription = role.description || '';
    this.editingRoleCategory = role.category || '';
  }

  togglePermission(code: string): void {
    if (this.selectedPermissionCodes.has(code)) {
      this.selectedPermissionCodes.delete(code);
    } else {
      this.selectedPermissionCodes.add(code);
    }
  }

  createRole(): void {
    if (!this.tenantId || !this.newRoleName.trim() || !this.newRoleCode.trim()) {
      this.toast.warning('Role name and code are required.');
      return;
    }

    this.platform.createRoleForTenant(this.tenantId, this.newRoleName.trim(), this.newRoleCode.trim()).subscribe({
      next: () => {
        this.newRoleName = '';
        this.newRoleCode = '';
        this.toast.success('Role created.');
        this.loadRoles(this.tenantId!);
      },
      error: (err) => this.toast.error(apiErrorMessage(err, 'Create failed.'))
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

    this.saving = true;
    this.platform.updateRolePermissionsForTenant(
      this.tenantId,
      this.selectedRole.id,
      [...this.selectedPermissionCodes]
    ).subscribe({
      next: () => {
        this.saving = false;
        this.toast.success('Permissions saved.');
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
      this.toast.success('System roles cannot be deleted.');
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
