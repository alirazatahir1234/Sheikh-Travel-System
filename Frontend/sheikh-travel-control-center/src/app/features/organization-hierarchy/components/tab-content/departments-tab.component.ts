import {
  Component,
  ChangeDetectionStrategy,
  input,
  output,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { Department } from '../../models/organization.models';

@Component({
  selector: 'app-departments-tab',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div>
      <!-- Header -->
      <div class="flex items-center justify-between mb-4">
        <div>
          <h3 class="text-lg font-semibold text-text">Departments</h3>
          <p class="text-sm text-text-muted">{{ departments().length }} department(s) in this branch</p>
        </div>
        <button
          type="button"
          (click)="addDepartment.emit()"
          class="flex items-center gap-2 px-3 py-2 text-sm font-medium text-white bg-primary-600 rounded-lg hover:bg-primary-700 transition-colors"
        >
          <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
          </svg>
          Add Department
        </button>
      </div>

      @if (departments().length === 0) {
        <div class="bg-surface-alt rounded-lg p-8 text-center">
          <svg class="w-12 h-12 mx-auto mb-3 text-text-soft" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 7v10a2 2 0 002 2h14a2 2 0 002-2V9a2 2 0 00-2-2h-6l-2-2H5a2 2 0 00-2 2z" />
          </svg>
          <p class="text-sm text-text-muted">No departments assigned to this branch yet.</p>
          <button
            type="button"
            (click)="addDepartment.emit()"
            class="mt-3 text-sm text-primary-600 font-medium hover:text-primary-700"
          >
            Add your first department
          </button>
        </div>
      } @else {
        <div class="space-y-2">
          @for (dept of departments(); track dept.id) {
            <div
              class="flex items-center justify-between p-4 bg-surface-alt rounded-lg border border-border/50 hover:border-border transition-colors cursor-pointer"
              (click)="departmentSelected.emit(dept)"
            >
              <div class="flex items-center gap-3">
                <div class="w-10 h-10 rounded-lg bg-amber-50 border border-amber-100 flex items-center justify-center">
                  <svg class="w-5 h-5 text-amber-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 7v10a2 2 0 002 2h14a2 2 0 002-2V9a2 2 0 00-2-2h-6l-2-2H5a2 2 0 00-2 2z" />
                  </svg>
                </div>
                <div>
                  <h4 class="text-sm font-semibold text-text">{{ dept.name }}</h4>
                  <div class="flex items-center gap-3 text-xs text-text-muted mt-0.5">
                    <span>{{ dept.staffCount }} staff</span>
                    @if (dept.departmentHeadName) {
                      <span class="w-px h-3 bg-border"></span>
                      <span>Head: {{ dept.departmentHeadName }}</span>
                    }
                  </div>
                </div>
              </div>
              <div class="flex items-center gap-2">
                <span
                  class="px-2 py-0.5 text-[10px] font-medium rounded-full"
                  [class.bg-emerald-50]="dept.isActive"
                  [class.text-emerald-700]="dept.isActive"
                  [class.bg-gray-100]="!dept.isActive"
                  [class.text-gray-600]="!dept.isActive"
                >
                  {{ dept.isActive ? 'Active' : 'Inactive' }}
                </span>
                <button
                  type="button"
                  class="p-1 text-text-muted hover:text-text rounded"
                  (click)="$event.stopPropagation(); editDepartment.emit(dept)"
                >
                  <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 5v.01M12 12v.01M12 19v.01M12 6a1 1 0 110-2 1 1 0 010 2zm0 7a1 1 0 110-2 1 1 0 010 2zm0 7a1 1 0 110-2 1 1 0 010 2z" />
                  </svg>
                </button>
              </div>
            </div>
          }
        </div>
      }
    </div>
  `,
})
export class DepartmentsTabComponent {
  readonly departments = input<Department[]>([]);

  readonly addDepartment = output<void>();
  readonly editDepartment = output<Department>();
  readonly departmentSelected = output<Department>();
}
