import { Component, OnInit } from '@angular/core';
import { GpsTrackingService } from '../../../core/services/gps-tracking.service';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { GpsDevice, GpsDeviceCommand } from '../../../core/models/gps-tracking.model';

@Component({
  selector: 'app-gps-commands',
  templateUrl: './gps-commands.component.html',
  styleUrls: ['./gps-commands.component.scss']
})
export class GpsCommandsComponent implements OnInit {
  devices: GpsDevice[] = [];
  selectedDeviceId: number | null = null;
  commands: GpsDeviceCommand[] = [];
  loading = false;

  constructor(
    private gps: GpsTrackingService,
    private toast: UiToastService
  ) {}

  ngOnInit(): void {
    this.gps.getDevices().subscribe({
      next: d => {
        this.devices = d.filter(x => x.isActive);
        if (d.length) {
          this.selectedDeviceId = d[0].id;
          this.loadCommands();
        }
      }
    });
  }

  loadCommands(): void {
    if (!this.selectedDeviceId) return;
    this.loading = true;
    this.gps.getCommands(this.selectedDeviceId).subscribe({
      next: c => { this.commands = c; this.loading = false; },
      error: () => { this.loading = false; }
    });
  }

  send(commandType: 'engineStop' | 'engineResume'): void {
    if (!this.selectedDeviceId) return;
    const device = this.devices.find(d => d.id === this.selectedDeviceId);
    if (commandType === 'engineStop' && device && !device.supportsEngineCutoff) {
      this.toast.warning('This device does not support engine cut-off');
      return;
    }
    if (!confirm(`Send ${commandType} to ${device?.name}?`)) return;

    this.gps.sendCommand(this.selectedDeviceId, commandType).subscribe({
      next: () => {
        this.toast.success('Command queued');
        this.loadCommands();
      },
      error: err => {
        this.toast.error(err?.error?.message ?? 'Command failed');
      }
    });
  }

  selectedDevice(): GpsDevice | undefined {
    return this.devices.find(d => d.id === this.selectedDeviceId);
  }
}
