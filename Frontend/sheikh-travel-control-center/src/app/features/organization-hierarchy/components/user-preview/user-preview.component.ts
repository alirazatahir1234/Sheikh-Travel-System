import {
  Component,
  ChangeDetectionStrategy,
  input,
  output,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { UserPreview, User } from '../../models/organization.models';
import { UserListItemComponent } from '../user-list-item/user-list-item.component';

@Component({
  selector: 'app-user-preview',
  standalone: true,
  imports: [CommonModule, UserListItemComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="bg-white rounded-xl border border-border overflow-hidden">
      <!-- Header -->
      <div class="px-4 py-3 border-b border-border flex items-center justify-between">
        <h3 class="text-sm font-semibold text-text">Users (Preview)</h3>
        <button
          type="button"
          (click)="addUser.emit()"
          class="flex items-center gap-1.5 px-3 py-1.5 bg-primary-600 text-white text-xs font-medium rounded-lg hover:bg-primary-700 transition-colors"
        >
          <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
          </svg>
          Add User
        </button>
      </div>

      <!-- Content -->
      <div class="divide-y divide-border">
        @for (user of preview().users; track user.id) {
          <app-user-list-item
            [user]="user"
            (userAction)="onUserAction($event, user)"
          />
        }
      </div>

      <!-- View All Link -->
      <div class="px-4 py-3 border-t border-border">
        <button
          type="button"
          (click)="viewAllUsers.emit()"
          class="text-sm font-medium text-primary-600 hover:text-primary-700 transition-colors"
        >
          VIEW ALL {{ preview().totalCount }} USERS
        </button>
      </div>
    </div>
  `,
})
export class UserPreviewComponent {
  readonly preview = input.required<UserPreview>();

  readonly addUser = output<void>();
  readonly viewAllUsers = output<void>();
  readonly userActionTriggered = output<{ action: string; user: User }>();

  onUserAction(action: string, user: User): void {
    this.userActionTriggered.emit({ action, user });
  }
}
