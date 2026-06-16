import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { DocumentSlotState } from '../../../models/vehicle-wizard.model';
import { DocumentUploadZoneComponent } from '../../document-upload-zone/document-upload-zone.component';

@Component({
  selector: 'app-wizard-step-documents',
  standalone: true,
  imports: [DocumentUploadZoneComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="rounded-lg border border-fleet-border bg-white p-6 shadow-sm">
      <h2 class="mb-1 text-lg font-semibold text-fleet-text">Documents</h2>
      <p class="mb-5 text-sm text-fleet-text-muted">Upload vehicle image and compliance documents.</p>

      <div class="grid gap-4 md:grid-cols-1">
        @for (slot of slots(); track slot.documentType; let i = $index) {
          <app-document-upload-zone
            [slot]="slot"
            [index]="i"
            (fileSelected)="fileSelected.emit($event)" />
        }
      </div>
    </section>
  `
})
export class WizardStepDocumentsComponent {
  readonly slots = input.required<DocumentSlotState[]>();
  readonly fileSelected = output<{ index: number; file: File }>();
}
