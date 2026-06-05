import { Component, OnInit, ViewChild } from '@angular/core';
import { MatTableDataSource } from '@angular/material/table';
import { MatPaginator } from '@angular/material/paginator';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatDialog } from '@angular/material/dialog';
import { Router } from '@angular/router';
import { DatePipe } from '@angular/common';
import { SelectionModel } from '@angular/cdk/collections';
import { UserService } from '../../../core/services/user.service';
import { ExportService, ExportColumn } from '../../../core/services/export.service';
import {
  User,
  UserRole,
  UserRoleLabels
} from '../../../core/models/user.model';

@Component({
  selector: 'app-user-list',
  templateUrl: './user-list.component.html',
  styleUrls: ['./user-list.component.scss'],
  providers: [DatePipe]
})
export class UserListComponent implements OnInit {
  displayedColumns = ['select', 'fullName', 'email', 'phone', 'role', 'isActive', 'createdAt', 'actions'];

  dataSource = new MatTableDataSource<User>();
  selection = new SelectionModel<User>(true, []);
  loading = true;
  allUsers: User[] = [];

  searchTerm = '';
  roleFilter: UserRole | 'ALL' = 'ALL';
  statusFilter: 'ALL' | 'ACTIVE' | 'INACTIVE' = 'ALL';

  readonly roles = [UserRole.Admin, UserRole.Dispatcher, UserRole.Driver, UserRole.Accountant];

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  constructor(
    private userService: UserService,
    private exportService: ExportService,
    private router: Router,
    private snackBar: MatSnackBar,
    private dialog: MatDialog,
    private datePipe: DatePipe
  ) {}

  ngOnInit(): void {
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
        this.snackBar.open('Failed to load users.', 'Close', { duration: 3000 });
      }
    });
  }

  applyFilters(): void {
    let filtered = [...this.allUsers];

    if (this.roleFilter !== 'ALL') {
      filtered = filtered.filter(u => u.role === this.roleFilter);
    }

    if (this.statusFilter === 'ACTIVE') {
      filtered = filtered.filter(u => u.isActive);
    } else if (this.statusFilter === 'INACTIVE') {
      filtered = filtered.filter(u => !u.isActive);
    }

    if (this.searchTerm.trim()) {
      const q = this.searchTerm.trim().toLowerCase();
      filtered = filtered.filter(u =>
        u.fullName.toLowerCase().includes(q) ||
        u.email.toLowerCase().includes(q) ||
        u.phone.toLowerCase().includes(q) ||
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

  onStatusFilterChange(status: 'ALL' | 'ACTIVE' | 'INACTIVE'): void {
    this.statusFilter = status;
    this.applyFilters();
  }

  clearFilters(): void {
    this.searchTerm = '';
    this.roleFilter = 'ALL';
    this.statusFilter = 'ALL';
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
        this.snackBar.open(
          `User ${newStatus ? 'activated' : 'deactivated'}.`,
          'Close',
          { duration: 2000 }
        );
      },
      error: () => this.snackBar.open('Status update failed.', 'Close', { duration: 3000 })
    });
  }

  resetPassword(user: User): void {
    if (!confirm(`Reset password for "${user.fullName}"? They will need to use the new temporary password.`)) return;

    this.userService.resetPassword(user.id).subscribe({
      next: result => {
        this.snackBar.open(
          `Password reset. Temporary: ${result.temporaryPassword}`,
          'Copy',
          { duration: 15000 }
        );
      },
      error: () => this.snackBar.open('Password reset failed.', 'Close', { duration: 3000 })
    });
  }

  delete(user: User): void {
    if (!confirm(`Delete user "${user.fullName}"? This cannot be undone.`)) return;

    this.userService.delete(user.id).subscribe({
      next: () => {
        this.snackBar.open('User deleted.', 'Close', { duration: 2000 });
        this.load();
      },
      error: () => this.snackBar.open('Delete failed.', 'Close', { duration: 3000 })
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
    this.exportService.exportPdf(this.dataSource.filteredData, this.getColumns(), { filename: 'users', title: 'Sheikh Travel – Users' });
  }

  private getColumns(): ExportColumn<User>[] {
    return [
      { header: 'Name',    accessor: (u: User) => u.fullName,                                        excelWidth: 24, pdfWeight: 2   },
      { header: 'Email',   accessor: (u: User) => u.email,                                           excelWidth: 28, pdfWeight: 2.2 },
      { header: 'Phone',   accessor: (u: User) => u.phone,                                           excelWidth: 16, pdfWeight: 1.3 },
      { header: 'Role',    accessor: (u: User) => this.roleLabel(u.role),                            excelWidth: 14, pdfWeight: 1   },
      { header: 'Status',  accessor: (u: User) => u.isActive ? 'Active' : 'Inactive', align: 'center', excelWidth: 10, pdfWeight: 0.7 },
      { header: 'Created', accessor: (u: User) => this.datePipe.transform(u.createdAt, 'mediumDate') ?? '',
                                                                                              excelWidth: 14, pdfWeight: 1   }
    ];
  }
}
