import { Component, OnInit, OnDestroy } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError, finalize } from 'rxjs/operators';
import { GpsTrackingService } from '../../../core/services/gps-tracking.service';
import { AuthService } from '../../../core/services/auth.service';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { GpsDevice, GpsDeviceCommand, GpsCommandLibraryItem, SupportedCommand } from '../../../core/models/gps-tracking.model';

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

type StatusFilter = 'all' | 'pending' | 'PendingApproval' | 'sent' | 'failed' | 'timeout' | 'cancelled';

// APN/SERVER query cards (EV26R APN# / GPRSSET#) need no attributes; set forms use Custom.
const ATTR_COMMANDS = new Set(['customSms', 'custom', 'heartbeat', 'timezone']);

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
  /** Unfiltered history for KPI tiles (status filter only affects the table). */
  allCommandsForKpi: GpsDeviceCommand[] = [];
  loading = false;
  loadError: string | null = null;
  sending = false;
  statusFilter: StatusFilter = 'all';

  pendingCard: CommandCard | null = null;
  selectedReason = '';
  smsPhone = '';
  smsMessage = '';
  attrData = '';
  attrServer = '';
  attrPort = '';
  attrInterval = '';
  attrTimezone = '';
  readonly REASONS = ['Vehicle Theft', 'Unauthorized Driver', 'Police Request', 'Maintenance', 'Other'];

  private readonly fallbackCards: CommandCard[] = [
    { type: 'engineStop',     label: 'Engine Stop',       description: 'Remotely cut engine power (RELAY,1#)', icon: 'power_off',    colorClass: 'card-red',    permission: 'Gps.CommandEngineCutoff',    requiresCutoff: true,  requiresRelay: false, needsAttributes: false },
    { type: 'engineResume',   label: 'Engine Resume',     description: 'Re-enable engine power (RELAY,0#)',  icon: 'power',           colorClass: 'card-green',  permission: 'Gps.CommandEngineCutoff',    requiresCutoff: true,  requiresRelay: false, needsAttributes: false },
    { type: 'positionSingle', label: 'Request Position',  description: 'GPS fix (URL# on VG03, WHERE# on EV26R)', icon: 'location_on', colorClass: 'card-blue', permission: 'Gps.CommandPositionRequest', requiresCutoff: false, requiresRelay: false, needsAttributes: false },
    { type: 'restart',        label: 'Restart Device',    description: 'Reboot tracker (RESET#)',            icon: 'restart_alt',    colorClass: 'card-blue',   permission: 'Gps.CommandRestart',         requiresCutoff: false, requiresRelay: false, needsAttributes: false },
    { type: 'status',         label: 'STATUS',            description: 'Battery, GPS, ACC, network (STATUS#)', icon: 'monitor_heart', colorClass: 'card-blue', permission: 'Gps.CommandSend', requiresCutoff: false, requiresRelay: false, needsAttributes: false },
    { type: 'check',          label: 'CHECK',             description: 'Full device snapshot (CHECK#)',      icon: 'fact_check',     colorClass: 'card-blue',   permission: 'Gps.CommandSend',            requiresCutoff: false, requiresRelay: false, needsAttributes: false },
    { type: 'iccid',          label: 'ICCID',             description: 'SIM ICCID (ICCID#)',                 icon: 'sim_card',       colorClass: 'card-blue',   permission: 'Gps.CommandSend',            requiresCutoff: false, requiresRelay: false, needsAttributes: false },
    { type: 'imsi',           label: 'IMSI',              description: 'SIM IMSI (IMSI#)',                   icon: 'pin',            colorClass: 'card-blue',   permission: 'Gps.CommandSend',            requiresCutoff: false, requiresRelay: false, needsAttributes: false },
    { type: 'apn',            label: 'APN',               description: 'Query current APN (APN#)',           icon: 'settings_ethernet', colorClass: 'card-blue', permission: 'Gps.CommandSend',         requiresCutoff: false, requiresRelay: false, needsAttributes: false },
    { type: 'gprsset',        label: 'GPRSSET',           description: 'Network settings snapshot (GPRSSET#)', icon: 'cell_tower',  colorClass: 'card-blue',   permission: 'Gps.CommandSend',            requiresCutoff: false, requiresRelay: false, needsAttributes: false },
    { type: 'server',         label: 'Network Settings',  description: 'APN/server via GPRSSET# (SERVER is set-only)', icon: 'dns', colorClass: 'card-blue', permission: 'Gps.CommandSend', requiresCutoff: false, requiresRelay: false, needsAttributes: false },
    { type: 'relayOn',        label: 'Relay ON',          description: 'Activate connected relay/output',   icon: 'toggle_on',      colorClass: 'card-green',  permission: 'Gps.CommandRelay',           requiresCutoff: false, requiresRelay: true,  needsAttributes: false },
    { type: 'relayOff',       label: 'Relay OFF',         description: 'Deactivate connected relay/output', icon: 'toggle_off',     colorClass: 'card-red',    permission: 'Gps.CommandRelay',           requiresCutoff: false, requiresRelay: true,  needsAttributes: false },
    { type: 'buzzer',         label: 'Buzzer',            description: 'Trigger buzzer (may not be supported on this model)', icon: 'campaign', colorClass: 'card-gray', permission: 'Gps.CommandBuzzer', requiresCutoff: false, requiresRelay: false, needsAttributes: false },
    { type: 'customSms',      label: 'Custom SMS',        description: 'Queue a raw SMS command (delivery not yet configured)', icon: 'sms', colorClass: 'card-gray', permission: 'Gps.CommandCustomSms', requiresCutoff: false, requiresRelay: false, needsAttributes: true },
    { type: 'custom',         label: 'Custom Command',    description: 'Send a raw command string',         icon: 'terminal',       colorClass: 'card-gray',   permission: 'Gps.CommandSend',            requiresCutoff: false, requiresRelay: false, needsAttributes: true },
  ];

  commandCards: CommandCard[] = [...this.fallbackCards];
  /** All library/fallback cards before per-device filtering. */
  private allCommandCards: CommandCard[] = [...this.fallbackCards];
  libraryReady = false;

  private refreshTimer: ReturnType<typeof setInterval> | null = null;
  private pendingDeepLinkCommand: string | null = null;

  constructor(
    private gps: GpsTrackingService,
    private auth: AuthService,
    private toast: UiToastService,
    private route: ActivatedRoute
  ) {}

  ngOnInit(): void {
    const deepLinkDeviceId = Number(this.route.snapshot.queryParamMap.get('deviceId')) || null;
    this.pendingDeepLinkCommand = this.route.snapshot.queryParamMap.get('command');

    forkJoin({
      lib: this.gps.getCommandLibrary().pipe(catchError(() => of([] as GpsCommandLibraryItem[]))),
      devices: this.gps.getDevices().pipe(catchError(err => {
        this.loadError = err?.error?.message || err?.message || 'Failed to load devices.';
        return of([] as GpsDevice[]);
      }))
    }).subscribe(({ lib, devices }) => {
      if (lib?.length) {
        this.allCommandCards = this.mergeLibraryCards(lib);
      } else {
        this.allCommandCards = [...this.fallbackCards];
      }
      this.libraryReady = true;

      this.devices = devices.filter(x => x.isActive);
      if (this.devices.length) {
        const match = deepLinkDeviceId && this.devices.some(x => x.id === deepLinkDeviceId);
        this.selectedDeviceId = match ? deepLinkDeviceId : this.devices[0].id;
        this.refreshDeviceCommandCards();
        this.loadCommands();
        this.tryApplyDeepLinkCommand();
      } else {
        this.commandCards = [...this.allCommandCards];
      }
    });
    this.refreshTimer = setInterval(() => {
      this.refreshSelectedDeviceTelemetry();
      this.loadCommands();
    }, 15_000);
  }

  /** Keep Ignition / Relay badges in sync — devices were previously loaded once and went stale. */
  private refreshSelectedDeviceTelemetry(): void {
    if (!this.selectedDeviceId) return;
    this.gps.getDevices().pipe(catchError(() => of([] as GpsDevice[]))).subscribe(devices => {
      const active = (devices || []).filter(x => x.isActive);
      if (!active.length) return;
      this.devices = active;
      if (!this.devices.some(d => d.id === this.selectedDeviceId)) {
        this.selectedDeviceId = this.devices[0].id;
        this.refreshDeviceCommandCards();
      }
    });
  }

  private tryApplyDeepLinkCommand(): void {
    if (!this.pendingDeepLinkCommand || !this.libraryReady) return;
    const key = this.pendingDeepLinkCommand;
    this.pendingDeepLinkCommand = null;
    const card = this.commandCards.find(c => c.type === key);
    if (card && !this.isCardDisabled(card)) {
      this.send(card);
    } else if (card) {
      const reason = this.cardDisabledReason(card);
      this.toast.warning(reason || `Cannot open command "${key}" for this device.`);
    } else {
      this.toast.warning(`Unknown or unsupported command "${key}" for this device.`);
    }
  }

  /** Reload per-device supported commands and filter visible cards. */
  private refreshDeviceCommandCards(): void {
    const deviceId = this.selectedDeviceId;
    if (!deviceId) {
      this.commandCards = [...this.allCommandCards];
      return;
    }

    this.gps.getSupportedCommands(deviceId).pipe(
      catchError(() => of([] as SupportedCommand[]))
    ).subscribe(supported => {
      if (!supported?.length) {
        // API empty/failed — fall back to capability-gated full library.
        this.commandCards = [...this.allCommandCards];
        return;
      }
      const available = new Set(
        supported.filter(s => s.available).map(s => s.type)
      );
      this.commandCards = this.allCommandCards.filter(c => available.has(c.type));
    });
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
          needsAttributes: ATTR_COMMANDS.has(x.commandKey) || !!known?.needsAttributes
        } as CommandCard;
      });
  }

  ngOnDestroy(): void {
    if (this.refreshTimer) clearInterval(this.refreshTimer);
  }

  loadCommands(): void {
    if (!this.selectedDeviceId) return;
    this.loading = true;
    this.loadError = null;
    const status = this.statusFilter === 'all' ? undefined : this.statusFilter;
    forkJoin({
      filtered: this.gps.getCommands(this.selectedDeviceId, { status }).pipe(
        catchError(err => {
          this.loadError = err?.error?.message || err?.message || 'Failed to load command history.';
          return of([] as GpsDeviceCommand[]);
        })
      ),
      all: this.gps.getCommands(this.selectedDeviceId).pipe(catchError(() => of([] as GpsDeviceCommand[])))
    }).pipe(finalize(() => { this.loading = false; })).subscribe(({ filtered, all }) => {
      this.commands = filtered;
      this.allCommandsForKpi = all;
    });
  }

  setStatusFilter(filter: StatusFilter): void {
    this.statusFilter = filter;
    this.loadCommands();
  }

  get kpiTiles() {
    const source = this.allCommandsForKpi.length ? this.allCommandsForKpi : this.commands;
    const count = (status: string) => source.filter(c => c.status === status).length;
    return [
      { filter: 'all' as StatusFilter,      label: 'Total',     value: source.length, icon: 'list',           color: '#64748b' },
      { filter: 'PendingApproval' as StatusFilter, label: 'Awaiting', value: count('PendingApproval'), icon: 'pending_actions', color: '#8b5cf6' },
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
    this.attrData = '';
    this.attrServer = '';
    this.attrPort = '';
    this.attrInterval = '';
    this.attrTimezone = '';
  }

  canConfirm(): boolean {
    if (!this.pendingCard) return false;
    if (!this.selectedReason) return false;
    const t = this.pendingCard.type;
    if (t === 'customSms' && (!this.smsPhone.trim() || !this.smsMessage.trim())) return false;
    if (t === 'custom' && !this.attrData.trim()) return false;
    if (t === 'heartbeat' && !this.attrInterval.trim()) return false;
    if (t === 'timezone' && !this.attrTimezone.trim()) return false;
    return true;
  }

  private buildAttributes(): Record<string, string> | undefined {
    if (!this.pendingCard) return undefined;
    const t = this.pendingCard.type;
    if (t === 'customSms') return { phone: this.smsPhone.trim(), message: this.smsMessage.trim() };
    if (t === 'custom') return { data: this.attrData.trim() };
    if (t === 'heartbeat') return { interval: this.attrInterval.trim() };
    if (t === 'timezone') return { timezone: this.attrTimezone.trim() };
    return undefined;
  }

  confirmSend(): void {
    if (!this.pendingCard || !this.selectedDeviceId || !this.canConfirm()) return;
    this.sending = true;
    const attributes = this.buildAttributes();
    const label = this.pendingCard.label;

    this.gps.sendCommand(this.selectedDeviceId, this.pendingCard.type, this.selectedReason, attributes).subscribe({
      next: (id) => {
        this.gps.getCommandById(id).subscribe({
          next: (detail) => {
            this.toast.success(this.messageForCommandStatus(detail?.command?.status, label));
            this.sending = false;
            this.pendingCard = null;
            this.loadCommands();
          },
          error: () => {
            this.toast.success(`${label} submitted`);
            this.sending = false;
            this.pendingCard = null;
            this.loadCommands();
          }
        });
      },
      error: err => {
        this.toast.error(err?.error?.message ?? 'Command failed');
        this.sending = false;
      }
    });
  }

  private messageForCommandStatus(status: string | undefined, label: string): string {
    switch (status) {
      case 'PendingApproval':
        return `${label} queued for approval`;
      case 'sent':
      case 'acknowledged':
        return `${label} sent to device`;
      case 'not_configured':
        return `${label} logged — delivery not configured`;
      case 'failed':
        return `${label} failed to reach device`;
      case 'pending':
        return `${label} queued — will retry if needed`;
      default:
        return `${label} submitted`;
    }
  }

  cancelSend(): void {
    this.pendingCard = null;
    this.selectedReason = '';
  }

  canRetry(c: GpsDeviceCommand): boolean {
    return (c.status === 'failed' || c.status === 'timeout') && this.auth.hasPermission('Gps.CommandRetry');
  }

  canCancel(c: GpsDeviceCommand): boolean {
    return (c.status === 'pending' || c.status === 'PendingApproval')
      && this.auth.hasPermission('Gps.CommandCancel');
  }

  canApprove(c: GpsDeviceCommand): boolean {
    return c.status === 'PendingApproval' && this.auth.hasPermission('Gps.CommandApprove');
  }

  retry(c: GpsDeviceCommand): void {
    if (!confirm(`Retry "${c.commandType}" on this device?`)) return;
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

  approve(c: GpsDeviceCommand): void {
    if (!confirm(`Approve and send "${c.commandType}" to the device?`)) return;
    this.gps.approveCommand(c.id, true).subscribe({
      next: () => {
        this.toast.success('Approved — dispatching to device');
        this.loadCommands();
      },
      error: err => this.toast.error(err?.error?.message ?? 'Approve failed')
    });
  }

  reject(c: GpsDeviceCommand): void {
    if (!confirm(`Reject "${c.commandType}"?`)) return;
    this.gps.approveCommand(c.id, false, 'Rejected by operator').subscribe({
      next: () => {
        this.toast.success('Command rejected');
        this.loadCommands();
      },
      error: err => this.toast.error(err?.error?.message ?? 'Reject failed')
    });
  }

  statusBadgeClass(status: string): string {
    if (status === 'PendingApproval') return 'badge-purple';
    if (status === 'pending') return 'badge-amber';
    if (status === 'sent' || status === 'acknowledged') return 'badge-green';
    if (status === 'failed' || status === 'timeout') return 'badge-red';
    if (status === 'cancelled' || status === 'not_configured') return 'badge-gray';
    return 'badge-gray';
  }

  selectedDevice(): GpsDevice | undefined {
    return this.devices.find(d => d.id === this.selectedDeviceId);
  }

  onDeviceChange(): void {
    this.refreshDeviceCommandCards();
    this.loadCommands();
  }
}
