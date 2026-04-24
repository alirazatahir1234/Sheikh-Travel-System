import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { HttpErrorResponse } from '@angular/common/http';
import { UserService } from '../../../core/services/user.service';
import {
  User,
  UserRole,
  UserRoleLabels,
  UserRoleDescriptions,
  CreateUserDto,
  UpdateUserDto
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
    private snackBar: MatSnackBar
  ) {
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
            role:     user.role,
            isActive: user.isActive
          });
          this.loading = false;
        },
        error: () => {
          this.loading = false;
          this.snackBar.open('Failed to load user.', 'Close', { duration: 3000 });
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
    this.snackBar.open(`User ${action} successfully.`, 'Close', { duration: 2000 });
    this.router.navigate(['/users']);
  }

  private onError(err: HttpErrorResponse): void {
    this.submitting = false;
    this.snackBar.open(this.extractError(err), 'Close', { duration: 4000 });
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

  roleLabel(r: UserRole): string {
    return UserRoleLabels[r] ?? 'Unknown';
  }

  roleDescription(r: UserRole): string {
    return UserRoleDescriptions[r] ?? '';
  }
}
