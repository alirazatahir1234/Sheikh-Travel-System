import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { SharedModule } from '../../../../../shared/shared.module';

@Component({
  selector: 'app-fleet-tracking-header',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [SharedModule, MatIconModule],
  templateUrl: './fleet-tracking-header.component.html'
})
export class FleetTrackingHeaderComponent {
  @Input() liveTracking = true;
  @Output() liveToggle = new EventEmitter<void>();
}
