import { Component, OnInit, ViewChild } from '@angular/core';
import { MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatSnackBar } from '@angular/material/snack-bar';
import { DatePipe } from '@angular/common';
import { AuditLogService, AuditLogFilter } from '../../../core/services/audit-log.service';
import { UserService } from '../../../core/services/user.service';
import { AuditLog, AuditActions, AuditEntities } from '../../../core/models/audit-log.model';
import { User } from '../../../core/models/user.model';

@Component({
  selector: 'app-audit-log-list',
  templateUrl: './audit-log-list.component.html',
  styleUrls: ['./audit-log-list.component.scss'],
  providers: [DatePipe]
})
export class AuditLogListComponent implements OnInit {
  displayedColumns = ['createdAt', 'user', 'action', 'entity', 'details', 'ipAddress'];

  logs: AuditLog[] = [];
  loading = true;
  totalCount = 0;
  pageSize = 20;
  pageIndex = 0;

  users: User[] = [];

  // Filters
  actionFilter = '';
  entityFilter = '';
  userFilter: number | null = null;
  fromDate: Date | null = null;
  toDate: Date | null = null;

  readonly actions = AuditActions;
  readonly entities = AuditEntities;

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  constructor(
    private auditLogService: AuditLogService,
    private userService: UserService,
    private snackBar: MatSnackBar,
    private datePipe: DatePipe
  ) {}

  ngOnInit(): void {
    this.loadUsers();
    this.load();
  }

  loadUsers(): void {
    this.userService.getAll(1, 500).subscribe({
      next: res => this.users = res.items,
      error: () => {}
    });
  }

  load(): void {
    this.loading = true;

    const filter: AuditLogFilter = {
      page: this.pageIndex + 1,
      pageSize: this.pageSize,
      action: this.actionFilter || undefined,
      entityName: this.entityFilter || undefined,
      userId: this.userFilter ?? undefined,
      fromDate: this.fromDate ? this.fromDate.toISOString() : undefined,
      toDate: this.toDate ? this.toDate.toISOString() : undefined
    };

    this.auditLogService.getAll(filter).subscribe({
      next: res => {
        this.logs = res.items;
        this.totalCount = res.totalCount;
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.snackBar.open('Failed to load audit logs.', 'Close', { duration: 3000 });
      }
    });
  }

  onPageChange(event: PageEvent): void {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.load();
  }

  applyFilters(): void {
    this.pageIndex = 0;
    this.load();
  }

  clearFilters(): void {
    this.actionFilter = '';
    this.entityFilter = '';
    this.userFilter = null;
    this.fromDate = null;
    this.toDate = null;
    this.pageIndex = 0;
    this.load();
  }

  getActionChipClass(action: string): string {
    switch (action.toLowerCase()) {
      case 'create': return 'action-create';
      case 'update': return 'action-update';
      case 'delete': return 'action-delete';
      case 'login': return 'action-login';
      case 'logout': return 'action-logout';
      default: return 'action-default';
    }
  }

  parseValues(json: string | null | undefined): Record<string, unknown> | null {
    if (!json) return null;
    try {
      return JSON.parse(json);
    } catch {
      return null;
    }
  }

  getChangedFields(log: AuditLog): string[] {
    const newVals = this.parseValues(log.newValues);
    if (!newVals) return [];
    return Object.keys(newVals).slice(0, 3);
  }
}
