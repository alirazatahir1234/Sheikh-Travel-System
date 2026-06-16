import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { UiStatusBadgeComponent } from '../../../../../shared/components/ui/status-badge/ui-status-badge.component';

@Component({
  selector: 'app-wizard-summary-panel',
  standalone: true,
  imports: [MatIconModule, UiStatusBadgeComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <aside class="sticky top-4 rounded-lg border border-fleet-border bg-white p-5 shadow-sm">
      <div class="mb-4 flex aspect-video items-center justify-center overflow-hidden rounded-md bg-fleet-surface-muted">
        @if (imageUrl()) {
          <img [src]="imageUrl()" alt="Vehicle preview" class="h-full w-full object-cover" />
        } @else {
          <mat-icon class="text-fleet-text-muted/40" style="font-size:48px;width:48px;height:48px;">directions_car</mat-icon>
        }
      </div>

      <h3 class="text-base font-semibold text-fleet-text">{{ displayName() }}</h3>
      <p class="text-sm text-fleet-text-muted">{{ plate() || 'No plate yet' }}</p>

      <div class="mt-3">
        @if (isDraft()) {
          <ui-status-badge status="pending" label="DRAFT" />
        } @else {
          <ui-status-badge status="valid" label="ACTIVE" />
        }
      </div>

      <dl class="mt-5 space-y-3 text-sm">
        <div class="flex justify-between gap-2">
          <dt class="text-fleet-text-muted">Tracker</dt>
          <dd class="font-medium" [class.text-emerald-600]="gpsAssigned()" [class.text-amber-600]="!gpsAssigned()">
            {{ gpsAssigned() ? 'Assigned' : 'Unassigned' }}
          </dd>
        </div>
        <div class="flex justify-between gap-2">
          <dt class="text-fleet-text-muted">Documents</dt>
          <dd class="font-medium text-fleet-text">{{ documentsCount() }} / 3 uploaded</dd>
        </div>
        <div class="flex justify-between gap-2">
          <dt class="text-fleet-text-muted">Fuel</dt>
          <dd class="font-medium text-fleet-text">{{ fuelLabel() }}</dd>
        </div>
      </dl>

      @if (validationErrors().length) {
        <div class="mt-5 rounded-md border border-red-200 bg-red-50 p-3">
          <p class="mb-2 text-xs font-semibold uppercase tracking-wide text-red-700">Missing required</p>
          <ul class="space-y-1 text-sm text-red-700">
            @for (err of validationErrors(); track err) {
              <li class="flex items-start gap-1.5">
                <mat-icon class="!text-[16px] shrink-0">error_outline</mat-icon>
                <span>{{ err }}</span>
              </li>
            }
          </ul>
        </div>
      }
    </aside>
  `
})
export class WizardSummaryPanelComponent {
  readonly displayName = input('New Vehicle');
  readonly plate = input('');
  readonly fuelLabel = input('Petrol');
  readonly isDraft = input(true);
  readonly gpsAssigned = input(false);
  readonly documentsCount = input(0);
  readonly validationErrors = input<string[]>([]);
  readonly imageUrl = input<string | undefined>();
}
