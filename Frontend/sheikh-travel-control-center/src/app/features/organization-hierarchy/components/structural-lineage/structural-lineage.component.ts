import {
  Component,
  ChangeDetectionStrategy,
  input,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { StructuralLineage } from '../../models/organization.models';

@Component({
  selector: 'app-structural-lineage',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="bg-white rounded-xl border border-border overflow-hidden">
      <!-- Header -->
      <div class="px-4 py-3 border-b border-border">
        <h3 class="text-xs font-semibold text-text-muted uppercase tracking-wide">
          Structural Lineage
        </h3>
      </div>

      <!-- Content -->
      <div class="p-4">
        <div class="space-y-3">
          <!-- Tenant Level -->
          <div class="flex items-center gap-3">
            <div class="w-10 h-10 rounded-xl bg-primary-50 flex items-center justify-center flex-shrink-0">
              <svg class="w-5 h-5 text-primary-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
              </svg>
            </div>
            <div>
              <span class="block text-[10px] font-semibold text-text-soft uppercase tracking-wider">
                {{ lineage().tenant.type }}
              </span>
              <span class="block text-sm font-semibold text-text">
                {{ lineage().tenant.name }}
              </span>
            </div>
          </div>

          <!-- Connector Line -->
          <div class="flex items-center gap-3">
            <div class="w-10 flex justify-center">
              <div class="w-0.5 h-6 bg-border"></div>
            </div>
          </div>

          <!-- Branch Level -->
          <div class="flex items-center gap-3">
            <div class="w-10 h-10 rounded-xl bg-primary-100 flex items-center justify-center flex-shrink-0">
              <svg class="w-5 h-5 text-primary-700" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z" />
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 11a3 3 0 11-6 0 3 3 0 016 0z" />
              </svg>
            </div>
            <div>
              <span class="block text-[10px] font-semibold text-text-soft uppercase tracking-wider">
                {{ lineage().branch.type }}
              </span>
              <span class="block text-sm font-semibold text-text">
                {{ lineage().branch.name }}
              </span>
            </div>
          </div>

          <!-- Connector Line -->
          <div class="flex items-center gap-3">
            <div class="w-10 flex justify-center">
              <div class="w-0.5 h-6 bg-border"></div>
            </div>
          </div>

          <!-- Children Summary -->
          <div class="flex items-center gap-3">
            <div class="w-10 h-10 rounded-xl bg-gray-50 flex items-center justify-center flex-shrink-0">
              <svg class="w-5 h-5 text-gray-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z" />
              </svg>
            </div>
            <div>
              <span class="block text-[10px] font-semibold text-text-soft uppercase tracking-wider">
                CHILDREN
              </span>
              <span class="block text-sm font-medium text-text">
                {{ lineage().children.departmentCount }} Depts • {{ lineage().children.userCount }} Users
              </span>
            </div>
          </div>
        </div>
      </div>
    </div>
  `,
})
export class StructuralLineageComponent {
  readonly lineage = input.required<StructuralLineage>();
}
