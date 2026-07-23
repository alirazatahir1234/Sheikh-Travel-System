import {
  User,
  UserRole,
  UserRoleLabels,
  USER_LIFECYCLE_STATUSES,
  EMPLOYEE_TYPES
} from '../../../core/models/user.model';
import { PlatformService } from '../../../core/services/platform.service';
import { UserService } from '../../../core/services/user.service';
import { ExportService, ExportColumn } from '../../../core/services/export.service';
import { exportDocumentTitle } from '../../../core/constants/app-brand';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { Router } from '@angular/router';
import { DatePipe } from '@angular/common';
import { SelectionModel } from '@angular/cdk/collections';
import { Component, OnInit, ViewChild } from '@angular/core';
import { MatTableDataSource } from '@angular/material/table';
import { MatPaginator } from '@angular/material/paginator';
import { MatDialog } from '@angular/material/dialog';

@Component({
  standalone: false,
  selector: 'app-user-list',
  templateUrl: './user-list.component.html',
  styleUrls: ['./user-list.component.scss'],
  providers: [DatePipe]
})
export class UserListComponent implements OnInit {
  displayedColumns = [
    'select', 'fullName', 'email', 'branchName', 'departmentName',
    'employeeType', 'status', 'defaultWorkspaceKey', 'role', 'isActive', 'actions'
  ];

  dataSource = new MatTableDataSource<User>();
  selection = new SelectionModel<User>(true, []);
  loading = true;
  allUsers: User[] = [];

  searchTerm = '';
  roleFilter: UserRole | 'ALL' = 'ALL';
  statusFilter: string = 'ALL';
  employeeTypeFilter: string = 'ALL';
  branchFilter: number | 'ALL' = 'ALL';
  departmentFilter: number | 'ALL' = 'ALL';

  readonly roles = [UserRole.Admin, UserRole.Dispatcher, UserRole.Driver, UserRole.Accountant];
  readonly lifecycleStatuses = USER_LIFECYCLE_STATUSES;
  readonly employeeTypes = EMPLOYEE_TYPES;
  branches: { id: number; name: string }[] = [];
  departments: { id: number; name: string }[] = [];

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  constructor(
    private userService: UserService,
    private platform: PlatformService,
    private exportService: ExportService,
    private router: Router,
    private toast: UiToastService,
    private dialog: MatDialog,
    private datePipe: DatePipe
  ) {}

  ngOnInit(): void {
    this.platform.getBranches().subscribe({
      next: rows => { this.branches = rows.map(b => ({ id: b.id, name: b.name })); },
      error: () => { this.branches = []; }
    });
    this.platform.getDepartments().subscribe({
      next: rows => { this.departments = rows.map(d => ({ id: d.id, name: d.name })); },
      error: () => { this.departments = []; }
    });
    this.load();
  }

  load(): void {
    this.loading = true;
    this.selection.clear();
    this.userService.getAll(1, 500).subscribe({
      next: result => {
        this.allUsers = result.items;
        this.applyFilters();
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.toast.error('Failed to load users.');
      }
    });
  }

  applyFilters(): void {
    let filtered = [...this.allUsers];

    if (this.roleFilter !== 'ALL') {
      filtered = filtered.filter(u => u.role === this.roleFilter);
    }

    if (this.statusFilter !== 'ALL') {
      filtered = filtered.filter(u =>
        (u.status || (u.isActive ? 'Active' : 'Inactive')) === this.statusFilter
      );
    }

    if (this.employeeTypeFilter !== 'ALL') {
      filtered = filtered.filter(u => u.employeeType === this.employeeTypeFilter);
    }

    if (this.branchFilter !== 'ALL') {
      filtered = filtered.filter(u => u.branchId === this.branchFilter);
    }

    if (this.departmentFilter !== 'ALL') {
      filtered = filtered.filter(u => u.departmentId === this.departmentFilter);
    }

    if (this.searchTerm.trim()) {
      const q = this.searchTerm.trim().toLowerCase();
      filtered = filtered.filter(u =>
        u.fullName.toLowerCase().includes(q) ||
        u.email.toLowerCase().includes(q) ||
        (u.phone || '').toLowerCase().includes(q) ||
        (u.branchName || '').toLowerCase().includes(q) ||
        (u.departmentName || '').toLowerCase().includes(q) ||
        (u.employeeType || '').toLowerCase().includes(q) ||
        this.roleLabel(u.role).toLowerCase().includes(q)
      );
    }

    this.dataSource.data = filtered;
    setTimeout(() => (this.dataSource.paginator = this.paginator));
  }

  onSearch(term: string): void {
    this.searchTerm = term;
    this.applyFilters();
  }

  onRoleFilterChange(role: UserRole | 'ALL'): void {
    this.roleFilter = role;
    this.applyFilters();
  }

  onStatusFilterChange(status: string): void {
    this.statusFilter = status;
    this.applyFilters();
  }

  onEmployeeTypeFilterChange(type: string): void {
    this.employeeTypeFilter = type;
    this.applyFilters();
  }

  onBranchFilterChange(id: number | 'ALL'): void {
    this.branchFilter = id;
    this.applyFilters();
  }

  onDepartmentFilterChange(id: number | 'ALL'): void {
    this.departmentFilter = id;
    this.applyFilters();
  }

  clearFilters(): void {
    this.searchTerm = '';
    this.roleFilter = 'ALL';
    this.statusFilter = 'ALL';
    this.employeeTypeFilter = 'ALL';
    this.branchFilter = 'ALL';
    this.departmentFilter = 'ALL';
    this.applyFilters();
  }

  roleLabel(r: UserRole): string {
    return UserRoleLabels[r] ?? 'Unknown';
  }

  edit(id: number): void {
    this.router.navigate(['/users', id, 'edit']);
  }

  toggleStatus(user: User): void {
    const newStatus = !user.isActive;
    this.userService.updateStatus({ id: user.id, isActive: newStatus }).subscribe({
      next: () => {
        user.isActive = newStatus;
        this.toast.success(
          `User ${newStatus ? 'activated' : 'deactivated'}.`);
      },
      error: () => this.toast.error('Status update failed.')
    });
  }

  resetPassword(user: User): void {
    if (!confirm(`Reset password for "${user.fullName}"? They will need to use the new temporary password.`)) return;

    this.userService.resetPassword(user.id).subscribe({
      next: result => {
        this.toast.success(
          `Password reset. Temporary: ${result.temporaryPassword}`);
      },
      error: () => this.toast.error('Password reset failed.')
    });
  }

  delete(user: User): void {
    if (!confirm(`Delete user "${user.fullName}"? This cannot be undone.`)) return;

    this.userService.delete(user.id).subscribe({
      next: () => {
        this.toast.success('User deleted.');
        this.load();
      },
      error: () => this.toast.error('Delete failed.')
    });
  }

  bulkDelete(): void {
    const selected = this.selection.selected;
    if (selected.length === 0) return;
    if (!confirm(`Delete ${selected.length} user(s)?`)) return;

    let remaining = selected.length;
    selected.forEach(u => {
      this.userService.delete(u.id).subscribe({
        next: () => { if (--remaining === 0) this.load(); },
        error: () => { if (--remaining === 0) this.load(); }
      });
    });
  }

  // Selection helpers
  isAllSelected(): boolean {
    return this.selection.selected.length === this.dataSource.filteredData.length && this.dataSource.filteredData.length > 0;
  }
  toggleAllRows(): void {
    this.isAllSelected()
      ? this.selection.clear()
      : this.dataSource.filteredData.forEach(row => this.selection.select(row));
  }

  // Export
  exportExcel(): void {
    this.exportService.exportExcel(this.dataSource.filteredData, this.getColumns(), { filename: 'users', sheetName: 'Users' });
  }
  exportPdf(): void {
    this.exportService.exportPdf(this.dataSource.filteredData, this.getColumns(), { filename: 'users', title: exportDocumentTitle('Users') });
  }

  private getColumns(): ExportColumn<User>[] {
    return [
      { header: 'Name',    accessor: (u: User) => u.fullName,                                        excelWidth: 24, pdfWeight: 2   },
      { header: 'Email',   accessor: (u: User) => u.email,                                           excelWidth: 28, pdfWeight: 2.2 },
      { header: 'Branch',  accessor: (u: User) => u.branchName || '',                                excelWidth: 16, pdfWeight: 1.2 },
      { header: 'Department', accessor: (u: User) => u.departmentName || '',                         excelWidth: 16, pdfWeight: 1.2 },
      { header: 'Type',    accessor: (u: User) => u.employeeType || '',                              excelWidth: 12, pdfWeight: 1 },
      { header: 'Status',  accessor: (u: User) => u.status || (u.isActive ? 'Active' : 'Inactive'), align: 'center', excelWidth: 12, pdfWeight: 0.9 },
      { header: 'Role',    accessor: (u: User) => this.roleLabel(u.role),                            excelWidth: 14, pdfWeight: 1 },
      { header: 'Workspace', accessor: (u: User) => u.defaultWorkspaceKey || '',                     excelWidth: 14, pdfWeight: 1 }
    ];
  }
}
