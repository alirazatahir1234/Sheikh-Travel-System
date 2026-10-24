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
      <div class="flex flex-wrap items-center justify-between gap-3 mb-4">
        <div>
          <h3 class="text-lg font-semibold text-text">Departments</h3>
          <p class="text-sm text-text-muted">{{ departments().length }} department(s) in this branch</p>
        </div>
        @if (canManageDepartments()) {
          <button
            type="button"
            (click)="addDepartment.emit()"
            class="flex items-center gap-2 px-3 py-2 text-sm font-medium text-white bg-primary-600 rounded-lg hover:bg-primary-700 transition-colors"
          >
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
            </svg>
            Add department
          </button>
        }
      </div>

      @if (departments().length === 0) {
        <div class="bg-surface-alt rounded-lg border border-dashed border-border p-8 text-center">
          <div class="mx-auto mb-3 w-12 h-12 rounded-lg bg-white border border-border flex items-center justify-center">
            <svg class="w-6 h-6 text-text-soft" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 7v10a2 2 0 002 2h14a2 2 0 002-2V9a2 2 0 00-2-2h-6l-2-2H5a2 2 0 00-2 2z" />
            </svg>
          </div>
          <p class="text-sm font-medium text-text">No departments yet</p>
          <p class="text-sm text-text-muted mt-1">Assign a department to organize staff under this branch.</p>
          @if (canManageDepartments()) {
            <button
              type="button"
              (click)="addDepartment.emit()"
              class="mt-3 text-sm text-primary-600 font-medium hover:text-primary-700"
            >
              Add your first department
            </button>
          }
        </div>
      } @else {
        <div class="space-y-2">
          @for (dept of departments(); track dept.id) {
            <div
              class="flex items-center justify-between gap-3 p-3 md:p-4 bg-surface-alt rounded-lg border border-border hover:border-primary-200 transition-colors cursor-pointer"
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
                @if (canManageDepartments()) {
                  <button
                    type="button"
                    class="p-1 text-text-muted hover:text-text rounded"
                    (click)="$event.stopPropagation(); editDepartment.emit(dept)"
                    title="Rename department"
                  >
                    <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15.232 5.232l3.536 3.536m-2.036-5.036a2.5 2.5 0 113.536 3.536L6.5 21.036H3v-3.572L16.732 3.732z" />
                    </svg>
                  </button>
                }
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
  readonly canManageDepartments = input(true);

  readonly addDepartment = output<void>();
  readonly editDepartment = output<Department>();
  readonly departmentSelected = output<Department>();
}
