import { Component, OnInit } from '@angular/core';
import { AuthService } from '../../../core/services/auth.service';
import { WhatsAppInboxService } from '../../../core/services/whatsapp-inbox.service';
import { WhatsAppAccount, WhatsAppTemplate } from '../../../core/models/whatsapp.model';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';

@Component({
  standalone: false,
  selector: 'app-whatsapp-templates',
  templateUrl: './whatsapp-templates.component.html',
  styleUrls: ['./whatsapp-templates.component.scss']
})
export class WhatsAppTemplatesComponent implements OnInit {
  accounts: WhatsAppAccount[] = [];
  templates: WhatsAppTemplate[] = [];
  selectedAccountId: number | null = null;
  loading = false;
  busyId: number | null = null;

  readonly breadcrumbs = [
    { label: 'Administration' },
    { label: 'WhatsApp' },
    { label: 'Templates' }
  ];

  readonly statuses = ['Draft', 'Pending', 'Approved', 'Rejected', 'Paused', 'Disabled'];

  constructor(
    private readonly api: WhatsAppInboxService,
    private readonly auth: AuthService,
    private readonly toast: UiToastService
  ) {}

  get canManageTemplates(): boolean {
    return this.auth.hasPermission('WhatsApp.ManageTemplates');
  }

  ngOnInit(): void {
    this.api.getAccounts().subscribe({
      next: rows => {
        this.accounts = rows ?? [];
        this.load();
      },
      error: () => this.toast.error('Unable to load WhatsApp accounts.')
    });
  }

  selectAccount(id: number | null): void {
    this.selectedAccountId = id;
    this.load();
  }

  load(): void {
    this.loading = true;
    this.api.getTemplates(this.selectedAccountId).subscribe({
      next: rows => {
        this.templates = rows ?? [];
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.toast.error('Unable to load templates.');
      }
    });
  }

  setStatus(row: WhatsAppTemplate, status: string): void {
    if (!this.canManageTemplates || this.busyId != null) return;
    this.busyId = row.id;
    this.api.setTemplateStatus(row.id, status).subscribe({
      next: updated => {
        const idx = this.templates.findIndex(t => t.id === updated.id);
        if (idx >= 0) this.templates[idx] = updated;
        this.busyId = null;
        this.toast.success(`Template → ${status}`);
      },
      error: err => {
        this.busyId = null;
        this.toast.error(err?.error?.message || 'Status update failed.');
      }
    });
  }
}
