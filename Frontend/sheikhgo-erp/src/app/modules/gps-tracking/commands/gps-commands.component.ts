import { Component, OnInit, OnDestroy } from '@angular/core';
import { GpsTrackingService } from '../../../core/services/gps-tracking.service';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { GpsDevice, GpsDeviceCommand } from '../../../core/models/gps-tracking.model';

interface CommandCard {
  type: string;
  label: string;
  description: string;
  icon: string;
  colorClass: string;
  requiresCutoff: boolean;
}

@Component({
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

  readonly commandCards: CommandCard[] = [
    { type: 'engineStop',     label: 'Engine Stop',       description: 'Remotely cut engine power', icon: 'power_off',    colorClass: 'card-red',   requiresCutoff: true },
    { type: 'engineResume',   label: 'Engine Resume',     description: 'Re-enable engine power',     icon: 'power',        colorClass: 'card-green', requiresCutoff: true },
    { type: 'positionSingle', label: 'Request Position',  description: 'Get current GPS fix',        icon: 'location_on',  colorClass: 'card-blue',  requiresCutoff: false },
    { type: 'custom',         label: 'Custom Command',    description: 'Send a raw command string',  icon: 'terminal',     colorClass: 'card-gray',  requiresCutoff: false },
  ];

  private refreshTimer: ReturnType<typeof setInterval> | null = null;

  constructor(
    private gps: GpsTrackingService,
    private toast: UiToastService
  ) {}

  ngOnInit(): void {
    this.gps.getDevices().subscribe({
      next: d => {
        this.devices = d.filter(x => x.isActive);
        if (this.devices.length) {
          this.selectedDeviceId = this.devices[0].id;
          this.loadCommands();
        }
      }
    });
    this.refreshTimer = setInterval(() => this.loadCommands(), 15_000);
  }

  ngOnDestroy(): void {
    if (this.refreshTimer) clearInterval(this.refreshTimer);
  }

  loadCommands(): void {
    if (!this.selectedDeviceId) return;
    this.loading = true;
    this.gps.getCommands(this.selectedDeviceId).subscribe({
      next: c => { this.commands = c; this.loading = false; },
      error: () => { this.loading = false; }
    });
  }

  isCardDisabled(card: CommandCard): boolean {
    if (!this.selectedDeviceId || this.sending) return true;
    if (card.requiresCutoff && !this.selectedDevice()?.supportsEngineCutoff) return true;
    if (card.type === 'custom') return true;
    return false;
  }

  send(card: CommandCard): void {
    if (this.isCardDisabled(card)) {
      if (card.type === 'custom') { this.toast.info('Custom commands coming soon'); return; }
      if (card.requiresCutoff) { this.toast.warning('This device does not support engine cut-off'); return; }
      return;
    }
    const device = this.selectedDevice();
    if (!confirm(`Send "${card.label}" to ${device?.name}?`)) return;

    this.sending = true;
    this.gps.sendCommand(this.selectedDeviceId!, card.type).subscribe({
      next: () => {
        this.toast.success('Command queued');
        this.sending = false;
        this.loadCommands();
      },
      error: err => {
        this.toast.error(err?.error?.message ?? 'Command failed');
        this.sending = false;
      }
    });
  }

  statusBadgeClass(status: string): string {
    if (status === 'Pending') return 'badge-amber';
    if (status === 'Acknowledged' || status === 'Sent') return 'badge-green';
    if (status === 'Failed' || status === 'NotAcknowledged') return 'badge-red';
    return 'badge-gray';
  }

  selectedDevice(): GpsDevice | undefined {
    return this.devices.find(d => d.id === this.selectedDeviceId);
  }
}
