import {
  Component,
  ChangeDetectionStrategy,
  input,
  output,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { Department } from '../../models/organization.models';

@Component({
  selector: 'app-unassigned-department',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-3">
      <!-- Header -->
      <div class="flex items-center justify-between mb-2">
        <div class="flex items-center gap-2">
          <svg class="w-4 h-4 text-amber-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 8h14M5 8a2 2 0 110-4h14a2 2 0 110 4M5 8v10a2 2 0 002 2h10a2 2 0 002-2V8m-9 4h4" />
          </svg>
          <span class="text-xs font-semibold text-text-muted uppercase tracking-wide">
            Department Pool
          </span>
          <span class="px-1.5 py-0.5 text-[10px] font-semibold text-amber-700 bg-amber-50 rounded">
            {{ departments().length }}
          </span>
          <button
            type="button"
            class="w-4 h-4 rounded-full bg-gray-200 text-gray-500 flex items-center justify-center hover:bg-gray-300 transition-colors"
            title="Departments not assigned to any branch - drag to assign"
          >
            <svg class="w-2.5 h-2.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
          </button>
        </div>
      </div>

      <!-- Department List -->
      <div class="space-y-1">
        @for (dept of departments(); track dept.id) {
          <div
            (click)="departmentSelect.emit(dept)"
            class="flex items-center gap-2 px-2 py-1.5 rounded-lg cursor-pointer hover:bg-white transition-colors group"
          >
            <!-- Drag Handle -->
            <div class="flex-shrink-0 text-text-soft opacity-0 group-hover:opacity-100 transition-opacity cursor-grab">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 8h16M4 16h16" />
              </svg>
            </div>

            <!-- Folder Icon -->
            <div class="flex-shrink-0 w-5 h-5 rounded flex items-center justify-center bg-gray-100 text-gray-500">
              <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 7v10a2 2 0 002 2h14a2 2 0 002-2V9a2 2 0 00-2-2h-6l-2-2H5a2 2 0 00-2 2z" />
              </svg>
            </div>

            <!-- Department Name -->
            <span class="flex-1 text-sm text-text truncate">{{ dept.name }}</span>
          </div>
        }
      </div>
    </div>
  `,
})
export class UnassignedDepartmentComponent {
  readonly departments = input.required<Department[]>();

  readonly departmentSelect = output<Department>();
}
