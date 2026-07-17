import { Component, OnInit, ViewChild } from '@angular/core';
import { MatPaginator, PageEvent } from '@angular/material/paginator';
import { DatePipe } from '@angular/common';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { NotificationService } from '../../../core/services/notification.service';
import { ExportService, ExportColumn } from '../../../core/services/export.service';
import {
  Notification,
  NotificationFilter,
  NotificationPriorityLabels,
  NotificationStats,
  NotificationTypeColors,
  NotificationTypeIcons,
  NotificationTypeLabels
} from '../../../core/models/notification.model';

type TabFilter = 'all' | 'unread' | 'read' | 'sent' | 'failed';

@Component({
  standalone: false,
  selector: 'app-notification-center',
  templateUrl: './notification-center.component.html',
  styleUrls: ['./notification-center.component.scss'],
  providers: [DatePipe]
})
export class NotificationCenterComponent implements OnInit {
  displayedColumns = ['status', 'title', 'channel', 'priority', 'createdAt', 'actions'];

  items: Notification[] = [];
  stats: NotificationStats | null = null;
  loading = true;
  totalCount = 0;
  pageSize = 20;
  pageIndex = 0;

  tab: TabFilter = 'all';
  search = '';
  channelFilter = '';
  priorityFilter: number | null = null;
  fromDateStr = '';
  toDateStr = '';

  readonly channels = ['InApp', 'Email', 'Sms', 'Push', 'Browser', 'WhatsApp'];
  readonly typeLabels = NotificationTypeLabels;
  readonly typeIcons = NotificationTypeIcons;
  readonly typeColors = NotificationTypeColors;
  readonly priorityLabels = NotificationPriorityLabels;

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  constructor(
    private notifications: NotificationService,
    private exportService: ExportService,
    private toast: UiToastService,
    private datePipe: DatePipe
  ) {}

  ngOnInit(): void {
    void this.notifications.requestBrowserPermission();
    this.loadStats();
    this.load();
  }

  setTab(tab: TabFilter): void {
    this.tab = tab;
    this.pageIndex = 0;
    this.load();
  }

  applyFilters(): void {
    this.pageIndex = 0;
    this.load();
  }

  onPage(e: PageEvent): void {
    this.pageIndex = e.pageIndex;
    this.pageSize = e.pageSize;
    this.load();
  }

  loadStats(): void {
    this.notifications.getStats().subscribe(s => this.stats = s);
  }

  load(): void {
    this.loading = true;
    const filter: NotificationFilter = {
      page: this.pageIndex + 1,
      pageSize: this.pageSize,
      search: this.search || undefined,
      channel: this.channelFilter || undefined,
      priority: this.priorityFilter ?? undefined,
      fromDate: this.fromDateStr ? new Date(`${this.fromDateStr}T00:00:00`).toISOString() : undefined,
      toDate: this.toDateStr ? new Date(`${this.toDateStr}T23:59:59`).toISOString() : undefined
    };

    if (this.tab === 'unread') filter.unreadOnly = true;
    if (this.tab === 'sent') filter.isSent = true;

    this.notifications.getAll(filter).subscribe({
      next: res => {
        let rows = res.items;
        if (this.tab === 'read') rows = rows.filter(n => n.isRead);
        if (this.tab === 'failed') rows = rows.filter(n => n.isSent === false && (n.channel ?? 'InApp') !== 'InApp');
        this.items = rows;
        this.totalCount = this.tab === 'read' || this.tab === 'failed' ? rows.length : res.totalCount;
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.toast.error('Failed to load notifications');
      }
    });
  }

  markRead(n: Notification): void {
    this.notifications.markAsRead([n.id]).subscribe({
      next: () => { this.load(); this.loadStats(); },
      error: () => this.toast.error('Could not mark as read')
    });
  }

  markAllRead(): void {
    this.notifications.markAllAsRead().subscribe({
      next: () => {
        this.toast.success('All notifications marked as read');
        this.load();
        this.loadStats();
      }
    });
  }

  deleteOne(n: Notification): void {
    if (!confirm(`Delete notification "${n.title}"?`)) return;
    this.notifications.delete(n.id).subscribe({
      next: () => {
        this.toast.success('Notification deleted');
        this.load();
        this.loadStats();
      },
      error: () => this.toast.error('Delete failed')
    });
  }

  resend(n: Notification): void {
    this.notifications.send(n.id).subscribe({
      next: () => {
        this.toast.success('Send requested');
        this.load();
      },
      error: () => this.toast.error('Send failed')
    });
  }

  exportExcel(): void {
    const cols: ExportColumn<Notification>[] = [
      { header: 'Title', accessor: n => n.title },
      { header: 'Message', accessor: n => n.message },
      { header: 'Channel', accessor: n => n.channel ?? 'InApp' },
      { header: 'Priority', accessor: n => this.priorityLabels[n.priority ?? 2] ?? '' },
      { header: 'Read', accessor: n => n.isRead ? 'Yes' : 'No' },
      { header: 'Sent', accessor: n => n.isSent ? 'Yes' : 'No' },
      { header: 'Created', accessor: n => this.datePipe.transform(n.createdAt, 'medium') ?? '' }
    ];
    this.exportService.exportExcel(this.items, cols, {
      title: 'Notifications',
      filename: 'notifications'
    });
  }

  iconFor(n: Notification): string {
    return this.typeIcons[n.type] ?? 'notifications';
  }

  colorFor(n: Notification): string {
    return this.typeColors[n.type] ?? '#64748b';
  }
}
