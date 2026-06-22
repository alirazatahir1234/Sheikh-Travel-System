import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { VehicleListItem } from '../../../../../core/models/vehicle.model';
import { CreateMaintenanceRequestPayload, ISSUE_CATEGORIES } from '../../../../../core/models/maintenance.model';

@Component({
  selector: 'request-create-form',
  standalone: true,
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form class="form" (ngSubmit)="submit.emit(form)">
      <h3>New Service Request</h3>
      <label>Vehicle
        <select [(ngModel)]="form.vehicleId" name="vehicleId" required>
          <option [ngValue]="0">Select vehicle</option>
          @for (v of vehicles(); track v.id) {
            <option [ngValue]="v.id">{{ v.name }} ({{ v.registrationNumber }})</option>
          }
        </select>
      </label>
      <label>Priority
        <select [(ngModel)]="form.priority" name="priority">
          <option>Low</option><option>Medium</option><option>High</option><option>Critical</option>
        </select>
      </label>
      <label>Category
        <select [(ngModel)]="form.issueCategory" name="category">
          @for (c of categories; track c) { <option [value]="c">{{ c }}</option> }
        </select>
      </label>
      <label>Type
        <select [(ngModel)]="form.requestType" name="type">
          <option>Corrective</option><option>Preventive</option><option>Breakdown</option>
        </select>
      </label>
      <label class="full">Description
        <textarea [(ngModel)]="form.description" name="description" rows="3" required></textarea>
      </label>
      <div class="actions">
        <button type="button" (click)="cancel.emit()">Cancel</button>
        <button type="submit" [disabled]="saving() || !form.vehicleId">Create Request</button>
      </div>
    </form>
  `,
  styles: [`
    .form { display: grid; grid-template-columns: repeat(2, 1fr); gap: 0.75rem; background: #fff; border: 1px solid #e2e8f0; border-radius: 14px; padding: 1.25rem; margin-bottom: 1rem; }
    h3 { grid-column: 1 / -1; margin: 0 0 0.25rem; color: #0b6b50; }
    label { display: flex; flex-direction: column; gap: 0.25rem; font-size: 0.8125rem; font-weight: 600; }
    .full { grid-column: 1 / -1; }
    select, textarea { border: 1px solid #e2e8f0; border-radius: 8px; padding: 0.5rem; }
    .actions { grid-column: 1 / -1; display: flex; gap: 0.5rem; justify-content: flex-end; }
    button { border-radius: 8px; padding: 0.5rem 1rem; font-weight: 700; cursor: pointer; border: 1px solid #e2e8f0; background: #fff; }
    button[type="submit"] { background: #0b6b50; color: #fff; border-color: #0b6b50; }
    button:disabled { opacity: 0.5; cursor: not-allowed; }
  `]
})
export class RequestCreateFormComponent {
  readonly categories = ISSUE_CATEGORIES;
  readonly vehicles = input<VehicleListItem[]>([]);
  readonly saving = input(false);
  readonly submit = output<CreateMaintenanceRequestPayload>();
  readonly cancel = output<void>();

  form: CreateMaintenanceRequestPayload = {
    vehicleId: 0,
    requestType: 'Corrective',
    priority: 'Medium',
    issueCategory: 'Engine',
    description: ''
  };
}
