import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { PortalVehicleDto } from '../../core/models/portal.models';

@Component({
  selector: 'app-vehicle-card-grid',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="grid gap-3 sm:grid-cols-2">
      @for (v of vehicles(); track v.id) {
        <button
          type="button"
          class="rounded-2xl border p-4 text-left transition shadow-card"
          [class.border-primary-500]="selectedId() === v.id"
          [class.bg-primary-50]="selectedId() === v.id"
          [class.ring-2]="selectedId() === v.id"
          [class.ring-primary-300]="selectedId() === v.id"
          [class.border-slate-200]="selectedId() !== v.id"
          [class.opacity-50]="maxPassengers() > v.seatingCapacity"
          [disabled]="maxPassengers() > v.seatingCapacity"
          (click)="select.emit(v.id)"
        >
          <p class="font-bold text-slate-900">{{ v.name }}</p>
          @if (v.model || v.year) {
            <p class="text-sm text-slate-600">{{ v.model }} @if (v.year) { · {{ v.year }} }</p>
          }
          <p class="mt-2 text-xs text-slate-500">{{ v.registrationNumber }}</p>
          <div class="mt-3 flex flex-wrap gap-2 text-xs font-semibold">
            <span class="rounded-full bg-slate-100 px-2 py-0.5">{{ v.seatingCapacity }} seats</span>
            <span class="rounded-full bg-emerald-50 px-2 py-0.5 text-emerald-800">Available</span>
          </div>
          <p class="mt-2 text-xs text-amber-700">★★★★☆ Premium fleet</p>
        </button>
      }
    </div>
  `
})
export class VehicleCardGridComponent {
  readonly vehicles = input.required<PortalVehicleDto[]>();
  readonly selectedId = input<number | null>(null);
  readonly maxPassengers = input(1);
  readonly select = output<number>();
}
