import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { forkJoin, of, switchMap } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { UserService } from '../../../core/services/user.service';
import { PlatformService } from '../../../core/services/platform.service';
import {
  User,
  UserRole,
  UserRoleLabels,
  UserRoleDescriptions,
  CreateUserDto,
  UpdateUserDto,
  parseUserRole,
  USER_LIFECYCLE_STATUSES,
  EMPLOYEE_TYPES,
  AssignedRole
} from '../../../core/models/user.model';
import { Branch, Department, RoleSummary, EffectivePermission, WorkspaceDefinition, DashboardDefinition, CompanyDataScope } from '../../../core/models/platform.model';

@Component({
  standalone: false,
  selector: 'app-user-form',
  templateUrl: './user-form.component.html',
  styleUrls: ['./user-form.component.scss']
})
export class UserFormComponent implements OnInit {
  form: FormGroup;
  loading = false;
  submitting = false;
  isEdit = false;
  userId: number | null = null;
  hidePassword = true;
  companyName = '';
  branches: Branch[] = [];
  departments: Department[] = [];
  assignableRoles: RoleSummary[] = [];
  workspaces: WorkspaceDefinition[] = [];
  dashboards: DashboardDefinition[] = [];
  selectedRoleIds = new Set<number>();
  effectivePermissions: EffectivePermission[] = [];
  effectiveCategoryChips: string[] = [];
  dataScope: CompanyDataScope | null = null;

  readonly roles = [
    UserRole.Admin,
    UserRole.Dispatcher,
    UserRole.Driver,
    UserRole.Accountant
  ];
  readonly lifecycleStatuses = USER_LIFECYCLE_STATUSES;
  readonly employeeTypes = EMPLOYEE_TYPES;
  readonly themes = ['light', 'dark', 'system'];
  readonly languages = ['en', 'ar'];

  constructor(
    private fb: FormBuilder,
    private userService: UserService,
    private platform: PlatformService,
    private route: ActivatedRoute,
    private router: Router,
    private toast: UiToastService) {
    this.form = this.fb.group({
      fullName: ['', [Validators.required, Validators.maxLength(100)]],
      email:    ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(6)]],
      phone:    ['', [Validators.required]],
      role:     [UserRole.Dispatcher, Validators.required],
      isActive: [true],
      status: ['Active'],
      branchId: [null as number | null],
      departmentId: [null as number | null],
      jobTitle: [''],
      employeeCode: [''],
      employeeType: [null as string | null],
      defaultWorkspaceKey: [''],
      defaultDashboardKey: [''],
      homeRoute: [''],
      timeZone: [''],
      language: ['en'],
      theme: ['system'],
      avatarUrl: ['']
    });
  }

  ngOnInit(): void {
    this.route.paramMap.subscribe(params => {
      const id = params.get('id');
      this.loading = true;

      forkJoin({
        branches: this.platform.getBranches().pipe(catchError(() => of([] as Branch[]))),
        departments: this.platform.getDepartments().pipe(catchError(() => of([] as Department[]))),
        roles: this.platform.getCompanyRoles().pipe(catchError(() => of([] as RoleSummary[]))),
        workspaces: this.platform.getWorkspaceCatalog().pipe(catchError(() => of([] as WorkspaceDefinition[]))),
        dashboards: this.platform.getDashboardCatalog(true).pipe(catchError(() => of([] as DashboardDefinition[]))),
        user: id
          ? this.userService.getById(+id)
          : of(null as User | null),
        assigned: id
          ? this.userService.getUserRoles(+id).pipe(catchError(() => of([] as AssignedRole[])))
          : of([] as AssignedRole[]),
        effective: id
          ? this.userService.getUserPermissions(+id).pipe(catchError(() => of([] as EffectivePermission[])))
          : of([] as EffectivePermission[]),
        dataScope: id
          ? this.userService.getUserDataScope(+id).pipe(catchError(() => of(null as CompanyDataScope | null)))
          : of(null as CompanyDataScope | null)
      }).subscribe({
        next: ({ branches, departments, roles, workspaces, dashboards, user, assigned, effective, dataScope }) => {
          this.branches = branches;
          this.departments = departments;
          this.assignableRoles = roles;
          this.workspaces = (workspaces ?? []).filter(w => w.isActive && w.visible);
          this.dashboards = (dashboards ?? []).filter(d => d.isActive && d.visible);
          this.selectedRoleIds = new Set(assigned.map(r => r.roleId));
          this.effectivePermissions = effective;
          this.dataScope = dataScope;
          this.effectiveCategoryChips = [...new Set(
            effective.map(p => p.category).filter((c): c is string => !!c && c.trim().length > 0)
          )].sort((a, b) => a.localeCompare(b)).slice(0, 8);

          if (user) {
            this.isEdit = true;
            this.userId = user.id;
            this.companyName = user.companyName || '';
            this.form.get('password')?.clearValidators();
            this.form.get('password')?.updateValueAndValidity();
            if (user.assignedRoles?.length) {
              this.selectedRoleIds = new Set(user.assignedRoles.map(r => r.roleId));
            }
            this.form.patchValue({
              fullName: user.fullName,
              email: user.email,
              phone: user.phone,
              role: parseUserRole(user.role),
              isActive: user.isActive,
              status: user.status || (user.isActive ? 'Active' : 'Inactive'),
              branchId: user.branchId ?? null,
              departmentId: user.departmentId ?? null,
              jobTitle: user.jobTitle || '',
              employeeCode: user.employeeCode || '',
              employeeType: user.employeeType || null,
              defaultWorkspaceKey: user.defaultWorkspaceKey || '',
              defaultDashboardKey: user.defaultDashboardKey || '',
              homeRoute: user.homeRoute || '',
              timeZone: user.timeZone || '',
              language: user.language || 'en',
              theme: user.theme || 'system',
              avatarUrl: user.avatarUrl || ''
            });
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

  isRoleSelected(roleId: number): boolean {
    return this.selectedRoleIds.has(roleId);
  }

  togglePlatformRole(roleId: number): void {
    if (this.selectedRoleIds.has(roleId)) {
      this.selectedRoleIds.delete(roleId);
    } else {
      this.selectedRoleIds.add(roleId);
    }
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting = true;
    const f = this.form.value;
    const orgFields = {
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
      avatarUrl: f.avatarUrl?.trim() || null
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
        email:    f.email.trim(),
        phone:    f.phone.trim(),
        role:     Number(f.role),
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
        email:    f.email.trim(),
        password: f.password,
        phone:    f.phone.trim(),
        role:     Number(f.role),
        ...orgFields
      };

      this.userService.create({ user: payload }).pipe(
        switchMap((id) => {
          if (!roleIds.length) {
            return of(true);
          }
          return this.userService.setUserRoles(id, { roleIds, scopes });
        })
      ).subscribe({
        next: () => this.onSuccess('created'),
        error: (err) => this.onError(err)
      });
    }
  }

  roleLabel(r: UserRole): string {
    return UserRoleLabels[r] ?? 'Unknown';
  }

  roleDescription(r: UserRole): string {
    return UserRoleDescriptions[r] ?? '';
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
