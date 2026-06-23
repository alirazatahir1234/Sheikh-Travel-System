import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { FleetKpi, KpiTone } from '../fleet-dashboard.model';

const TONE_ICON_CLASS: Record<KpiTone, string> = {
  primary: 'bg-fleet-primary/10 text-fleet-primary',
  secondary: 'bg-fleet-secondary/15 text-fleet-secondary',
  error: 'bg-fleet-error-soft text-fleet-error'
};

@Component({
  selector: 'fleet-dashboard-kpi-row',
  standalone: true,
  imports: [MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="grid grid-cols-1 gap-gutter sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-6">
      @for (kpi of kpis(); track kpi.id) {
        <div
          class="group rounded-xl border border-fleet-border bg-white p-5 transition-all hover:shadow-lg"
          [class.border-l-4]="kpi.alert"
          [class.border-l-fleet-error]="kpi.alert">
          <div class="mb-4 flex items-start justify-between">
            <div class="rounded-lg p-2 transition-transform group-hover:scale-110" [class]="iconClass(kpi.tone)">
              <mat-icon>{{ kpi.icon }}</mat-icon>
            </div>
          </div>
          <p class="mb-1 font-label text-[12px] font-bold uppercase tracking-wider text-fleet-text-muted">{{ kpi.label }}</p>
          <h4 class="text-2xl font-bold text-fleet-text">{{ kpi.value }}</h4>
          @if (kpi.trend) {
            <p class="mt-2 flex items-center gap-1 text-[11px] font-medium"
               [class.text-fleet-primary]="kpi.trendUp"
               [class.text-fleet-error]="kpi.trendUp === false"
               [class.text-fleet-text-muted]="kpi.trendUp === undefined">
              @if (kpi.trendUp !== undefined) {
                <mat-icon class="!text-[14px]">{{ kpi.trendUp ? 'trending_up' : 'trending_down' }}</mat-icon>
              }
              {{ kpi.trend }}
            </p>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    :host { display: block; }
    mat-icon { display: inline-flex; align-items: center; justify-content: center; }
  `]
})
export class DashboardKpiRowComponent {
  readonly kpis = input<FleetKpi[]>([]);

  iconClass(tone: KpiTone): string {
    return TONE_ICON_CLASS[tone];
  }
}
