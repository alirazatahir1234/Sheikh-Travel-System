import { Component, OnInit } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { AuthService } from '../../../core/services/auth.service';
import { WhatsAppInboxService } from '../../../core/services/whatsapp-inbox.service';
import { WhatsAppAccount } from '../../../core/models/whatsapp.model';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import {
  ConfirmDialogComponent,
  ConfirmDialogData
} from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import {
  WhatsAppAccountViewDialogComponent,
  WhatsAppAccountViewDialogData
} from './whatsapp-account-view-dialog.component';
import {
  WhatsAppAccountTestDialogComponent,
  WhatsAppAccountTestDialogData
} from './whatsapp-account-test-dialog.component';

@Component({
  standalone: false,
  selector: 'app-whatsapp-accounts',
  templateUrl: './whatsapp-accounts.component.html',
  styleUrls: ['./whatsapp-accounts.component.scss']
})
export class WhatsAppAccountsComponent implements OnInit {
  accounts: WhatsAppAccount[] = [];
  loading = false;
  actionBusyId: number | null = null;
  error: string | null = null;

  readonly breadcrumbs = [
    { label: 'Administration' },
    { label: 'WhatsApp' },
    { label: 'Accounts' }
  ];

  constructor(
    private readonly api: WhatsAppInboxService,
    private readonly auth: AuthService,
    private readonly dialog: MatDialog,
    private readonly toast: UiToastService
  ) {}

  get canManage(): boolean {
    return (
      this.auth.hasPermission('WhatsApp.ManageAccounts') ||
      this.auth.hasPermission('WhatsApp.Manage')
    );
  }

  get canReply(): boolean {
    return this.auth.hasPermission('WhatsApp.Reply');
  }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    this.error = null;
    this.api.getAccounts(this.canManage).subscribe({
      next: rows => {
        this.accounts = rows ?? [];
        this.loading = false;
      },
      error: err => {
        this.loading = false;
        this.error = err?.error?.message || err?.message || 'Failed to load WhatsApp accounts.';
        this.toast.error(this.error!);
      }
    });
  }

  formatPhone(phone: string | null | undefined): string {
    const raw = (phone ?? '').trim();
    const digits = raw.replace(/\D/g, '');
    if (digits.startsWith('971') && digits.length >= 11) {
      const rest = digits.slice(3);
      return `+971 ${rest.slice(0, 2)} ${rest.slice(2, 5)} ${rest.slice(5)}`.trim();
    }
    if (digits.startsWith('92') && digits.length >= 12) {
      const rest = digits.slice(2);
      return `+92 ${rest.slice(0, 3)} ${rest.slice(3)}`.trim();
    }
    if (raw.startsWith('+')) return raw;
    return digits ? `+${digits}` : '—';
  }

  countryLabel(account: WhatsAppAccount): string {
    if (account.country === 'AE' || account.code === 'UAE') return 'United Arab Emirates';
    if (account.country === 'PK' || account.code === 'PK') return 'Pakistan';
    return account.country || account.countryCode || '—';
  }

  purposeLabel(account: WhatsAppAccount): string {
    const purpose = (account.purpose || '').trim();
    if (account.code === 'UAE') return purpose ? `${purpose} / GCC` : 'Sales / GCC';
    if (account.code === 'PK') return purpose ? `${purpose} / Pakistan` : 'Support / Pakistan';
    return purpose || '—';
  }

  healthLabel(account: WhatsAppAccount): string {
    if (!account.lastHealthCheckedAtUtc) return 'Never';
    const when = new Date(account.lastHealthCheckedAtUtc).toLocaleString();
    const status = account.lastHealthStatus || 'Unknown';
    return `${status} · ${when}`;
  }

  openView(account: WhatsAppAccount): void {
    this.dialog.open(WhatsAppAccountViewDialogComponent, {
      width: '480px',
      maxWidth: '95vw',
      data: {
        account,
        formattedPhone: this.formatPhone(account.e164Phone),
        countryLabel: this.countryLabel(account),
        purposeLabel: this.purposeLabel(account),
        healthLabel: this.healthLabel(account)
      } as WhatsAppAccountViewDialogData
    });
  }

  openTest(account: WhatsAppAccount): void {
    if (!this.canReply) {
      this.toast.warning('WhatsApp.Reply permission is required to send a test message.');
      return;
    }
    this.dialog.open(WhatsAppAccountTestDialogComponent, {
      width: '440px',
      maxWidth: '95vw',
      data: { account } as WhatsAppAccountTestDialogData
    });
  }

  confirmToggleActive(account: WhatsAppAccount): void {
    if (!this.canManage) return;
    const enabling = !account.isActive;
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      width: '400px',
      data: {
        title: enabling ? 'Enable WhatsApp account' : 'Disable WhatsApp account',
        message: enabling
          ? `Enable “${account.displayName}”? It will appear in the inbox and can send/receive messages.`
          : `Disable “${account.displayName}”? It will be hidden from the inbox until re-enabled.`,
        confirmText: enabling ? 'Enable' : 'Disable',
        confirmColor: enabling ? 'primary' : 'warn'
      } as ConfirmDialogData
    });

    dialogRef.afterClosed().subscribe(ok => {
      if (!ok) return;
      this.actionBusyId = account.id;
      this.api.setAccountActive(account.id, enabling).subscribe({
        next: updated => {
          this.actionBusyId = null;
          this.toast.success(
            enabling
              ? `${updated.displayName} enabled.`
              : `${updated.displayName} disabled.`
          );
          this.load();
        },
        error: err => {
          this.actionBusyId = null;
          this.toast.error(err?.error?.message || 'Failed to update account status.');
        }
      });
    });
  }

  runHealthCheck(account: WhatsAppAccount): void {
    if (!this.canManage) return;
    this.actionBusyId = account.id;
    this.api.checkAccountHealth(account.id).subscribe({
      next: result => {
        this.actionBusyId = null;
        if (result.status === 'Healthy') {
          this.toast.success(result.message || 'Account is healthy.');
        } else {
          this.toast.warning(result.message || `Health: ${result.status}`);
        }
        this.load();
      },
      error: err => {
        this.actionBusyId = null;
        const envelope = err?.error;
        const msg = envelope?.message || envelope?.data?.message || 'Health check failed.';
        this.toast.error(msg);
        this.load();
      }
    });
  }
}
