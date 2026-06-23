import {
  Component,
  ChangeDetectionStrategy,
  input,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { BranchCapacity, CapacityMetric } from '../../models/organization.models';
import { ProgressBarComponent } from '../progress-bar/progress-bar.component';

@Component({
  selector: 'app-branch-capacity-card',
  standalone: true,
  imports: [CommonModule, ProgressBarComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="bg-white rounded-xl border border-border overflow-hidden">
      <!-- Header -->
      <div class="px-4 py-3 border-b border-border flex items-center justify-between">
        <div class="flex items-center gap-2">
          <div class="w-6 h-6 rounded-lg bg-primary-50 flex items-center justify-center">
            <svg class="w-3.5 h-3.5 text-primary-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
            </svg>
          </div>
          <h3 class="text-sm font-semibold text-text">Branch Capacity</h3>
        </div>
        <span class="text-xs text-text-muted">Updated {{ capacity().lastUpdated }}</span>
      </div>

      <!-- Content -->
      <div class="p-4 space-y-5">
        @for (metric of metrics(); track metric.label) {
          <div>
            <div class="flex items-center justify-between mb-2">
              <span class="text-sm font-medium text-text">{{ metric.label }}</span>
              <span class="text-sm text-text-muted">
                {{ metric.current }} / {{ metric.max }} {{ getUnit(metric.label) }} ({{ metric.percentage }}%)
              </span>
            </div>
            <app-progress-bar
              [value]="metric.percentage"
              [color]="getProgressColor(metric.percentage)"
            />
          </div>
        }
      </div>
    </div>
  `,
})
export class BranchCapacityCardComponent {
  readonly capacity = input.required<BranchCapacity>();

  metrics(): CapacityMetric[] {
    const cap = this.capacity();
    return [
      cap.staffUtilization,
      cap.fleetAllocation,
      cap.departmentSlots,
    ];
  }

  getUnit(label: string): string {
    if (label.toLowerCase().includes('staff')) return 'Users';
    if (label.toLowerCase().includes('fleet')) return 'Vehicles';
    if (label.toLowerCase().includes('department')) return 'Depts';
    return '';
  }

  getProgressColor(percentage: number): 'green' | 'yellow' | 'red' | 'blue' {
    if (percentage >= 80) return 'yellow';
    if (percentage >= 90) return 'red';
    return 'green';
  }
}
