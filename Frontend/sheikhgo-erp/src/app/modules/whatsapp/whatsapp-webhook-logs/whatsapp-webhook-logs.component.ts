import { Component, OnInit } from '@angular/core';
import { WhatsAppInboxService } from '../../../core/services/whatsapp-inbox.service';
import { WhatsAppWebhookLog } from '../../../core/models/whatsapp.model';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  standalone: false,
  selector: 'app-whatsapp-webhook-logs',
  templateUrl: './whatsapp-webhook-logs.component.html',
  styleUrls: ['./whatsapp-webhook-logs.component.scss']
})
export class WhatsAppWebhookLogsComponent implements OnInit {
  logs: WhatsAppWebhookLog[] = [];
  total = 0;
  page = 1;
  pageSize = 30;
  statusFilter = '';
  loading = false;
  requeueingId: number | null = null;

  readonly breadcrumbs = [
    { label: 'Administration' },
    { label: 'WhatsApp' },
    { label: 'Webhook logs' }
  ];

  readonly statuses = ['', 'Received', 'Processing', 'Succeeded', 'Failed', 'Rejected'];

  constructor(
    private readonly api: WhatsAppInboxService,
    private readonly toast: UiToastService,
    private readonly auth: AuthService
  ) {}

  get canManage(): boolean {
    return this.auth.hasPermission('WhatsApp.Manage');
  }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    this.api
      .getWebhookLogs({
        page: this.page,
        pageSize: this.pageSize,
        status: this.statusFilter || null
      })
      .subscribe({
        next: res => {
          this.logs = res?.items ?? [];
          this.total = res?.total ?? 0;
          this.loading = false;
        },
        error: () => {
          this.loading = false;
          this.toast.error('Unable to load webhook logs.');
        }
      });
  }

  setStatus(status: string): void {
    this.statusFilter = status;
    this.page = 1;
    this.load();
  }

  requeue(row: WhatsAppWebhookLog): void {
    if (!this.canManage || this.requeueingId != null) return;
    if (!row.signatureValid) {
      this.toast.error('Cannot requeue invalid signature logs.');
      return;
    }
    this.requeueingId = row.id;
    this.api.requeueWebhookLog(row.id).subscribe({
      next: () => {
        this.requeueingId = null;
        this.toast.success('Webhook requeued.');
        this.load();
      },
      error: err => {
        this.requeueingId = null;
        this.toast.error(err?.error?.message || 'Requeue failed.');
      }
    });
  }
}
