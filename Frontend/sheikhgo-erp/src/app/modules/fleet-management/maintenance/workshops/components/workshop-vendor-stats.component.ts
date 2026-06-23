import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { WorkshopVendorStats } from '../../../../../core/models/maintenance.model';

@Component({
  selector: 'workshop-vendor-stats',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="stats">
      <div class="stat"><span class="stat__value">{{ stats()?.totalWorkshops ?? 0 }}</span><span class="stat__label">Total Workshops</span></div>
      <div class="stat stat--green"><span class="stat__value">{{ stats()?.activeWorkshops ?? 0 }}</span><span class="stat__label">Active Workshops</span></div>
      <div class="stat stat--blue"><span class="stat__value">{{ stats()?.totalVendors ?? 0 }}</span><span class="stat__label">Total Vendors</span></div>
      <div class="stat stat--amber"><span class="stat__value">{{ stats()?.preferredVendors ?? 0 }}</span><span class="stat__label">Preferred Vendors</span></div>
    </div>
  `,
  styles: [`
    .stats { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 0.75rem; margin-bottom: 1rem; }
    .stat { background: #fff; border: 1px solid #e2e8f0; border-radius: 12px; padding: 0.875rem 1rem; }
    .stat--green { border-color: #b8e6d4; background: #f0fdf8; }
    .stat--blue { border-color: #bfdbfe; background: #eff6ff; }
    .stat--amber { border-color: #fde68a; background: #fffbeb; }
    .stat__value { display: block; font-size: 1.5rem; font-weight: 800; color: #0B6B50; line-height: 1.1; }
    .stat--blue .stat__value { color: #1d4ed8; }
    .stat--amber .stat__value { color: #b45309; }
    .stat__label { display: block; margin-top: 0.25rem; font-size: 0.6875rem; font-weight: 700; text-transform: uppercase; color: #64748b; }
    @media (max-width: 767px) { .stats { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
  `]
})
export class WorkshopVendorStatsComponent {
  readonly stats = input<WorkshopVendorStats | null>(null);
}
