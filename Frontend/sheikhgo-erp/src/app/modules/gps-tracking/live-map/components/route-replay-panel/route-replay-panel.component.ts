import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { TripEvent, TripReplayPosition, TripStop } from '../../../../../core/models/gps-tracking.model';
import { TripReplayMapComponent } from '../../../shared/trip-replay-map/trip-replay-map.component';
import { SharedModule } from '../../../../../shared/shared.module';

export type ReplayPreset = 'today' | '24h' | '7d' | 'custom';

export interface RouteReplayData {
  routePoints: TripReplayPosition[];
  positions: TripReplayPosition[];
  stops?: TripStop[];
  events?: TripEvent[];
}

@Component({
  selector: 'app-route-replay-panel',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [SharedModule, MatIconModule, TripReplayMapComponent],
  templateUrl: './route-replay-panel.component.html'
})
export class RouteReplayPanelComponent {
  @Input() visible = false;
  @Input() vehicleName = '';
  @Input() loading = false;
  @Input() error = '';
  @Input() replayData: RouteReplayData | null = null;
  @Input() preset: ReplayPreset = 'today';

  @Output() close = new EventEmitter<void>();
  @Output() load = new EventEmitter<void>();
  @Output() presetChange = new EventEmitter<ReplayPreset>();

  readonly presets: { id: ReplayPreset; label: string }[] = [
    { id: 'today', label: 'Today' },
    { id: '24h', label: 'Last 24h' },
    { id: '7d', label: 'Last 7d' },
    { id: 'custom', label: 'Custom' }
  ];
}
