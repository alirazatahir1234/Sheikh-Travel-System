import { Component, OnInit, ViewChild } from '@angular/core';
import { MatPaginator, PageEvent } from '@angular/material/paginator';
import { DatePipe } from '@angular/common';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { NotificationService } from '../../../core/services/notification.service';
import { ExportService, ExportColumn } from '../../../core/services/export.service';
import {
  Notification,
  NotificationFilter,
  NotificationModules,
  NotificationPreferences,
  NotificationPriorityLabels,
  NotificationRetentionEstimate,
  NotificationRetentionPolicy,
  NotificationStats,
  NotificationTemplate,
  NotificationType,
  NotificationTypeColors,
  NotificationTypeIcons,
  NotificationTypeLabels
} from '../../../core/models/notification.model';

type TabFilter = 'all' | 'unread' | 'read' | 'failed' | 'archived' | 'trash';
type ViewSection = 'inbox' | 'preferences' | 'templates' | 'retention';
type ComposeRecipientMode = 'users' | 'role' | 'custom';
type ComposeRecipient = { id: number; fullName: string; email: string };

@Component({
  standalone: false,
  selector: 'app-notification-center',
  templateUrl: './notification-center.component.html',
  styleUrls: ['./notification-center.component.scss'],
  providers: [DatePipe]
})
export class NotificationCenterComponent implements OnInit {
  items: Notification[] = [];
  stats: NotificationStats | null = null;
  loading = true;
  totalCount = 0;
  pageSize = 20;
  pageIndex = 0;
  expandedId: number | null = null;

  section: ViewSection = 'inbox';
  tab: TabFilter = 'all';
  search = '';
  channelFilter = '';
  moduleFilter = '';
  priorityFilter: number | null = null;
  fromDateStr = '';
  toDateStr = '';
  datePreset = '';

  prefs: NotificationPreferences = {
    emailEnabled: true,
    smsEnabled: true,
    pushEnabled: true,
    browserEnabled: true,
    whatsAppEnabled: false
  };
  prefsSaving = false;

  templates: NotificationTemplate[] = [];
  templatesLoading = false;
  editingTemplate: NotificationTemplate | null = null;
  templateForm = {
    templateKey: '',
    templateName: '',
    subject: '',
    body: '',
    channel: 'InApp',
    language: 'en',
    variables: '',
    isActive: true
  };

  composeOpen = false;
  composeSending = false;
  users: ComposeRecipient[] = [];
  usersLoading = false;
  selectedIds: number[] = [];
  undoIds: number[] | null = null;
  private undoTimer: ReturnType<typeof setTimeout> | null = null;
  bulkDeleting = false;

  retention: NotificationRetentionPolicy = {
    readArchiveDays: 30,
    archivedDeleteDays: 180,
    failedDeleteDays: 90,
    draftDeleteDays: 30,
    operationalDeleteDays: 90,
    maintenanceDeleteDays: 730,
    complianceDeleteDays: 2555,
    criticalNeverDelete: true,
    securityDeleteDays: 730
  };
  retentionEstimate: NotificationRetentionEstimate | null = null;
  retentionSaving = false;
  retentionRunning = false;

  compose = {
    recipientMode: 'users' as ComposeRecipientMode,
    selectedUserIds: [] as number[],
    role: '' as string,
    customEmails: '',
    subject: '',
    body: '',
    templateKey: '' as string,
    priority: 2,
    channels: {
      Email: true,
      Push: false,
      Sms: false,
      Browser: false,
      InApp: true
    }
  };

  readonly roleOptions = [
    { value: 'Admin', label: 'Admin' },
    { value: 'Dispatcher', label: 'Dispatcher' },
    { value: 'Driver', label: 'Driver' },
    { value: 'Accountant', label: 'Accountant' }
  ];

  readonly channels = ['InApp', 'Email', 'Sms', 'Push', 'Browser', 'WhatsApp'];
  readonly modules = NotificationModules;
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

  get readCount(): number {
    if (!this.stats) return 0;
    return Math.max(0, this.stats.total - this.stats.unread);
  }

  setSection(section: ViewSection): void {
    this.section = section;
    if (section === 'preferences') this.loadPreferences();
    if (section === 'templates') this.loadTemplates();
    if (section === 'retention') {
      this.loadRetention();
      this.loadRetentionEstimate();
    }
  }

  setTab(tab: TabFilter): void {
    this.tab = tab;
    this.pageIndex = 0;
    this.selectedIds = [];
    this.load();
  }

  applyFilters(): void {
    this.pageIndex = 0;
    this.load();
  }

  resetFilters(): void {
    this.search = '';
    this.channelFilter = '';
    this.moduleFilter = '';
    this.priorityFilter = null;
    this.fromDateStr = '';
    this.toDateStr = '';
    this.datePreset = '';
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
      module: this.moduleFilter || undefined,
      priority: this.priorityFilter ?? undefined,
      datePreset: this.datePreset || undefined,
      fromDate: !this.datePreset && this.fromDateStr
        ? new Date(`${this.fromDateStr}T00:00:00`).toISOString()
        : undefined,
      toDate: !this.datePreset && this.toDateStr
        ? new Date(`${this.toDateStr}T23:59:59`).toISOString()
        : undefined,
      archived: this.tab === 'archived',
      trash: this.tab === 'trash'
    };

    if (this.tab === 'unread') filter.unreadOnly = true;
    if (this.tab === 'failed') filter.isSent = false;

    this.notifications.getAll(filter).subscribe({
      next: res => {
        let rows = res.items;
        if (this.tab === 'read') rows = rows.filter(n => n.isRead);
        if (this.tab === 'failed') {
          rows = rows.filter(n =>
            n.deliveryStatus === 'Failed' ||
            (n.isSent === false && (n.channel ?? 'InApp') !== 'InApp'));
        }
        this.items = rows;
        this.totalCount = this.tab === 'read' || this.tab === 'failed' ? rows.length : res.totalCount;
        this.selectedIds = [];
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.toast.error('Failed to load notifications');
      }
    });
  }

  loadPreferences(): void {
    this.notifications.getPreferences().subscribe(p => this.prefs = { ...p });
  }

  savePreferences(): void {
    this.prefsSaving = true;
    this.notifications.savePreferences(this.prefs).subscribe({
      next: p => {
        this.prefs = { ...p };
        this.prefsSaving = false;
        this.toast.success('Notification preferences saved');
      },
      error: () => {
        this.prefsSaving = false;
        this.toast.error('Could not save preferences');
      }
    });
  }

  loadTemplates(): void {
    this.templatesLoading = true;
    this.notifications.getTemplates().subscribe({
      next: rows => {
        this.templates = rows;
        this.templatesLoading = false;
      },
      error: () => {
        this.templatesLoading = false;
        this.toast.error('Failed to load templates');
      }
    });
  }

  startEditTemplate(t?: NotificationTemplate): void {
    if (t) {
      this.editingTemplate = t;
      this.templateForm = {
        templateKey: t.templateKey,
        templateName: t.templateName,
        subject: t.subject,
        body: t.body,
        channel: t.channel,
        language: t.language || 'en',
        variables: t.variables || '',
        isActive: t.isActive
      };
    } else {
      this.editingTemplate = null;
      this.templateForm = {
        templateKey: '',
        templateName: '',
        subject: '',
        body: '',
        channel: 'InApp',
        language: 'en',
        variables: '',
        isActive: true
      };
    }
  }

  cancelEditTemplate(): void {
    this.editingTemplate = null;
    this.startEditTemplate();
  }

  saveTemplate(): void {
    const f = this.templateForm;
    if (!f.templateKey.trim() || !f.templateName.trim() || !f.subject.trim() || !f.body.trim()) {
      this.toast.warning('Fill in key, name, subject, and body');
      return;
    }

    this.notifications.upsertTemplate({
      templateKey: f.templateKey.trim(),
      templateName: f.templateName.trim(),
      subject: f.subject,
      body: f.body,
      channel: f.channel,
      language: f.language || 'en',
      variables: f.variables || null,
      isActive: f.isActive
    }, this.editingTemplate?.id).subscribe({
      next: () => {
        this.toast.success(this.editingTemplate ? 'Template updated' : 'Template created');
        this.cancelEditTemplate();
        this.loadTemplates();
      },
      error: () => this.toast.error('Could not save template')
    });
  }

  toggleExpand(n: Notification): void {
    this.expandedId = this.expandedId === n.id ? null : n.id;
    if (!n.isRead) this.markRead(n);
  }

  markRead(n: Notification): void {
    if (n.isRead) return;
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

  deleteOne(n: Notification, event?: Event): void {
    event?.stopPropagation();
    this.softDeleteIds([n.id]);
  }

  archiveOne(n: Notification, event?: Event): void {
    event?.stopPropagation();
    this.notifications.archive([n.id]).subscribe({
      next: () => {
        this.toast.success('Notification archived');
        this.load();
        this.loadStats();
      },
      error: () => this.toast.error('Archive failed')
    });
  }

  restoreOne(n: Notification, event?: Event): void {
    event?.stopPropagation();
    this.notifications.restore([n.id]).subscribe({
      next: () => {
        this.toast.success('Notification restored');
        this.load();
        this.loadStats();
      },
      error: () => this.toast.error('Restore failed')
    });
  }

  get selectedCount(): number {
    return this.selectedIds.length;
  }

  get allPageSelected(): boolean {
    return this.items.length > 0 && this.items.every(n => this.selectedIds.includes(n.id));
  }

  isSelected(id: number): boolean {
    return this.selectedIds.includes(id);
  }

  toggleSelect(id: number, event: Event): void {
    event.stopPropagation();
    if (this.selectedIds.includes(id)) {
      this.selectedIds = this.selectedIds.filter(x => x !== id);
    } else {
      this.selectedIds = [...this.selectedIds, id];
    }
  }

  toggleSelectAll(event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    this.selectedIds = checked ? this.items.map(n => n.id) : [];
  }

  clearSelection(): void {
    this.selectedIds = [];
  }

  bulkMarkRead(): void {
    const ids = [...this.selectedIds];
    if (!ids.length) return;
    this.notifications.markAsRead(ids).subscribe({
      next: () => {
        this.toast.success('Marked as read');
        this.selectedIds = [];
        this.load();
        this.loadStats();
      }
    });
  }

  bulkArchive(): void {
    const ids = [...this.selectedIds];
    if (!ids.length) return;
    this.notifications.archive(ids).subscribe({
      next: count => {
        this.toast.success(`Archived ${count} notification(s)`);
        this.selectedIds = [];
        this.load();
        this.loadStats();
      },
      error: () => this.toast.error('Archive failed')
    });
  }

  bulkDeleteSelected(): void {
    const ids = [...this.selectedIds];
    if (!ids.length) {
      this.toast.warning('Select at least one notification');
      return;
    }
    if (!confirm(`Move ${ids.length} notification(s) to Trash?`)) return;
    this.softDeleteIds(ids);
  }

  bulkRestoreSelected(): void {
    const ids = [...this.selectedIds];
    if (!ids.length) return;
    this.notifications.restore(ids).subscribe({
      next: count => {
        this.toast.success(`Restored ${count} notification(s)`);
        this.selectedIds = [];
        this.load();
        this.loadStats();
      },
      error: () => this.toast.error('Restore failed')
    });
  }

  softDeleteIds(ids: number[]): void {
    this.bulkDeleting = true;
    this.notifications.bulkDelete(ids).subscribe({
      next: count => {
        this.bulkDeleting = false;
        this.selectedIds = [];
        this.load();
        this.loadStats();
        this.showUndo(ids);
        this.toast.success(
          count > 0
            ? `Moved ${count} notification(s) to Trash`
            : 'Nothing deleted — check selection and try again'
        );
      },
      error: () => {
        this.bulkDeleting = false;
        this.toast.error('Delete failed');
      }
    });
  }

  showUndo(ids: number[]): void {
    if (this.undoTimer) clearTimeout(this.undoTimer);
    this.undoIds = ids;
    this.undoTimer = setTimeout(() => {
      this.undoIds = null;
      this.undoTimer = null;
    }, 10_000);
  }

  undoDelete(): void {
    if (!this.undoIds?.length) return;
    const ids = [...this.undoIds];
    if (this.undoTimer) clearTimeout(this.undoTimer);
    this.undoIds = null;
    this.undoTimer = null;
    this.notifications.restore(ids).subscribe({
      next: () => {
        this.toast.success('Delete undone');
        this.load();
        this.loadStats();
      },
      error: () => this.toast.error('Undo failed')
    });
  }

  loadRetention(): void {
    this.notifications.getRetention().subscribe({
      next: p => this.retention = { ...p },
      error: () => this.toast.error('Could not load retention policy')
    });
  }

  loadRetentionEstimate(): void {
    this.notifications.getRetentionEstimate().subscribe({
      next: e => this.retentionEstimate = e,
      error: () => { this.retentionEstimate = null; }
    });
  }

  saveRetention(): void {
    this.retentionSaving = true;
    this.notifications.saveRetention(this.retention).subscribe({
      next: p => {
        this.retention = { ...p };
        this.retentionSaving = false;
        this.toast.success('Retention policy saved');
        this.loadRetentionEstimate();
      },
      error: () => {
        this.retentionSaving = false;
        this.toast.error('Could not save retention policy');
      }
    });
  }

  runRetentionNow(): void {
    if (!confirm('Run notification cleanup now? This archives/deletes eligible records.')) return;
    this.retentionRunning = true;
    this.notifications.runRetentionCleanup().subscribe({
      next: e => {
        this.retentionEstimate = e;
        this.retentionRunning = false;
        this.toast.success(
          `Cleanup done — archived ${e.eligibleAutoArchive}, removed ${e.eligibleHardDelete}`
        );
        this.load();
        this.loadStats();
      },
      error: () => {
        this.retentionRunning = false;
        this.toast.error('Cleanup failed');
      }
    });
  }

  resend(n: Notification, event?: Event): void {
    event?.stopPropagation();
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
      { header: 'Module', accessor: n => n.module ?? 'System' },
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
    const module = (n.module || '').toLowerCase();
    if (module.includes('fleet') || module.includes('gps')) return 'directions_car';
    if (module.includes('compliance')) return 'verified_user';
    if (module.includes('finance') || module.includes('payment')) return 'payments';
    if (module.includes('booking')) return 'confirmation_number';
    if (module.includes('maintenance')) return 'build';
    return this.typeIcons[n.type] ?? 'notifications';
  }

  colorFor(n: Notification): string {
    return this.typeColors[n.type] ?? '#0F766E';
  }

  priorityClass(n: Notification): string {
    switch (n.priority ?? 2) {
      case 1: return 'prio-low';
      case 3: return 'prio-high';
      case 4: return 'prio-critical';
      default: return 'prio-normal';
    }
  }

  priorityLabel(n: Notification): string {
    return this.priorityLabels[n.priority ?? 2] ?? 'Normal';
  }

  channelLabel(channel?: string | null): string {
    if (!channel || channel === 'InApp') return 'In-App';
    return channel;
  }

  formatAbsolute(iso: string): string {
    return this.datePipe.transform(iso, 'MMM d, yyyy h:mm a') ?? '';
  }

  formatRelative(iso: string): string {
    const then = new Date(iso).getTime();
    if (Number.isNaN(then)) return '';
    const seconds = Math.floor((Date.now() - then) / 1000);
    if (seconds < 60) return 'Just now';
    const minutes = Math.floor(seconds / 60);
    if (minutes < 60) return `${minutes}m ago`;
    const hours = Math.floor(minutes / 60);
    if (hours < 24) return `${hours}h ago`;
    const days = Math.floor(hours / 24);
    if (days < 7) return `${days}d ago`;
    return this.formatAbsolute(iso);
  }

  trackById(_: number, n: Notification): number {
    return n.id;
  }

  typeName(type: NotificationType): string {
    return this.typeLabels[type] ?? 'Alert';
  }

  openCompose(): void {
    this.composeOpen = true;
    this.loadComposeUsers();
    this.loadTemplates();
  }

  closeCompose(): void {
    this.composeOpen = false;
  }

  loadComposeUsers(): void {
    this.usersLoading = true;
    this.notifications.getRecipients().subscribe({
      next: rows => {
        this.users = Array.isArray(rows) ? rows : [];
        this.usersLoading = false;
        if (this.users.length === 0) {
          this.toast.warning('No users found for this tenant');
        }
      },
      error: () => {
        this.usersLoading = false;
        this.users = [];
        this.toast.error('Could not load users — restart API if you just updated it');
      }
    });
  }

  toggleUser(id: number, event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    if (checked) {
      if (!this.compose.selectedUserIds.includes(id)) {
        this.compose.selectedUserIds = [...this.compose.selectedUserIds, id];
      }
    } else {
      this.compose.selectedUserIds = this.compose.selectedUserIds.filter(x => x !== id);
    }
  }

  get composeTemplates() {
    const active = Object.entries(this.compose.channels)
      .filter(([, on]) => on)
      .map(([name]) => name);
    const matched = this.templates.filter(t => active.includes(t.channel));
    // Prefer Email templates when Email channel is on (avoids SMS shells with {{title}})
    if (this.compose.channels.Email) {
      const emailOnly = matched.filter(t => t.channel === 'Email');
      if (emailOnly.length) return emailOnly;
    }
    return matched.length ? matched : this.templates;
  }

  onComposeTemplateChange(): void {
    const key = this.compose.templateKey;
    if (!key) return;
    const t =
      this.composeTemplates.find(x => x.templateKey === key) ??
      this.templates.find(x => x.templateKey === key);
    if (!t) return;

    // Subject/body are *content* for {{Title}}/{{Message}} — never paste raw placeholders.
    if (!this.compose.subject.trim() || this.looksLikePlaceholder(this.compose.subject)) {
      this.compose.subject = t.templateName;
    }
    if (!this.compose.body.trim() || this.looksLikePlaceholder(this.compose.body)) {
      this.compose.body = '';
    }
    this.toast.info(`Template “${t.templateName}” will brand your subject & message on send`);
  }

  private looksLikePlaceholder(text: string): boolean {
    return /\{\{\s*\w+\s*\}\}/.test(text);
  }

  toggleComposeChannel(channel: keyof typeof this.compose.channels): void {
    this.compose.channels[channel] = !this.compose.channels[channel];
  }

  sendCompose(): void {
    const subject = this.compose.subject.trim();
    const body = this.compose.body.trim();
    if (!subject || !body) {
      this.toast.warning('Subject and message are required');
      return;
    }

    const selectedChannels = Object.entries(this.compose.channels)
      .filter(([, on]) => on)
      .map(([name]) => name);

    if (selectedChannels.length === 0) {
      this.toast.warning('Select at least one channel');
      return;
    }

    const recipientUserIds =
      this.compose.recipientMode === 'users' ? [...this.compose.selectedUserIds] : [];
    const role =
      this.compose.recipientMode === 'role' && this.compose.role ? this.compose.role : null;
    const emailAddresses =
      this.compose.recipientMode === 'custom'
        ? this.compose.customEmails
            .split(/[,;\s]+/)
            .map(e => e.trim())
            .filter(Boolean)
        : [];

    if (
      recipientUserIds.length === 0 &&
      !role &&
      emailAddresses.length === 0
    ) {
      this.toast.warning('Select recipients (users, role, or custom email)');
      return;
    }

    this.composeSending = true;
    this.notifications.sendManualMessage({
      subject,
      body,
      priority: this.compose.priority,
      recipientUserIds,
      emailAddresses,
      role,
      channels: selectedChannels,
      templateKey: this.compose.templateKey || null,
      sendNow: true
    }).subscribe({
      next: count => {
        this.composeSending = false;
        this.toast.success(`Sent ${count} message(s)`);
        this.resetCompose();
        this.composeOpen = false;
        this.load();
        this.loadStats();
      },
      error: err => {
        this.composeSending = false;
        const msg = err?.error?.message || err?.error?.errors?.[0] || 'Send failed';
        this.toast.error(typeof msg === 'string' ? msg : 'Send failed');
      }
    });
  }

  private resetCompose(): void {
    this.compose = {
      recipientMode: 'users',
      selectedUserIds: [],
      role: '',
      customEmails: '',
      subject: '',
      body: '',
      templateKey: '',
      priority: 2,
      channels: {
        Email: true,
        Push: false,
        Sms: false,
        Browser: false,
        InApp: true
      }
    };
  }
}
