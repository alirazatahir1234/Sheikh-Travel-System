import { Component, OnInit, ViewChild } from '@angular/core';
import { AbstractControl, FormBuilder, ValidationErrors, Validators } from '@angular/forms';
import { MatPaginator } from '@angular/material/paginator';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { MatTableDataSource } from '@angular/material/table';
import { PlatformService } from '../../../core/services/platform.service';
import { UserService } from '../../../core/services/user.service';
import { Department } from '../../../core/models/platform.model';
import { apiErrorMessage } from '../../../core/utils/api-error.util';

type DeptStatusFilter = 'all' | 'active' | 'inactive';

interface DepartmentFilters {
  search: string;
  status: DeptStatusFilter;
}

interface ManagerOption {
  id: number;
  fullName: string;
  isActive: boolean;
}

/** Reject null/empty/whitespace-only names (API also trims). */
function requiredTrimmed(control: AbstractControl): ValidationErrors | null {
  return String(control.value ?? '').trim() ? null : { required: true };
}

@Component({
  standalone: false,
  selector: 'app-department-list',
  templateUrl: './department-list.component.html',
  styleUrls: ['./department-list.component.scss']
})
export class DepartmentListComponent implements OnInit {
  private _paginator?: MatPaginator;

  /** Re-bind whenever *ngIf recreates the paginator after filter changes. */
  @ViewChild(MatPaginator)
  set paginator(p: MatPaginator | undefined) {
    this._paginator = p;
    if (p) {
      this.dataSource.paginator = p;
    }
  }

  loading = true;
  saving = false;
  showFilters = false;
  /** Row id with an in-flight activate/deactivate or delete. */
  busyId: number | null = null;
  /** When set, sidebar form updates this department instead of creating. */
  editingId: number | null = null;
  managers: ManagerOption[] = [];
  departments: Department[] = [];
  dataSource = new MatTableDataSource<Department>([]);
  readonly pageSize = 5;
  readonly displayedColumns = ['name', 'staffCount', 'status', 'actions'];

  filters: DepartmentFilters = {
    search: '',
    status: 'all'
  };

  form;

  constructor(
    private fb: FormBuilder,
    private platform: PlatformService,
    private userService: UserService,
    private toast: UiToastService
  ) {
    this.form = this.fb.group({
      name: ['', [requiredTrimmed, Validators.maxLength(100)]],
      departmentHeadUserId: [null as number | null]
    });
    this.dataSource.filterPredicate = (dept, raw) => this.matchesFilters(dept, raw);
  }

  ngOnInit(): void {
    this.userService.getAll(1, 500).subscribe({
      next: res => {
        // Active users only; inactive/missing heads are injected in startEdit when needed.
        this.managers = res.items
          .filter(u => u.isActive)
          .map(u => ({ id: u.id, fullName: u.fullName, isActive: u.isActive }));
      },
      error: (err) => this.toast.error(apiErrorMessage(err, 'Failed to load managers.'))
    });
    this.load();
  }

  get isEditing(): boolean {
    return this.editingId != null;
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

  get filteredDepartments(): Department[] {
    return this.dataSource.filteredData;
  }

  get filteredCount(): number {
    return this.dataSource.filteredData.length;
  }

  get hasActiveFilters(): boolean {
    return this.filters.status !== 'all' || !!this.filters.search.trim();
  }

  load(): void {
    this.loading = true;
    this.platform.getDepartments().subscribe({
      next: rows => {
        this.departments = rows;
        this.dataSource.data = rows;
        this.applyFilters();
        this.loading = false;
      },
      error: (err) => {
        this.loading = false;
        this.toast.error(apiErrorMessage(err, 'Failed to load departments.'));
      }
    });
  }

  toggleFilters(): void {
    this.showFilters = !this.showFilters;
  }

  applyFilters(): void {
    this.dataSource.filter = JSON.stringify(this.filters);
    if (this._paginator) {
      this._paginator.firstPage();
    }
  }

  clearFilters(): void {
    this.filters = { search: '', status: 'all' };
    this.applyFilters();
  }

  private matchesFilters(dept: Department, raw: string): boolean {
    let f: DepartmentFilters;
    try {
      f = JSON.parse(raw || '{}') as DepartmentFilters;
    } catch {
      return true;
    }

    const q = (f.search || '').trim().toLowerCase();
    if (q) {
      const haystack = `${dept.name} ${dept.departmentHeadName || ''}`.toLowerCase();
      if (!haystack.includes(q)) return false;
    }

    if (f.status === 'active' && !dept.isActive) return false;
    if (f.status === 'inactive' && dept.isActive) return false;

    return true;
  }

  startEdit(dept: Department): void {
    this.editingId = dept.id;
    this.form.reset({
      name: dept.name,
      departmentHeadUserId: dept.departmentHeadUserId ?? null
    });
    // Ensure current head remains selectable even if inactive / not in the user page.
    if (
      dept.departmentHeadUserId != null &&
      !this.managers.some(u => u.id === dept.departmentHeadUserId)
    ) {
      this.managers = [
        ...this.managers,
        {
          id: dept.departmentHeadUserId,
          fullName: dept.departmentHeadName || `User #${dept.departmentHeadUserId}`,
          isActive: false
        }
      ];
    }
    // Mobile: list is above the form — bring the editor into view.
    setTimeout(() => {
      const el = document.getElementById('dept-name');
      el?.scrollIntoView({ behavior: 'smooth', block: 'center' });
      el?.focus();
    });
  }

  cancelEdit(): void {
    this.editingId = null;
    this.form.reset({ name: '', departmentHeadUserId: null });
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      if (this.form.get('name')?.hasError('maxlength')) {
        this.toast.warning('Department name must be 100 characters or fewer.');
      } else {
        this.toast.warning('Department name is required.');
      }
      return;
    }
    if (this.saving) return;

    if (this.editingId != null) {
      this.saveEdit();
    } else {
      this.create();
    }
  }

  private create(): void {
    const v = this.form.getRawValue();
    const payload = {
      name: v.name!.trim(),
      departmentHeadUserId: v.departmentHeadUserId ?? null
    };

    this.saving = true;
    this.platform.createDepartment(payload).subscribe({
      next: () => {
        this.form.reset({ name: '', departmentHeadUserId: null });
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

  private saveEdit(): void {
    const id = this.editingId;
    if (id == null) return;

    const existing = this.departments.find(d => d.id === id);
    if (!existing) {
      this.cancelEdit();
      this.toast.error('Department no longer exists. Refresh and try again.');
      return;
    }

    const v = this.form.getRawValue();
    const payload = {
      name: v.name!.trim(),
      departmentHeadUserId: v.departmentHeadUserId ?? null
    };

    this.saving = true;
    this.platform.updateDepartment(id, payload, existing.isActive).subscribe({
      next: () => {
        this.saving = false;
        this.editingId = null;
        this.form.reset({ name: '', departmentHeadUserId: null });
        this.toast.success('Department updated.');
        this.load();
      },
      error: (err) => {
        this.saving = false;
        this.toast.error(apiErrorMessage(err, 'Update failed.'));
      }
    });
  }

  toggleActive(dept: Department): void {
    if (this.busyId != null) return;
    this.busyId = dept.id;
    this.platform.updateDepartment(dept.id, {
      name: dept.name,
      departmentHeadUserId: dept.departmentHeadUserId ?? null
    }, !dept.isActive).subscribe({
      next: () => {
        dept.isActive = !dept.isActive;
        this.dataSource.data = [...this.departments];
        this.applyFilters();
        this.busyId = null;
        this.toast.success(dept.isActive ? 'Department activated.' : 'Department deactivated.');
      },
      error: (err) => {
        this.busyId = null;
        this.toast.error(apiErrorMessage(err, 'Update failed.'));
      }
    });
  }

  remove(dept: Department): void {
    if (this.busyId != null) return;
    if (!confirm(`Delete department "${dept.name}"?`)) return;
    this.busyId = dept.id;
    this.platform.deleteDepartment(dept.id).subscribe({
      next: () => {
        if (this.editingId === dept.id) {
          this.cancelEdit();
        }
        this.busyId = null;
        this.toast.success('Department deleted.');
        this.load();
      },
      error: (err) => {
        this.busyId = null;
        this.toast.error(apiErrorMessage(err, 'Delete failed.'));
      }
    });
  }

  bulkImportHint(): void {
    this.toast.warning('Bulk import will be available in a future release.');
  }

  managerLabel(u: ManagerOption): string {
    return u.isActive ? u.fullName : `${u.fullName} (inactive)`;
  }

  initials(name: string): string {
    return name.split(/\s+/).slice(0, 2).map(w => w[0]?.toUpperCase() ?? '').join('') || '?';
  }

  avatarColor(name: string): string {
    // Teal-first ERP palette (matches --stb-primary family; avoids purple-on-white drift)
    const palette = ['#0F766E', '#0D6B64', '#1B7F75', '#115E59', '#0E7490', '#155E75'];
    let hash = 0;
    for (let i = 0; i < name.length; i++) hash = name.charCodeAt(i) + ((hash << 5) - hash);
    return palette[Math.abs(hash) % palette.length];
  }
}
