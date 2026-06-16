import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { UiButtonComponent } from '../../../../shared/components/ui/button/ui-button.component';

@Component({
  selector: 'vehicle-bulk-toolbar',
  standalone: true,
  imports: [MatIconModule, UiButtonComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (count() > 0) {
      <div class="sticky top-0 z-20 flex flex-wrap items-center gap-3 rounded-xl border border-fleet-primary/30 bg-fleet-primary-soft px-4 py-3">
        <span class="text-sm font-bold text-fleet-primary">{{ count() }} Selected</span>
        <div class="flex flex-wrap items-center gap-2">
          <ui-button variant="outline" size="sm" icon="person_add" (clicked)="assign.emit()">Assign Driver</ui-button>
          <ui-button variant="outline" size="sm" icon="download" (clicked)="export.emit()">Export</ui-button>
          <ui-button variant="outline" size="sm" icon="build" (clicked)="scheduleMaintenance.emit()">Schedule Maintenance</ui-button>
          <ui-button variant="danger" size="sm" icon="delete" (clicked)="delete.emit()">Delete</ui-button>
        </div>
        <button type="button" class="ml-auto text-sm font-semibold text-fleet-text-muted hover:text-fleet-text" (click)="clear.emit()">
          Clear selection
        </button>
      </div>
    }
  `
})
export class VehicleBulkToolbarComponent {
  readonly count = input(0);
  readonly assign = output<void>();
  readonly export = output<void>();
  readonly delete = output<void>();
  readonly scheduleMaintenance = output<void>();
  readonly clear = output<void>();
}
