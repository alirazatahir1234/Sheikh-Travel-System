import {
  Component,
  ChangeDetectionStrategy,
  input,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { Branch } from '../../models/organization.models';

interface StatCard {
  label: string;
  value: number;
  suffix?: string;
  icon: 'users' | 'vehicles' | 'drivers' | 'compliance';
}

@Component({
  selector: 'app-statistics-cards',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="grid grid-cols-2 lg:grid-cols-4 gap-4">
      @for (stat of stats(); track stat.label) {
        <div class="bg-white rounded-xl border border-border p-4 hover:shadow-sm transition-shadow">
          <div class="flex items-center justify-between mb-2">
            <span class="text-xs font-semibold text-text-muted uppercase tracking-wide">
              {{ stat.label }}
            </span>
          </div>
          <div class="flex items-end justify-between">
            <span class="text-2xl font-bold text-text">
              {{ stat.value }}{{ stat.suffix || '' }}
            </span>
            <div [class]="getIconContainerClass(stat.icon)">
              @switch (stat.icon) {
                @case ('users') {
                  <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4.354a4 4 0 110 5.292M15 21H3v-1a6 6 0 0112 0v1zm0 0h6v-1a6 6 0 00-9-5.197M13 7a4 4 0 11-8 0 4 4 0 018 0z" />
                  </svg>
                }
                @case ('vehicles') {
                  <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path d="M9 17a2 2 0 11-4 0 2 2 0 014 0zM19 17a2 2 0 11-4 0 2 2 0 014 0z" />
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 16V6a1 1 0 00-1-1H4a1 1 0 00-1 1v10a1 1 0 001 1h1m8-1a1 1 0 01-1 1H9m4-1V8a1 1 0 011-1h2.586a1 1 0 01.707.293l3.414 3.414a1 1 0 01.293.707V16a1 1 0 01-1 1h-1m-6-1a1 1 0 001 1h1M5 17a2 2 0 104 0m-4 0a2 2 0 114 0m6 0a2 2 0 104 0m-4 0a2 2 0 114 0" />
                  </svg>
                }
                @case ('drivers') {
                  <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
                  </svg>
                }
                @case ('compliance') {
                  <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
                  </svg>
                }
              }
            </div>
          </div>
        </div>
      }
    </div>
  `,
})
export class StatisticsCardsComponent {
  readonly branch = input.required<Branch>();

  stats(): StatCard[] {
    const b = this.branch();
    return [
      { label: 'Users', value: b.userCount, icon: 'users' },
      { label: 'Vehicles', value: b.vehicleCount, icon: 'vehicles' },
      { label: 'Drivers', value: b.driverCount, icon: 'drivers' },
      { label: 'Compliance', value: b.complianceScore, suffix: '%', icon: 'compliance' },
    ];
  }

  getIconContainerClass(icon: string): string {
    const base = 'w-10 h-10 rounded-lg flex items-center justify-center';
    switch (icon) {
      case 'users':
        return `${base} bg-blue-50 text-blue-600`;
      case 'vehicles':
        return `${base} bg-purple-50 text-purple-600`;
      case 'drivers':
        return `${base} bg-amber-50 text-amber-600`;
      case 'compliance':
        return `${base} bg-emerald-50 text-emerald-600`;
      default:
        return `${base} bg-gray-50 text-gray-600`;
    }
  }
}
