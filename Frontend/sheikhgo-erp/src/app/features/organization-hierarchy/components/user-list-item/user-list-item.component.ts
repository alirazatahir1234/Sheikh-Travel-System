import {
  Component,
  ChangeDetectionStrategy,
  input,
  output,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { User } from '../../models/organization.models';

@Component({
  selector: 'app-user-list-item',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="px-4 py-3 flex items-center gap-3 hover:bg-surface-alt transition-colors">
      <!-- Avatar -->
      <div class="relative flex-shrink-0">
        @if (user().avatarUrl) {
          <img
            [src]="user().avatarUrl"
            [alt]="user().fullName"
            class="w-10 h-10 rounded-full object-cover"
          />
        } @else {
          <div class="w-10 h-10 rounded-full bg-primary-100 flex items-center justify-center">
            <span class="text-sm font-semibold text-primary-700">
              {{ getInitials(user().fullName) }}
            </span>
          </div>
        }
        @if (user().isActive) {
          <span class="absolute bottom-0 right-0 w-2.5 h-2.5 bg-emerald-500 rounded-full border-2 border-white"></span>
        }
      </div>

      <!-- User Info -->
      <div class="flex-1 min-w-0">
        <div class="text-sm font-medium text-text truncate">{{ user().fullName }}</div>
        <div class="text-xs text-text-muted truncate">{{ user().role }}</div>
      </div>

      <!-- Actions Menu -->
      <div class="relative">
        <button
          type="button"
          (click)="toggleMenu($event)"
          class="w-8 h-8 rounded-lg hover:bg-gray-100 flex items-center justify-center text-text-muted hover:text-text transition-colors"
        >
          <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 5v.01M12 12v.01M12 19v.01M12 6a1 1 0 110-2 1 1 0 010 2zm0 7a1 1 0 110-2 1 1 0 010 2zm0 7a1 1 0 110-2 1 1 0 010 2z" />
          </svg>
        </button>

        @if (menuOpen()) {
          <div
            class="absolute right-0 top-full mt-1 w-36 bg-white rounded-lg shadow-lg border border-border py-1 z-10"
            (click)="closeMenu()"
          >
            <button
              type="button"
              (click)="emitAction('view')"
              class="w-full px-3 py-2 text-sm text-left text-text hover:bg-surface-alt transition-colors"
            >
              View Profile
            </button>
            <button
              type="button"
              (click)="emitAction('edit')"
              class="w-full px-3 py-2 text-sm text-left text-text hover:bg-surface-alt transition-colors"
            >
              Edit User
            </button>
            <button
              type="button"
              (click)="emitAction('reassign')"
              class="w-full px-3 py-2 text-sm text-left text-text hover:bg-surface-alt transition-colors"
            >
              Reassign
            </button>
            <div class="border-t border-border my-1"></div>
            <button
              type="button"
              (click)="emitAction('deactivate')"
              class="w-full px-3 py-2 text-sm text-left text-red-600 hover:bg-red-50 transition-colors"
            >
              Deactivate
            </button>
          </div>
        }
      </div>
    </div>
  `,
  host: {
    '(document:click)': 'onDocumentClick($event)',
  },
})
export class UserListItemComponent {
  readonly user = input.required<User>();

  readonly userAction = output<string>();

  readonly menuOpen = signal<boolean>(false);

  private menuButtonElement: HTMLElement | null = null;

  getInitials(name: string): string {
    const parts = name.split(' ').filter(Boolean);
    if (parts.length >= 2) {
      return `${parts[0][0]}${parts[1][0]}`.toUpperCase();
    }
    return parts[0]?.substring(0, 2).toUpperCase() ?? '';
  }

  toggleMenu(event: Event): void {
    event.stopPropagation();
    this.menuButtonElement = event.currentTarget as HTMLElement;
    this.menuOpen.update(v => !v);
  }

  closeMenu(): void {
    this.menuOpen.set(false);
  }

  emitAction(action: string): void {
    this.userAction.emit(action);
    this.closeMenu();
  }

  onDocumentClick(event: Event): void {
    if (this.menuOpen() && this.menuButtonElement && !this.menuButtonElement.contains(event.target as Node)) {
      this.closeMenu();
    }
  }
}
