import { Component, OnInit, OnDestroy } from '@angular/core';
import { FormBuilder, FormGroup } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Subject, forkJoin, takeUntil } from 'rxjs';
import { PlatformTenantContextService } from '../../../core/services/platform-tenant-context.service';
import { PlatformService } from '../../../core/services/platform.service';
import { UserService } from '../../../core/services/user.service';
import {
  Permission,
  RoleSummary,
  RoleTemplate,
  Tenant,
  TenantSecuritySettings
} from '../../../core/models/platform.model';
import { User, UserRole, UserRoleLabels } from '../../../core/models/user.model';
import { apiErrorMessage } from '../../../core/utils/api-error.util';

@Component({
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
  permissions: Permission[] = [];
  selectedRole: RoleSummary | null = null;
  selectedPermissionCodes = new Set<string>();
  newRoleName = '';
  newRoleCode = '';
  editingRoleName = '';
  editingRoleActive = true;

  securityForm!: FormGroup;
  roleTemplates: RoleTemplate[] = [];

  private destroy$ = new Subject<void>();

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private fb: FormBuilder,
    private snackBar: MatSnackBar,
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
    const tab = this.route.snapshot.queryParamMap.get('tab');
    if (tab === 'policies') this.activeTab = 3;

    const tenantIdParam = this.route.snapshot.queryParamMap.get('tenantId');
    if (tenantIdParam) {
      const id = parseInt(tenantIdParam, 10);
      if (Number.isFinite(id)) {
        this.tenantContext.selectTenantById(id);
      }
    }

    this.platform.getPermissions().subscribe(perms => {
      this.permissions = perms;
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
        this.loadSecurity(tenantId);
        break;
      case 4:
        this.tabLoading = false;
        break;
      default:
        this.tabLoading = false;
    }
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
        this.snackBar.open(apiErrorMessage(err, 'Failed to load users.'), 'Close', { duration: 4000 });
      }
    });
  }

  private loadRoles(tenantId: number): void {
    this.platform.getRolesForTenant(tenantId).subscribe({
      next: (roles) => {
        this.roles = roles;
        if (this.selectedRole) {
          this.selectedRole = roles.find(r => r.id === this.selectedRole!.id) ?? null;
          if (this.selectedRole) {
            this.selectedPermissionCodes = new Set(this.selectedRole.permissions);
            this.editingRoleName = this.selectedRole.name;
            this.editingRoleActive = this.selectedRole.isActive;
          }
        }
        this.tabLoading = false;
      },
      error: (err) => {
        this.tabLoading = false;
        this.snackBar.open(apiErrorMessage(err, 'Failed to load roles.'), 'Close', { duration: 4000 });
      }
    });
  }

  private loadSecurity(tenantId: number): void {
    this.platform.getTenantSecuritySettings(tenantId).subscribe({
      next: (settings) => {
        this.securityForm.patchValue(settings);
        this.tabLoading = false;
      },
      error: (err) => {
        this.tabLoading = false;
        this.snackBar.open(apiErrorMessage(err, 'Failed to load security settings.'), 'Close', { duration: 4000 });
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
    this.editingRoleName = role.name;
    this.editingRoleActive = role.isActive;
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
      this.snackBar.open('Role name and code are required.', 'Close', { duration: 3000 });
      return;
    }

    this.platform.createRoleForTenant(this.tenantId, this.newRoleName.trim(), this.newRoleCode.trim()).subscribe({
      next: () => {
        this.newRoleName = '';
        this.newRoleCode = '';
        this.snackBar.open('Role created.', 'Close', { duration: 2500 });
        this.loadRoles(this.tenantId!);
      },
      error: (err) => this.snackBar.open(apiErrorMessage(err, 'Create failed.'), 'Close', { duration: 4000 })
    });
  }

  saveRoleDetails(): void {
    if (!this.tenantId || !this.selectedRole) return;

    this.saving = true;
    this.platform.updateRoleForTenant(
      this.tenantId,
      this.selectedRole.id,
      this.editingRoleName.trim(),
      this.editingRoleActive
    ).subscribe({
      next: () => {
        this.saving = false;
        this.snackBar.open('Role updated.', 'Close', { duration: 2500 });
        this.loadRoles(this.tenantId!);
      },
      error: (err) => {
        this.saving = false;
        this.snackBar.open(apiErrorMessage(err, 'Update failed.'), 'Close', { duration: 4000 });
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
        this.snackBar.open('Permissions saved.', 'Close', { duration: 2500 });
        this.loadRoles(this.tenantId!);
      },
      error: (err) => {
        this.saving = false;
        this.snackBar.open(apiErrorMessage(err, 'Save failed.'), 'Close', { duration: 4000 });
      }
    });
  }

  deleteRole(role: RoleSummary): void {
    if (!this.tenantId) return;
    if (role.isSystem) {
      this.snackBar.open('System roles cannot be deleted.', 'Close', { duration: 3000 });
      return;
    }
    if (!confirm(`Delete role "${role.name}"?`)) return;

    this.platform.deleteRoleForTenant(this.tenantId, role.id).subscribe({
      next: () => {
        if (this.selectedRole?.id === role.id) this.selectedRole = null;
        this.snackBar.open('Role deleted.', 'Close', { duration: 2500 });
        this.loadRoles(this.tenantId!);
      },
      error: (err) => this.snackBar.open(apiErrorMessage(err, 'Delete failed.'), 'Close', { duration: 4000 })
    });
  }

  saveSecuritySettings(): void {
    if (!this.tenantId) return;

    this.saving = true;
    const payload = this.securityForm.value as TenantSecuritySettings;
    this.platform.updateTenantSecuritySettings(this.tenantId, payload).subscribe({
      next: () => {
        this.saving = false;
        this.snackBar.open('Security settings saved.', 'Close', { duration: 2500 });
      },
      error: (err) => {
        this.saving = false;
        this.snackBar.open(apiErrorMessage(err, 'Save failed.'), 'Close', { duration: 4000 });
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
        this.snackBar.open(`Template "${template.name}" applied.`, 'Close', { duration: 2500 });
        this.loadRoles(this.tenantId!);
      },
      error: (err) => {
        this.saving = false;
        this.snackBar.open(apiErrorMessage(err, 'Apply failed.'), 'Close', { duration: 4000 });
      }
    });
  }

  permissionsByModule(): { module: string; items: Permission[] }[] {
    const map = new Map<string, Permission[]>();
    for (const p of this.permissions) {
      if (!map.has(p.moduleName)) map.set(p.moduleName, []);
      map.get(p.moduleName)!.push(p);
    }
    return [...map.entries()].map(([module, items]) => ({ module, items }));
  }

  rolesWithPermission(code: string): string[] {
    return this.roles
      .filter(r => r.permissions.some(p => p.toLowerCase() === code.toLowerCase()))
      .map(r => r.name);
  }

  roleLabel(role: UserRole): string {
    return UserRoleLabels[role] ?? String(role);
  }

  goBack(): void {
    this.router.navigate(['/platform/tenants']);
  }
}
