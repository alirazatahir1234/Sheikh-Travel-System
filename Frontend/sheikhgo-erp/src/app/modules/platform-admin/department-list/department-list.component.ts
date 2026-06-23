import { Component, OnInit, ViewChild } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { MatPaginator } from '@angular/material/paginator';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { MatTableDataSource } from '@angular/material/table';
import { PlatformService } from '../../../core/services/platform.service';
import { UserService } from '../../../core/services/user.service';
import { Department } from '../../../core/models/platform.model';
import { User } from '../../../core/models/user.model';
import { apiErrorMessage } from '../../../core/utils/api-error.util';

@Component({
  selector: 'app-department-list',
  templateUrl: './department-list.component.html',
  styleUrls: ['./department-list.component.scss']
})
export class DepartmentListComponent implements OnInit {
  @ViewChild(MatPaginator) paginator?: MatPaginator;

  loading = true;
  saving = false;
  managers: User[] = [];
  departments: Department[] = [];
  dataSource = new MatTableDataSource<Department>([]);
  readonly pageSize = 5;
  readonly displayedColumns = ['name', 'staffCount', 'status', 'actions'];

  form;

  constructor(
    private fb: FormBuilder,
    private platform: PlatformService,
    private userService: UserService,
    private toast: UiToastService
  ) {
    this.form = this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(100)]],
      departmentHeadUserId: [null as number | null]
    });
  }

  ngOnInit(): void {
    this.userService.getAll(1, 500).subscribe({
      next: res => { this.managers = res.items.filter(u => u.isActive); },
      error: () => {}
    });
    this.load();
  }

  get activeCount(): number {
    return this.departments.filter(d => d.isActive).length;
  }

  get addedThisMonth(): number {
    const now = new Date();
    return this.departments.filter(d => {
      const created = new Date(d.createdAt);
      return created.getMonth() === now.getMonth() && created.getFullYear() === now.getFullYear();
    }).length;
  }

  load(): void {
    this.loading = true;
    this.platform.getDepartments().subscribe({
      next: rows => {
        this.departments = rows;
        this.dataSource.data = rows;
        setTimeout(() => { if (this.paginator) this.dataSource.paginator = this.paginator; });
        this.loading = false;
      },
      error: (err) => {
        this.loading = false;
        this.toast.error(apiErrorMessage(err, 'Failed to load departments.'));
      }
    });
  }

  create(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.toast.warning('Department name is required.');
      return;
    }
    if (this.saving) return;

    const v = this.form.getRawValue();
    const payload = {
      name: v.name!.trim(),
      departmentHeadUserId: v.departmentHeadUserId ?? null
    };

    this.saving = true;
    this.platform.createDepartment(payload).subscribe({
      next: () => {
        this.form.reset();
        this.saving = false;
        this.toast.success('Department created.');
        this.load();
      },
      error: (err) => {
        this.saving = false;
        this.toast.error(apiErrorMessage(err, 'Create failed.'));
      }
    });
  }

  toggleActive(dept: Department): void {
    this.platform.updateDepartment(dept.id, {
      name: dept.name,
      departmentHeadUserId: dept.departmentHeadUserId ?? null
    }, !dept.isActive).subscribe({
      next: () => {
        dept.isActive = !dept.isActive;
        this.dataSource.data = [...this.departments];
      },
      error: (err) => this.toast.error(apiErrorMessage(err, 'Update failed.'))
    });
  }

  remove(dept: Department): void {
    if (!confirm(`Delete department "${dept.name}"?`)) return;
    this.platform.deleteDepartment(dept.id).subscribe({
      next: () => this.load(),
      error: (err) => this.toast.error(apiErrorMessage(err, 'Delete failed.'))
    });
  }

  bulkImportHint(): void {
    this.toast.warning('Bulk import will be available in a future release.');
  }

  initials(name: string): string {
    return name.split(/\s+/).slice(0, 2).map(w => w[0]?.toUpperCase() ?? '').join('') || '?';
  }

  avatarColor(name: string): string {
    const palette = ['#166534', '#1d4ed8', '#7c3aed', '#b45309', '#be123c', '#0f766e'];
    let hash = 0;
    for (let i = 0; i < name.length; i++) hash = name.charCodeAt(i) + ((hash << 5) - hash);
    return palette[Math.abs(hash) % palette.length];
  }
}
