import { Component, OnInit } from '@angular/core';
import { AuthService } from '../../../core/services/auth.service';
import { WhatsAppInboxService } from '../../../core/services/whatsapp-inbox.service';
import { WhatsAppAccount, WhatsAppAutomationPreview, WhatsAppAutomationRule } from '../../../core/models/whatsapp.model';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';

@Component({
  standalone: false,
  selector: 'app-whatsapp-automations',
  templateUrl: './whatsapp-automations.component.html',
  styleUrls: ['./whatsapp-automations.component.scss']
})
export class WhatsAppAutomationsComponent implements OnInit {
  rules: WhatsAppAutomationRule[] = [];
  accounts: WhatsAppAccount[] = [];
  loading = false;
  busyType: string | null = null;
  previewBookingId: number | null = null;
  preview: WhatsAppAutomationPreview | null = null;
  previewingType: string | null = null;

  readonly breadcrumbs = [
    { label: 'Administration' },
    { label: 'WhatsApp' },
    { label: 'Automations' }
  ];

  constructor(
    private readonly api: WhatsAppInboxService,
    private readonly auth: AuthService,
    private readonly toast: UiToastService
  ) {}

  get canManage(): boolean {
    return this.auth.hasPermission('WhatsApp.Manage');
  }

  ngOnInit(): void {
    this.api.getAccounts().subscribe({
      next: rows => (this.accounts = rows ?? []),
      error: () => undefined
    });
    this.load();
  }

  load(): void {
    this.loading = true;
    this.api.getAutomationRules().subscribe({
      next: rows => {
        this.rules = rows ?? [];
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.toast.error('Unable to load automation rules.');
      }
    });
  }

  accountLabel(id?: number | null): string {
    if (id == null) return '—';
    return this.accounts.find(a => a.id === id)?.code ?? String(id);
  }

  save(rule: WhatsAppAutomationRule): void {
    if (!this.canManage || this.busyType) return;
    this.busyType = rule.eventType;
    this.api
      .upsertAutomationRule(rule.eventType, {
        isEnabled: rule.isEnabled,
        accountId: rule.accountId,
        templateName: rule.templateName,
        offsetMinutes: rule.offsetMinutes ?? 0,
        notifyBooker: !!rule.notifyBooker,
        isUrgent: !!rule.isUrgent
      })
      .subscribe({
        next: saved => {
          const idx = this.rules.findIndex(r => r.eventType === saved.eventType);
          if (idx >= 0) this.rules[idx] = saved;
          this.busyType = null;
          this.toast.success(`${saved.eventType} saved`);
        },
        error: err => {
          this.busyType = null;
          const msg = err?.error?.message || err?.error?.Message || 'Save failed.';
          this.toast.error(msg);
          this.load();
        }
      });
  }

  toggle(rule: WhatsAppAutomationRule): void {
    rule.isEnabled = !rule.isEnabled;
    this.save(rule);
  }

  runPreview(rule: WhatsAppAutomationRule): void {
    if (!this.previewBookingId || this.previewBookingId < 1) {
      this.toast.error('Enter a booking id for preview.');
      return;
    }
    this.previewingType = rule.eventType;
    this.api.previewAutomation(rule.eventType, this.previewBookingId).subscribe({
      next: p => {
        this.preview = p;
        this.previewingType = null;
      },
      error: err => {
        this.previewingType = null;
        this.toast.error(err?.error?.message || 'Preview failed.');
      }
    });
  }
}
