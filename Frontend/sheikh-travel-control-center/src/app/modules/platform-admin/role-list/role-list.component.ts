import { Component, OnInit } from '@angular/core';
import { PlatformService } from '../../../core/services/platform.service';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { Permission, PlatformRole } from '../../../core/models/platform.model';
import { apiErrorMessage } from '../../../core/utils/api-error.util';

@Component({
  selector: 'app-role-list',
  templateUrl: './role-list.component.html',
  styleUrls: ['./role-list.component.scss']
})
export class RoleListComponent implements OnInit {
  loading = true;
  roles: PlatformRole[] = [];
  permissions: Permission[] = [];
  selectedRole?: PlatformRole;
  selectedPermissionCodes = new Set<string>();
  newName = '';
  newCode = '';

  constructor(private platform: PlatformService, private toast: UiToastService) {}

  ngOnInit(): void {
    this.platform.getPermissions().subscribe(perms => this.permissions = perms);
    this.load();
  }

  load(): void {
    this.loading = true;
    this.platform.getRoles().subscribe({
      next: rows => { this.roles = rows; this.loading = false; },
      error: (err) => { this.loading = false; this.toast.error(apiErrorMessage(err, 'Failed to load roles.')); }
    });
  }

  create(): void {
    if (!this.newName.trim() || !this.newCode.trim()) {
      this.toast.warning('Role name and code are required.');
      return;
    }
    this.platform.createRole(this.newName.trim(), this.newCode.trim()).subscribe({
      next: () => { this.newName = ''; this.newCode = ''; this.load(); },
      error: (err) => this.toast.error(apiErrorMessage(err, 'Create failed.'))
    });
  }

  selectRole(role: PlatformRole): void {
    this.selectedRole = role;
    this.selectedPermissionCodes = new Set(role.permissions);
  }

  togglePermission(code: string): void {
    if (this.selectedPermissionCodes.has(code)) this.selectedPermissionCodes.delete(code);
    else this.selectedPermissionCodes.add(code);
  }

  savePermissions(): void {
    if (!this.selectedRole) return;
    this.platform.updateRolePermissions(this.selectedRole.id, [...this.selectedPermissionCodes]).subscribe({
      next: () => {
        this.selectedRole!.permissions = [...this.selectedPermissionCodes];
        this.toast.success('Permissions saved.');
        this.load();
      },
      error: (err) => this.toast.error(apiErrorMessage(err, 'Save failed.'))
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
}
