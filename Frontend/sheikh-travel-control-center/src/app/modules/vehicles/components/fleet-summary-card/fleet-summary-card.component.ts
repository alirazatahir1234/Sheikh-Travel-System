import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { NgClass } from '@angular/common';

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
}

@Component({
  selector: 'fleet-summary-card',
  standalone: true,
  imports: [MatIconModule, NgClass],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="rounded-xl border border-fleet-border bg-white p-5 transition-shadow hover:shadow-md"
      [class.border-l-4]="card().alert"
      [class.border-l-fleet-error]="card().alert">
      <div class="mb-4 flex items-start justify-between">
        <div class="rounded-lg bg-fleet-primary/10 p-2.5 text-fleet-primary">
          <mat-icon>{{ card().icon }}</mat-icon>
        </div>
        @if (card().trend) {
          <span
            class="inline-flex items-center gap-0.5 rounded-full px-2 py-0.5 text-[11px] font-bold"
            [ngClass]="card().trendUp ? 'bg-emerald-50 text-emerald-700' : 'bg-fleet-surface-muted text-fleet-text-muted'">
            @if (card().trendUp) { <mat-icon class="!text-[14px]">trending_up</mat-icon> }
            {{ card().trend }}
          </span>
        }
        @if (card().alert) {
          <span class="text-[10px] font-bold uppercase tracking-wide text-fleet-error">Requires attention</span>
        }
      </div>
      <p class="text-[11px] font-bold uppercase tracking-wider text-fleet-text-muted">{{ card().title }}</p>
      <h3 class="mt-1 text-3xl font-bold text-fleet-text">{{ card().value }}</h3>
      @if (card().subtext) {
        <p class="mt-1 text-xs text-fleet-text-muted">{{ card().subtext }}</p>
      }
      @if (card().progress != null) {
        <div class="mt-3">
          <div class="h-1.5 overflow-hidden rounded-full bg-fleet-surface-muted">
            <div class="h-full rounded-full bg-fleet-primary transition-all" [style.width.%]="card().progress"></div>
          </div>
          <p class="mt-1 text-[10px] font-semibold uppercase text-fleet-text-muted">{{ card().progress }}% utilization</p>
        </div>
      }
      @if (card().actionLabel) {
        <button type="button" class="mt-2 text-xs font-semibold text-fleet-primary hover:underline">{{ card().actionLabel }}</button>
      }
    </div>
  `,
  styles: [`mat-icon { display: inline-flex; }`]
})
export class FleetSummaryCardComponent {
  readonly card = input.required<FleetSummaryCardData>();
}
