import {
  Component,
  ChangeDetectionStrategy,
  input,
  output,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { User } from '../../models/organization.models';

@Component({
  selector: 'app-users-tab',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div>
      <!-- Header -->
      <div class="flex items-center justify-between mb-4">
        <div>
          <h3 class="text-lg font-semibold text-text">Users</h3>
          <p class="text-sm text-text-muted">{{ totalCount() }} user(s) in this branch</p>
        </div>
        <button
          type="button"
          (click)="addUser.emit()"
          class="flex items-center gap-2 px-3 py-2 text-sm font-medium text-white bg-primary-600 rounded-lg hover:bg-primary-700 transition-colors"
        >
          <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
          </svg>
          Add User
        </button>
      </div>

      @if (users().length === 0 && !isPlaceholder()) {
        <div class="bg-surface-alt rounded-lg p-8 text-center">
          <svg class="w-12 h-12 mx-auto mb-3 text-text-soft" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4.354a4 4 0 110 5.292M15 21H3v-1a6 6 0 0112 0v1zm0 0h6v-1a6 6 0 00-9-5.197M13 7a4 4 0 11-8 0 4 4 0 018 0z" />
          </svg>
          <p class="text-sm text-text-muted">No users assigned to this branch yet.</p>
          <button
            type="button"
            (click)="addUser.emit()"
            class="mt-3 text-sm text-primary-600 font-medium hover:text-primary-700"
          >
            Add your first user
          </button>
        </div>
      } @else {
        @if (isPlaceholder()) {
          <div class="mb-4 p-3 bg-amber-50 border border-amber-100 rounded-lg">
            <p class="text-sm text-amber-700">
              <span class="font-medium">Note:</span> User details require backend integration.
              Showing placeholder data for {{ totalCount() }} users.
            </p>
          </div>
        }

        <div class="space-y-2">
          @for (user of users(); track user.id) {
            <div
              class="flex items-center justify-between p-3 bg-surface-alt rounded-lg border border-border/50 hover:border-border transition-colors"
            >
              <div class="flex items-center gap-3">
                @if (user.avatarUrl) {
                  <img
                    [src]="user.avatarUrl"
                    [alt]="user.fullName"
                    class="w-10 h-10 rounded-full object-cover"
                  />
                } @else {
                  <div class="w-10 h-10 rounded-full bg-primary-100 flex items-center justify-center text-primary-700 font-semibold text-sm">
                    {{ getInitials(user.fullName) }}
                  </div>
                }
                <div>
                  <h4 class="text-sm font-semibold text-text">{{ user.fullName }}</h4>
                  <div class="flex items-center gap-2 text-xs text-text-muted mt-0.5">
                    <span>{{ user.role }}</span>
                    <span class="w-px h-3 bg-border"></span>
                    <span>{{ user.email }}</span>
                  </div>
                </div>
              </div>
              <div class="flex items-center gap-2">
                <span
                  class="px-2 py-0.5 text-[10px] font-medium rounded-full"
                  [class.bg-emerald-50]="user.isActive"
                  [class.text-emerald-700]="user.isActive"
                  [class.bg-gray-100]="!user.isActive"
                  [class.text-gray-600]="!user.isActive"
                >
                  {{ user.isActive ? 'Active' : 'Inactive' }}
                </span>
                <button
                  type="button"
                  class="p-1 text-text-muted hover:text-text rounded"
                  (click)="editUser.emit(user)"
                >
                  <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 5v.01M12 12v.01M12 19v.01M12 6a1 1 0 110-2 1 1 0 010 2zm0 7a1 1 0 110-2 1 1 0 010 2zm0 7a1 1 0 110-2 1 1 0 010 2z" />
                  </svg>
                </button>
              </div>
            </div>
          }
        </div>

        @if (totalCount() > users().length) {
          <div class="mt-4 text-center">
            <button
              type="button"
              (click)="viewAllUsers.emit()"
              class="text-sm text-primary-600 font-medium hover:text-primary-700"
            >
              View all {{ totalCount() }} users
            </button>
          </div>
        }
      }
    </div>
  `,
})
export class UsersTabComponent {
  readonly users = input<User[]>([]);
  readonly totalCount = input<number>(0);
  readonly isPlaceholder = input<boolean>(false);

  readonly addUser = output<void>();
  readonly editUser = output<User>();
  readonly viewAllUsers = output<void>();

  getInitials(name: string): string {
    return name
      .split(' ')
      .map(part => part[0])
      .join('')
      .toUpperCase()
      .slice(0, 2);
  }
}
