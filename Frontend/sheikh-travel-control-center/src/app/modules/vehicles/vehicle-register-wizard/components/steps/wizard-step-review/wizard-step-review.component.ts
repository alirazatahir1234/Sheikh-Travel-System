import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { fuelTypeLabel } from '../../../models/vehicle-wizard.model';
import { UiStatusBadgeComponent } from '../../../../../../shared/components/ui/status-badge/ui-status-badge.component';

@Component({
  selector: 'app-wizard-step-review',
  standalone: true,
  imports: [UiStatusBadgeComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="rounded-lg border border-fleet-border bg-white p-6 shadow-sm">
      <h2 class="mb-1 text-lg font-semibold text-fleet-text">
        {{ isDraft() ? 'Review & Publish' : 'Review & Save' }}
      </h2>
      <p class="mb-5 text-sm text-fleet-text-muted">
        {{ isDraft()
          ? 'Confirm all details before publishing to the fleet registry.'
          : 'Confirm your updates before saving changes to this vehicle.' }}
      </p>

      @if (isDraft()) {
        <div class="mb-4">
          <ui-status-badge status="pending" label="DRAFT — will become Available on publish" />
        </div>
      }

      <dl class="grid gap-4 md:grid-cols-2">
        <div>
          <dt class="text-xs font-semibold uppercase text-fleet-text-muted">Name</dt>
          <dd class="text-sm font-medium text-fleet-text">{{ v()['name'] || '—' }}</dd>
        </div>
        <div>
          <dt class="text-xs font-semibold uppercase text-fleet-text-muted">Plate</dt>
          <dd class="text-sm font-medium text-fleet-text">{{ v()['registrationNumber'] || '—' }}</dd>
        </div>
        <div>
          <dt class="text-xs font-semibold uppercase text-fleet-text-muted">Code</dt>
          <dd class="text-sm font-medium text-fleet-text">{{ v()['vehicleCode'] || '—' }}</dd>
        </div>
        <div>
          <dt class="text-xs font-semibold uppercase text-fleet-text-muted">VIN</dt>
          <dd class="text-sm font-medium text-fleet-text">{{ v()['vin'] || '—' }}</dd>
        </div>
        <div>
          <dt class="text-xs font-semibold uppercase text-fleet-text-muted">Make / Model</dt>
          <dd class="text-sm font-medium text-fleet-text">{{ v()['make'] || '—' }} {{ v()['model'] || '' }}</dd>
        </div>
        <div>
          <dt class="text-xs font-semibold uppercase text-fleet-text-muted">Year / Color</dt>
          <dd class="text-sm font-medium text-fleet-text">{{ v()['year'] || '—' }} · {{ v()['color'] || '—' }}</dd>
        </div>
        <div>
          <dt class="text-xs font-semibold uppercase text-fleet-text-muted">Fuel</dt>
          <dd class="text-sm font-medium text-fleet-text">{{ fuelLabel() }} · {{ v()['fuelAverage'] || '—' }} km/L</dd>
        </div>
        <div>
          <dt class="text-xs font-semibold uppercase text-fleet-text-muted">Seating</dt>
          <dd class="text-sm font-medium text-fleet-text">{{ v()['seatingCapacity'] || '—' }}</dd>
        </div>
        <div>
          <dt class="text-xs font-semibold uppercase text-fleet-text-muted">Engine / Chassis</dt>
          <dd class="text-sm font-medium text-fleet-text">{{ v()['engineNo'] || '—' }} / {{ v()['chassisNo'] || '—' }}</dd>
        </div>
        <div>
          <dt class="text-xs font-semibold uppercase text-fleet-text-muted">Purchase Price</dt>
          <dd class="text-sm font-medium text-fleet-text">{{ v()['purchasePrice'] || '—' }}</dd>
        </div>
        <div>
          <dt class="text-xs font-semibold uppercase text-fleet-text-muted">GPS Tracker</dt>
          <dd class="text-sm font-medium" [class.text-emerald-600]="gpsAssigned()">{{ gpsAssigned() ? 'Assigned' : 'Not assigned' }}</dd>
        </div>
        <div>
          <dt class="text-xs font-semibold uppercase text-fleet-text-muted">Documents</dt>
          <dd class="text-sm font-medium text-fleet-text">{{ documentsCount() }} / 3 uploaded</dd>
        </div>
      </dl>

      @if (showValidationErrors() && validationErrors().length) {
        <div class="mt-6 rounded-md border border-red-200 bg-red-50 p-4">
          <p class="mb-2 text-sm font-semibold text-red-700">
            {{ isDraft() ? 'Cannot publish until resolved:' : 'Cannot save until resolved:' }}
          </p>
          <ul class="list-disc pl-5 text-sm text-red-700">
            @for (e of validationErrors(); track e) {
              <li>{{ e }}</li>
            }
          </ul>
        </div>
      }
    </section>
  `
})
export class WizardStepReviewComponent {
  readonly formValues = input<Record<string, unknown>>({});
  readonly isDraft = input(true);
  readonly gpsAssigned = input(false);
  readonly documentsCount = input(0);
  readonly validationErrors = input<string[]>([]);
  readonly showValidationErrors = input(false);

  readonly v = computed(() => this.formValues());
  readonly fuelLabel = computed(() => fuelTypeLabel(this.v()['fuelType'] as string | number));
}
