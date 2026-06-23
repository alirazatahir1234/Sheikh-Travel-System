import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { UserService } from '../../../core/services/user.service';
import {
  User,
  UserRole,
  UserRoleLabels,
  UserRoleDescriptions,
  CreateUserDto,
  UpdateUserDto,
  parseUserRole
} from '../../../core/models/user.model';

@Component({
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

  readonly roles = [
    UserRole.Admin,
    UserRole.Dispatcher,
    UserRole.Driver,
    UserRole.Accountant
  ];

  constructor(
    private fb: FormBuilder,
    private userService: UserService,
    private route: ActivatedRoute,
    private router: Router,
    private toast: UiToastService) {
    this.form = this.fb.group({
      fullName: ['', [Validators.required, Validators.maxLength(100)]],
      email:    ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(6)]],
      phone:    ['', [Validators.required]],
      role:     [UserRole.Dispatcher, Validators.required],
      isActive: [true]
    });
  }

  ngOnInit(): void {
    this.route.paramMap.subscribe(params => {
      const id = params.get('id');
      if (!id) return;

      this.isEdit = true;
      this.userId = +id;
      this.loading = true;

      // Password not required when editing
      this.form.get('password')?.clearValidators();
      this.form.get('password')?.updateValueAndValidity();

      this.userService.getById(this.userId).subscribe({
        next: (user: User) => {
          this.form.patchValue({
            fullName: user.fullName,
            email:    user.email,
            phone:    user.phone,
            role:     parseUserRole(user.role),
            isActive: user.isActive
          });
          this.loading = false;
        },
        error: () => {
          this.loading = false;
          this.toast.error('Failed to load user.');
          this.router.navigate(['/users']);
        }
      });
    });
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting = true;
    const f = this.form.value;

    if (this.isEdit && this.userId) {
      const payload: UpdateUserDto = {
        fullName: f.fullName.trim(),
        email:    f.email.trim(),
        phone:    f.phone.trim(),
        role:     Number(f.role),
        isActive: f.isActive
      };

      this.userService.update({ id: this.userId, user: payload }).subscribe({
        next: () => this.onSuccess('updated'),
        error: (err) => this.onError(err)
      });
    } else {
      const payload: CreateUserDto = {
        fullName: f.fullName.trim(),
        email:    f.email.trim(),
        password: f.password,
        phone:    f.phone.trim(),
        role:     Number(f.role)
      };

      this.userService.create({ user: payload }).subscribe({
        next: () => this.onSuccess('created'),
        error: (err) => this.onError(err)
      });
    }
  }

  private onSuccess(action: string): void {
    this.submitting = false;
    this.toast.success(`User ${action} successfully.`);
    this.router.navigate(['/users']);
  }

  private onError(err: HttpErrorResponse): void {
    this.submitting = false;
    this.toast.error(this.extractError(err));
  }

  private extractError(err: HttpErrorResponse): string {
    const body: any = err?.error;
    if (body?.errors) {
      const flat = Object.values(body.errors).flat();
      if (flat.length) return String(flat[0]);
    }
    if (body?.message) return String(body.message);
    return `Operation failed (${err?.status || 'network'}).`;
  }

  roleLabel(r: UserRole | unknown): string {
    return UserRoleLabels[parseUserRole(r)] ?? 'Unknown';
  }

  roleDescription(r: UserRole | unknown): string {
    return UserRoleDescriptions[parseUserRole(r)] ?? '';
  }

  compareRoles(a: UserRole, b: UserRole): boolean {
    return parseUserRole(a) === parseUserRole(b);
  }
}
