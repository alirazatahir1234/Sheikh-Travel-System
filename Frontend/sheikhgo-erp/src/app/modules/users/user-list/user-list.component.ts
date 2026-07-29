import {
  User,
  UserRole,
  UserRoleLabels,
  USER_LIFECYCLE_STATUSES,
  EMPLOYEE_TYPES
} from '../../../core/models/user.model';
import { PlatformService } from '../../../core/services/platform.service';
import { AuthService } from '../../../core/services/auth.service';
import { UserService } from '../../../core/services/user.service';
import { ExportService, ExportColumn } from '../../../core/services/export.service';
import { exportDocumentTitle } from '../../../core/constants/app-brand';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { Router, ActivatedRoute } from '@angular/router';
import { DatePipe } from '@angular/common';
import { SelectionModel } from '@angular/cdk/collections';
import { catchError, forkJoin, of } from 'rxjs';
import { AfterViewInit, ChangeDetectorRef, Component, OnInit, ViewChild } from '@angular/core';
import { MatTableDataSource } from '@angular/material/table';
import { MatPaginator } from '@angular/material/paginator';
import { MatSort } from '@angular/material/sort';
import { MatDialog } from '@angular/material/dialog';
import {
  BulkUserImportDialogComponent,
  BulkUserImportDialogResult
} from '../bulk-user-import-dialog/bulk-user-import-dialog.component';
import {
  USER_IMPORT_SAMPLE,
  getUserImportExportColumns,
  userToImportRow
} from '../user-import-format';
import { PRIMARY_ROLE_CATALOG } from '../user-primary-roles';

@Component({
  standalone: false,
  selector: 'app-user-list',
  templateUrl: './user-list.component.html',
  styleUrls: ['./user-list.component.scss'],
  providers: [DatePipe]
})
export class UserListComponent implements OnInit, AfterViewInit {
  displayedColumns: string[] = [
    'select',
    'fullName',
    'role',
    'status',
    'lastLoginAt',
    'createdAt',
    'branchName',
    'departmentName',
    'employeeType',
    'actions'
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
  private readonly statusOrder: Record<string, number> = {
    Active: 1,
    Pending: 2,
    Suspended: 3,
    Locked: 4,
    Inactive: 5
  };

  @ViewChild(MatPaginator) paginator!: MatPaginator;
  @ViewChild(MatSort) sort!: MatSort;

  constructor(
    private userService: UserService,
    private platform: PlatformService,
    private auth: AuthService,
    private exportService: ExportService,
    private router: Router,
    private route: ActivatedRoute,
    private toast: UiToastService,
    private datePipe: DatePipe,
    private cdr: ChangeDetectorRef,
    private dialog: MatDialog
  ) {
    this.dataSource.sortingDataAccessor = (user, column) => {
      switch (column) {
        case 'fullName':
          return (user.fullName || '').toLowerCase();
        case 'role':
          return this.primaryRoleLabel(user).toLowerCase();
        case 'status':
          return this.statusOrder[this.lifecycleStatus(user)] ?? 999;
        case 'createdAt':
          return user.createdAt ? new Date(user.createdAt).getTime() : 0;
        case 'lastLoginAt':
          return user.lastLoginAt ? new Date(user.lastLoginAt).getTime() : 0;
        default:
          return '';
      }
    };
  }

  ngOnInit(): void {
    const searchFromUrl = this.route.snapshot.queryParamMap.get('search');
    if (searchFromUrl?.trim()) {
      this.searchTerm = searchFromUrl.trim();
    }
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

  ngAfterViewInit(): void {
    this.wireTableControls();
  }

  get kpiTotal(): number {
    return this.allUsers.length;
  }

  get kpiActive(): number {
    return this.allUsers.filter(u => this.lifecycleStatus(u) === 'Active').length;
  }

  get kpiInactive(): number {
    return this.allUsers.filter(u => this.lifecycleStatus(u) === 'Inactive').length;
  }

  get kpiSuspended(): number {
    return this.allUsers.filter(u => this.lifecycleStatus(u) === 'Suspended').length;
  }

  get kpiLocked(): number {
    return this.allUsers.filter(u => this.lifecycleStatus(u) === 'Locked').length;
  }

  get pageRangeStart(): number {
    if (!this.paginator || !this.dataSource.data.length) return 0;
    return this.paginator.pageIndex * this.paginator.pageSize + 1;
  }

  get pageRangeEnd(): number {
    if (!this.paginator || !this.dataSource.data.length) return 0;
    return Math.min(
      (this.paginator.pageIndex + 1) * this.paginator.pageSize,
      this.dataSource.data.length
    );
  }

  load(): void {
    this.loading = true;
    this.selection.clear();
    this.userService.getAll(1, 500).subscribe({
      next: result => {
        this.allUsers = result.items;
        this.loading = false;
        this.cdr.detectChanges();
        this.applyFilters();
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
      filtered = filtered.filter(u => this.lifecycleStatus(u) === this.statusFilter);
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
        (u.employeeCode || '').toLowerCase().includes(q) ||
        (u.branchName || '').toLowerCase().includes(q) ||
        (u.departmentName || '').toLowerCase().includes(q) ||
        (u.jobTitle || '').toLowerCase().includes(q) ||
        (u.employeeType || '').toLowerCase().includes(q) ||
        this.primaryRoleLabel(u).toLowerCase().includes(q)
      );
    }

    this.rebuildDisplayedColumns(filtered);
    this.dataSource.data = filtered;
    this.cdr.detectChanges();
    this.wireTableControls();
    if (this.paginator) {
      this.paginator.length = filtered.length;
      const maxPage = Math.max(0, Math.ceil(filtered.length / this.paginator.pageSize) - 1);
      if (this.paginator.pageIndex > maxPage) {
        this.paginator.firstPage();
      }
    }
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

  primaryRoleLabel(user: User): string {
    const roleCode = user.assignedRoles?.[0]?.code?.toUpperCase();
    if (roleCode) {
      const fromCatalog = PRIMARY_ROLE_CATALOG.find(r => r.platformCode === roleCode || r.id === roleCode);
      if (fromCatalog) return fromCatalog.label;
      const fromAssigned = user.assignedRoles?.[0]?.displayName || user.assignedRoles?.[0]?.name;
      if (fromAssigned) return fromAssigned;
      return roleCode.replace(/_/g, ' ');
    }
    if (user.role === UserRole.Admin) return 'Tenant Admin';
    return this.roleLabel(user.role);
  }

  employeeTypeLabel(user: User): string {
    const value = (user.employeeType || '').trim();
    if (value) return value;
    return user.role === UserRole.Driver ? 'Driver' : 'Staff';
  }

  lifecycleStatus(user: User): string {
    return user.status || (user.isActive ? 'Active' : 'Inactive');
  }

  formatCreated(value?: string | null): string {
    if (!value) return '—';
    return this.datePipe.transform(value, 'dd MMM yyyy') || '—';
  }

  formatLastLogin(value?: string | null): string {
    if (!value) return 'Never Logged In';
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return 'Never Logged In';

    const now = new Date();
    const startOfToday = new Date(now.getFullYear(), now.getMonth(), now.getDate());
    const startOfThatDay = new Date(date.getFullYear(), date.getMonth(), date.getDate());
    const dayDiff = Math.floor((startOfToday.getTime() - startOfThatDay.getTime()) / 86_400_000);
    const time = this.datePipe.transform(date, 'h:mm a') || '';

    if (dayDiff === 0) return `Today ${time}`.trim();
    if (dayDiff === 1) return `Yesterday ${time}`.trim();
    if (dayDiff > 1 && dayDiff < 7) return `${dayDiff} days ago`;
    return this.datePipe.transform(date, 'dd MMM yyyy') || 'Never Logged In';
  }

  createdMeta(user: User): string {
    return `Created ${this.formatCreated(user.createdAt)} by System Admin`;
  }

  get totalPages(): number {
    if (!this.paginator || !this.dataSource.data.length) return 1;
    return Math.max(1, Math.ceil(this.dataSource.data.length / this.paginator.pageSize));
  }

  get currentPage(): number {
    if (!this.paginator) return 1;
    return this.paginator.pageIndex + 1;
  }

  edit(id: number): void {
    this.router.navigate(['/users', id, 'edit']);
  }

  toggleStatus(user: User): void {
    const newStatus = !user.isActive;
    this.userService.updateStatus({ id: user.id, isActive: newStatus, status: newStatus ? 'Active' : 'Inactive' }).subscribe({
      next: () => {
        user.isActive = newStatus;
        user.status = newStatus ? 'Active' : 'Inactive';
        this.toast.success(`User ${newStatus ? 'activated' : 'deactivated'}.`);
      },
      error: () => this.toast.error('Status update failed.')
    });
  }

  setLifecycleStatus(user: User, status: 'Active' | 'Suspended' | 'Locked' | 'Inactive'): void {
    const isActive = status === 'Active';
    this.userService.updateStatus({ id: user.id, isActive, status }).subscribe({
      next: () => {
        user.isActive = isActive;
        user.status = status;
        this.toast.success(`Status updated to ${status}.`);
      },
      error: () => this.toast.error('Status update failed.')
    });
  }

  resetPassword(user: User): void {
    if (!confirm(`Reset password for "${user.fullName}"? They will need to use the new temporary password.`)) return;

    this.userService.resetPassword(user.id).subscribe({
      next: result => {
        this.toast.success(`Password reset. Temporary: ${result.temporaryPassword}`);
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

  bulkActivate(): void {
    this.bulkUpdateStatus(true);
  }

  bulkDeactivate(): void {
    this.bulkUpdateStatus(false);
  }

  bulkResetPassword(): void {
    const selected = this.selection.selected;
    if (selected.length === 0) return;
    if (!confirm(`Reset password for ${selected.length} user(s)?`)) return;

    let ok = 0;
    let fail = 0;
    let remaining = selected.length;

    selected.forEach(u => {
      this.userService.resetPassword(u.id).subscribe({
        next: () => {
          ok++;
          if (--remaining === 0) this.toast.success(`Password reset for ${ok} user(s)${fail ? `; ${fail} failed` : ''}.`);
        },
        error: () => {
          fail++;
          if (--remaining === 0) {
            if (ok) this.toast.success(`Password reset for ${ok} user(s); ${fail} failed.`);
            else this.toast.error('Password reset failed.');
          }
        }
      });
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

  exportSelectedExcel(): void {
    const rows = this.selection.selected;
    if (!rows.length) return;
    this.exportService.exportExcel(rows, this.getColumns(), { filename: 'users-selected', sheetName: 'Users' });
  }

  exportSelectedPdf(): void {
    const rows = this.selection.selected;
    if (!rows.length) return;
    this.exportService.exportPdf(rows, this.getColumns(), {
      filename: 'users-selected',
      title: exportDocumentTitle('Users (Selected)')
    });
  }

  isAllSelected(): boolean {
    return this.selection.selected.length === this.dataSource.filteredData.length
      && this.dataSource.filteredData.length > 0;
  }

  toggleAllRows(): void {
    this.isAllSelected()
      ? this.selection.clear()
      : this.dataSource.filteredData.forEach(row => this.selection.select(row));
  }

  exportExcel(): void {
    this.exportService.exportExcel(this.dataSource.filteredData, this.getColumns(), {
      filename: 'users',
      sheetName: 'Users'
    });
  }

  exportPdf(): void {
    this.exportService.exportPdf(this.dataSource.filteredData, this.getColumns(), {
      filename: 'users',
      title: exportDocumentTitle('Users')
    });
  }

  exportCsv(): void {
    this.exportService.exportCsv(this.dataSource.filteredData, this.getColumns(), {
      filename: 'users',
      sheetName: 'Users'
    });
  }

  printTable(): void {
    window.print();
  }

  downloadImportTemplateCsv(): void {
    this.exportService.exportCsv([USER_IMPORT_SAMPLE], getUserImportExportColumns(), {
      filename: 'users-import-template',
      sheetName: 'Users'
    });
  }

  downloadImportTemplateExcel(): void {
    this.exportService.exportExcel([USER_IMPORT_SAMPLE], getUserImportExportColumns(), {
      filename: 'users-import-template',
      sheetName: 'Users',
      title: 'Users import template'
    });
  }

  exportImportFormatCsv(): void {
    const rows = this.dataSource.filteredData.map(u => userToImportRow(u));
    if (!rows.length) {
      this.toast.error('No users to export.');
      return;
    }
    this.exportService.exportCsv(rows, getUserImportExportColumns(), {
      filename: 'users-import-format',
      sheetName: 'Users'
    });
  }

  exportImportFormatExcel(): void {
    const rows = this.dataSource.filteredData.map(u => userToImportRow(u));
    if (!rows.length) {
      this.toast.error('No users to export.');
      return;
    }
    this.exportService.exportExcel(rows, getUserImportExportColumns(), {
      filename: 'users-import-format',
      sheetName: 'Users',
      title: 'Users (import format)'
    });
  }

  openImportDialog(): void {
    forkJoin({
      roles: this.platform.getCompanyRoles().pipe(catchError(() => of([])))
    }).subscribe(({ roles }) => {
      const ref = this.dialog.open(BulkUserImportDialogComponent, {
        width: '980px',
        maxWidth: '98vw',
        data: {
          branches: this.branches,
          departments: this.departments,
          assignableRoles: roles,
          isPlatformSuperAdmin: this.auth.hasRole('SUPER_ADMIN') || this.auth.hasRole('SuperAdmin'),
          existingEmails: this.allUsers.map(u => u.email),
          existingPhones: this.allUsers.map(u => u.phone || ''),
          existingEmployeeCodes: this.allUsers
            .map(u => u.employeeCode || '')
            .filter(c => !!c)
        }
      });
      ref.afterClosed().subscribe((result?: BulkUserImportDialogResult) => {
        if (result && result.created > 0) {
          this.load();
        }
      });
    });
  }

  private bulkUpdateStatus(isActive: boolean): void {
    const selected = this.selection.selected;
    if (selected.length === 0) return;

    const label = isActive ? 'activate' : 'deactivate';
    if (!confirm(`${label.charAt(0).toUpperCase() + label.slice(1)} ${selected.length} user(s)?`)) return;

    let remaining = selected.length;
    selected.forEach(u => {
      this.userService.updateStatus({ id: u.id, isActive }).subscribe({
        next: () => {
          u.isActive = isActive;
          u.status = isActive ? 'Active' : 'Inactive';
          if (--remaining === 0) {
            this.selection.clear();
            this.toast.success(`Users ${label}d.`);
          }
        },
        error: () => {
          if (--remaining === 0) {
            this.toast.error(`Some users could not be ${label}d.`);
          }
        }
      });
    });
  }

  private rebuildDisplayedColumns(rows: User[]): void {
    const cols = ['select', 'fullName', 'role', 'status', 'lastLoginAt', 'createdAt', 'branchName', 'departmentName', 'employeeType'];
    if (rows.some(u => !!u.defaultWorkspaceKey?.trim())) cols.push('defaultWorkspaceKey');
    cols.push('actions');
    this.displayedColumns = cols;
  }

  private wireTableControls(): void {
    if (this.paginator) {
      this.dataSource.paginator = this.paginator;
      this.paginator.length = this.dataSource.data.length;
    }
    if (this.sort) {
      this.dataSource.sort = this.sort;
    }
  }

  private getColumns(): ExportColumn<User>[] {
    return [
      { header: 'Name', accessor: (u: User) => u.fullName, excelWidth: 24, pdfWeight: 2 },
      { header: 'Email', accessor: (u: User) => u.email, excelWidth: 28, pdfWeight: 2.2 },
      { header: 'Primary Role', accessor: (u: User) => this.primaryRoleLabel(u), excelWidth: 18, pdfWeight: 1 },
      {
        header: 'Status',
        accessor: (u: User) => this.lifecycleStatus(u),
        align: 'center',
        excelWidth: 12,
        pdfWeight: 0.9
      },
      { header: 'Created', accessor: (u: User) => this.formatCreated(u.createdAt), excelWidth: 14, pdfWeight: 1 },
      {
        header: 'Last Login',
        accessor: (u: User) => this.formatLastLogin(u.lastLoginAt),
        excelWidth: 16,
        pdfWeight: 1.2
      },
      { header: 'Branch', accessor: (u: User) => u.branchName || '', excelWidth: 16, pdfWeight: 1.2 },
      { header: 'Department', accessor: (u: User) => u.departmentName || '', excelWidth: 16, pdfWeight: 1.2 },
      { header: 'Employee Type', accessor: (u: User) => this.employeeTypeLabel(u), excelWidth: 14, pdfWeight: 1 },
      { header: 'Workspace', accessor: (u: User) => u.defaultWorkspaceKey || '', excelWidth: 14, pdfWeight: 1 },
      { header: 'Employee ID', accessor: (u: User) => u.employeeCode || '', excelWidth: 14, pdfWeight: 1 }
    ];
  }
}
