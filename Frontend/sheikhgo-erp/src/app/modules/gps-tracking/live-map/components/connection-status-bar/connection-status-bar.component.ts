import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { GpsConnectionState } from '../../../../../core/services/gps-realtime.service';
import { SharedModule } from '../../../../../shared/shared.module';

export type RefreshRateMs = 5000 | 10000 | 30000 | 60000 | null;

@Component({
  selector: 'app-connection-status-bar',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [SharedModule, MatIconModule],
  templateUrl: './connection-status-bar.component.html'
})
export class ConnectionStatusBarComponent {
  @Input() liveTracking = true;
  @Input() connectionState: GpsConnectionState = 'disconnected';
  @Input() apiHealthy = true;
  @Input() lastSyncLabel = '—';
  @Input() refreshRateMs: RefreshRateMs = 5000;
  @Input() loading = false;

  @Output() refresh = new EventEmitter<void>();
  @Output() refreshRateChange = new EventEmitter<RefreshRateMs>();

  readonly refreshRateOptions: { id: RefreshRateMs; label: string }[] = [
    { id: 5000, label: '5 sec' },
    { id: 10000, label: '10 sec' },
    { id: 30000, label: '30 sec' },
    { id: 60000, label: '1 min' },
    { id: null, label: 'Pause' }
  ];

  get liveStatusLabel(): string {
    if (!this.liveTracking) return 'Paused';
    if (this.connectionState === 'connected') return 'Live';
    if (this.connectionState === 'reconnecting') return 'Reconnecting';
    return 'Polling';
  }

  get liveStatusClass(): string {
    if (!this.liveTracking) return 'ft-status-pill--muted';
    if (this.connectionState === 'connected') return 'ft-status-pill--live';
    if (this.connectionState === 'reconnecting') return 'ft-status-pill--warn';
    return 'ft-status-pill--muted';
  }
}
