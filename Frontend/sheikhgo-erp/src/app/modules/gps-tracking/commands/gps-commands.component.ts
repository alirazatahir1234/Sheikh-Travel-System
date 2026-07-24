import { Component, OnInit, OnDestroy } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { GpsTrackingService } from '../../../core/services/gps-tracking.service';
import { AuthService } from '../../../core/services/auth.service';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { GpsDevice, GpsDeviceCommand, GpsCommandLibraryItem } from '../../../core/models/gps-tracking.model';

interface CommandCard {
  type: string;
  label: string;
  description: string;
  icon: string;
  colorClass: string;
  permission: string;
  requiresCutoff: boolean;
  requiresRelay: boolean;
  needsAttributes: boolean;
}

type StatusFilter = 'all' | 'pending' | 'sent' | 'failed' | 'timeout' | 'cancelled';

@Component({
  standalone: false,
  selector: 'app-gps-commands',
  templateUrl: './gps-commands.component.html',
  styleUrls: ['./gps-commands.component.scss']
})
export class GpsCommandsComponent implements OnInit, OnDestroy {
  devices: GpsDevice[] = [];
  selectedDeviceId: number | null = null;
  commands: GpsDeviceCommand[] = [];
  loading = false;
  sending = false;
  statusFilter: StatusFilter = 'all';

  pendingCard: CommandCard | null = null;
  selectedReason = '';
  smsPhone = '';
  smsMessage = '';
  readonly REASONS = ['Vehicle Theft', 'Unauthorized Driver', 'Police Request', 'Maintenance', 'Other'];

  private readonly fallbackCards: CommandCard[] = [
    { type: 'engineStop',     label: 'Engine Stop',       description: 'Remotely cut engine power',        icon: 'power_off',       colorClass: 'card-red',    permission: 'Gps.CommandEngineCutoff',    requiresCutoff: true,  requiresRelay: false, needsAttributes: false },
    { type: 'engineResume',   label: 'Engine Resume',     description: 'Re-enable engine power',            icon: 'power',           colorClass: 'card-green',  permission: 'Gps.CommandEngineCutoff',    requiresCutoff: true,  requiresRelay: false, needsAttributes: false },
    { type: 'positionSingle', label: 'Request Position',  description: 'Get current GPS fix',               icon: 'location_on',    colorClass: 'card-blue',   permission: 'Gps.CommandPositionRequest', requiresCutoff: false, requiresRelay: false, needsAttributes: false },
    { type: 'restart',        label: 'Restart Device',    description: 'Reboot the GPS tracker',            icon: 'restart_alt',    colorClass: 'card-blue',   permission: 'Gps.CommandRestart',         requiresCutoff: false, requiresRelay: false, needsAttributes: false },
    { type: 'relayOn',        label: 'Relay ON',          description: 'Activate connected relay/output',   icon: 'toggle_on',      colorClass: 'card-green',  permission: 'Gps.CommandRelay',           requiresCutoff: false, requiresRelay: true,  needsAttributes: false },
    { type: 'relayOff',       label: 'Relay OFF',         description: 'Deactivate connected relay/output', icon: 'toggle_off',     colorClass: 'card-red',    permission: 'Gps.CommandRelay',           requiresCutoff: false, requiresRelay: true,  needsAttributes: false },
    { type: 'buzzer',         label: 'Buzzer',            description: 'Trigger buzzer (may not be supported on this model)', icon: 'campaign', colorClass: 'card-gray', permission: 'Gps.CommandBuzzer', requiresCutoff: false, requiresRelay: false, needsAttributes: false },
    { type: 'customSms',      label: 'Custom SMS',        description: 'Queue a raw SMS command (delivery not yet configured)', icon: 'sms', colorClass: 'card-gray', permission: 'Gps.CommandCustomSms', requiresCutoff: false, requiresRelay: false, needsAttributes: true },
    { type: 'custom',         label: 'Custom Command',    description: 'Send a raw command string',         icon: 'terminal',       colorClass: 'card-gray',   permission: 'Gps.CommandSend',            requiresCutoff: false, requiresRelay: false, needsAttributes: false },
  ];

  commandCards: CommandCard[] = [...this.fallbackCards];

  private refreshTimer: ReturnType<typeof setInterval> | null = null;

  constructor(
    private gps: GpsTrackingService,
    private auth: AuthService,
    private toast: UiToastService,
    private route: ActivatedRoute
  ) {}

  ngOnInit(): void {
    const deepLinkDeviceId = Number(this.route.snapshot.queryParamMap.get('deviceId')) || null;
    const deepLinkCommand = this.route.snapshot.queryParamMap.get('command');

    this.gps.getCommandLibrary().subscribe({
      next: lib => {
        if (lib?.length) {
          this.commandCards = this.mergeLibraryCards(lib);
        }
      },
      error: () => { /* keep fallback cards */ }
    });

    this.gps.getDevices().subscribe({
      next: d => {
        this.devices = d.filter(x => x.isActive);
        if (this.devices.length) {
          const match = deepLinkDeviceId && this.devices.some(x => x.id === deepLinkDeviceId);
          this.selectedDeviceId = match ? deepLinkDeviceId : this.devices[0].id;
          this.loadCommands();

          if (deepLinkCommand) {
            const card = this.commandCards.find(c => c.type === deepLinkCommand);
            if (card && !this.isCardDisabled(card)) {
              this.pendingCard = card;
              this.selectedReason = '';
            }
          }
        }
      }
    });
    this.refreshTimer = setInterval(() => this.loadCommands(), 15_000);
  }

  private mergeLibraryCards(lib: GpsCommandLibraryItem[]): CommandCard[] {
    const meta: Record<string, Partial<CommandCard>> = {};
    for (const c of this.fallbackCards) meta[c.type] = c;

    return lib
      .filter(x => x.isActive)
      .sort((a, b) => a.sortOrder - b.sortOrder)
      .map(x => {
        const known = meta[x.commandKey];
        return {
          type: x.commandKey,
          label: x.displayName,
          description: x.description || x.category,
          icon: known?.icon || 'tune',
          colorClass: known?.colorClass || (x.dangerLevel === 'Critical' || x.dangerLevel === 'High' ? 'card-red' : 'card-blue'),
          permission: known?.permission || 'Gps.CommandSend',
          requiresCutoff: x.requiredCapabilityKey === 'EngineCut' || !!known?.requiresCutoff,
          requiresRelay: x.requiredCapabilityKey === 'Relay' || !!known?.requiresRelay,
          needsAttributes: x.commandKey === 'customSms' || x.commandKey === 'apn' || x.commandKey === 'server' || x.commandKey === 'heartbeat' || x.commandKey === 'timezone'
        } as CommandCard;
      });
  }

  ngOnDestroy(): void {
    if (this.refreshTimer) clearInterval(this.refreshTimer);
  }

  loadCommands(): void {
    if (!this.selectedDeviceId) return;
    this.loading = true;
    const status = this.statusFilter === 'all' ? undefined : this.statusFilter;
    this.gps.getCommands(this.selectedDeviceId, { status }).subscribe({
      next: c => { this.commands = c; this.loading = false; },
      error: () => { this.loading = false; }
    });
  }

  setStatusFilter(filter: StatusFilter): void {
    this.statusFilter = filter;
    this.loadCommands();
  }

  get kpiTiles() {
    const count = (status: string) => this.commands.filter(c => c.status === status).length;
    return [
      { filter: 'all' as StatusFilter,      label: 'Total',     value: this.commands.length, icon: 'list',           color: '#64748b' },
      { filter: 'pending' as StatusFilter,  label: 'Pending',   value: count('pending'),      icon: 'hourglass_top',  color: '#f59e0b' },
      { filter: 'sent' as StatusFilter,     label: 'Sent',      value: count('sent'),          icon: 'send',           color: '#3b82f6' },
      { filter: 'failed' as StatusFilter,   label: 'Failed',    value: count('failed'),        icon: 'error',          color: '#ef4444' },
      { filter: 'timeout' as StatusFilter,  label: 'Timeout',   value: count('timeout'),       icon: 'schedule',       color: '#ea580c' },
      { filter: 'cancelled' as StatusFilter, label: 'Cancelled', value: count('cancelled'),    icon: 'block',          color: '#94a3b8' },
    ];
  }

  canView(): boolean {
    return this.auth.hasPermission('Gps.CommandView');
  }

  isCardDisabled(card: CommandCard): boolean {
    if (!this.selectedDeviceId || this.sending) return true;
    if (!this.auth.hasPermission(card.permission)) return true;
    if (card.requiresCutoff && !this.selectedDevice()?.supportsEngineCutoff) return true;
    if (card.requiresRelay && !this.selectedDevice()?.supportsRelay) return true;
    return false;
  }

  cardDisabledReason(card: CommandCard): string | null {
    if (!this.auth.hasPermission(card.permission)) return 'You do not have permission for this command';
    if (card.requiresCutoff && !this.selectedDevice()?.supportsEngineCutoff) return 'Device does not support engine cut-off';
    if (card.requiresRelay && !this.selectedDevice()?.supportsRelay) return 'Device does not support relay control';
    return null;
  }

  send(card: CommandCard): void {
    if (this.isCardDisabled(card)) {
      const reason = this.cardDisabledReason(card);
      if (reason) this.toast.warning(reason);
      return;
    }
    this.pendingCard = card;
    this.selectedReason = '';
    this.smsPhone = '';
    this.smsMessage = '';
  }

  canConfirm(): boolean {
    if (!this.pendingCard) return false;
    if (!this.selectedReason) return false;
    if (this.pendingCard.type === 'customSms' && (!this.smsPhone || !this.smsMessage)) return false;
    return true;
  }

  confirmSend(): void {
    if (!this.pendingCard || !this.selectedDeviceId || !this.canConfirm()) return;
    this.sending = true;
    const attributes = this.pendingCard.type === 'customSms'
      ? { phone: this.smsPhone, message: this.smsMessage }
      : undefined;

    this.gps.sendCommand(this.selectedDeviceId, this.pendingCard.type, this.selectedReason, attributes).subscribe({
      next: () => {
        this.toast.success('Command sent to device');
        this.sending = false;
        this.pendingCard = null;
        this.loadCommands();
      },
      error: err => {
        this.toast.error(err?.error?.message ?? 'Command failed');
        this.sending = false;
      }
    });
  }

  cancelSend(): void {
    this.pendingCard = null;
    this.selectedReason = '';
  }

  canRetry(c: GpsDeviceCommand): boolean {
    return (c.status === 'failed' || c.status === 'timeout') && this.auth.hasPermission('Gps.CommandRetry');
  }

  canCancel(c: GpsDeviceCommand): boolean {
    return c.status === 'pending' && this.auth.hasPermission('Gps.CommandCancel');
  }

  retry(c: GpsDeviceCommand): void {
    this.gps.retryCommand(c.id).subscribe({
      next: () => { this.toast.success('Command retried'); this.loadCommands(); },
      error: err => this.toast.error(err?.error?.message ?? 'Retry failed')
    });
  }

  cancel(c: GpsDeviceCommand): void {
    this.gps.cancelCommand(c.id).subscribe({
      next: () => { this.toast.success('Command cancelled'); this.loadCommands(); },
      error: err => this.toast.error(err?.error?.message ?? 'Cancel failed')
    });
  }

  statusBadgeClass(status: string): string {
    if (status === 'pending') return 'badge-amber';
    if (status === 'sent' || status === 'acknowledged') return 'badge-green';
    if (status === 'failed' || status === 'timeout') return 'badge-red';
    if (status === 'cancelled' || status === 'not_configured') return 'badge-gray';
    return 'badge-gray';
  }

  selectedDevice(): GpsDevice | undefined {
    return this.devices.find(d => d.id === this.selectedDeviceId);
  }
}
