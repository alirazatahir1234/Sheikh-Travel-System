import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { NgClass } from '@angular/common';

export type FleetSummaryCardVariant = 'neutral' | 'operational' | 'risk';

export interface FleetSummaryCardData {
  icon: string;
  title: string;
  value: string | number;
  trend?: string;
  trendUp?: boolean;
  subtext?: string;
  progress?: number;
  alert?: boolean;
  actionLabel?: string;
  /** Visual group: operational (green/blue), risk (amber/red), or neutral. */
  variant?: FleetSummaryCardVariant;
}

@Component({
  selector: 'fleet-summary-card',
  standalone: true,
  imports: [MatIconModule, NgClass],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="summary-card rounded-xl border border-fleet-border bg-white p-3.5 transition-shadow hover:shadow-md"
      [ngClass]="'summary-card--' + (card().variant ?? 'neutral')"
      [class.border-l-4]="card().alert || card().variant === 'risk'"
      [class.border-l-fleet-error]="card().alert || card().variant === 'risk'">
      <div class="mb-2 flex items-start justify-between gap-2">
        <div class="summary-card__icon rounded-lg p-2">
          <mat-icon>{{ card().icon }}</mat-icon>
        </div>
        @if (card().trend) {
          <span
            class="inline-flex items-center gap-0.5 rounded-full px-2 py-0.5 text-[11px] font-bold"
            [ngClass]="card().trendUp ? 'bg-emerald-50 text-emerald-700' : 'bg-fleet-surface-muted text-fleet-text-muted'">
            @if (card().trendUp) { <mat-icon class="!text-[14px]">trending_up</mat-icon> }
            {{ card().trend }}
          </span>
        } @else if (card().alert) {
          <span class="text-[9px] font-bold uppercase tracking-wide text-fleet-error leading-tight text-right">Attention</span>
        }
      </div>
      <p class="text-[11px] font-bold uppercase tracking-wider text-fleet-text-muted">{{ card().title }}</p>
      <h3 class="mt-0.5 text-2xl font-bold text-fleet-text leading-tight">{{ card().value }}</h3>
      @if (card().subtext) {
        <p class="mt-0.5 text-xs text-fleet-text-muted">{{ card().subtext }}</p>
      }
      @if (card().progress != null) {
        <div class="mt-2">
          <div class="h-1 overflow-hidden rounded-full bg-fleet-surface-muted">
            <div class="h-full rounded-full bg-fleet-primary transition-all" [style.width.%]="card().progress"></div>
          </div>
          <p class="mt-0.5 text-[10px] font-semibold uppercase text-fleet-text-muted">{{ card().progress }}% utilization</p>
        </div>
      }
    </div>
  `,
  styles: [`
    mat-icon { display: inline-flex; }
    .summary-card { min-width: 0; }
    .summary-card__icon { background: rgba(100, 116, 139, 0.12); color: #64748b; }
    .summary-card--operational .summary-card__icon { background: rgba(15, 118, 110, 0.12); color: #0f766e; }
    .summary-card--operational { border-top: 2px solid #14b8a6; }
    .summary-card--risk .summary-card__icon { background: rgba(245, 158, 11, 0.14); color: #d97706; }
    .summary-card--risk { border-top: 2px solid #f59e0b; }
    @media (max-width: 767px) {
      .summary-card h3 { font-size: 1.25rem; }
      .summary-card [class*="text-[10px]"] { line-height: 1.2; }
    }
  `]
})
export class FleetSummaryCardComponent {
  readonly card = input.required<FleetSummaryCardData>();
}
