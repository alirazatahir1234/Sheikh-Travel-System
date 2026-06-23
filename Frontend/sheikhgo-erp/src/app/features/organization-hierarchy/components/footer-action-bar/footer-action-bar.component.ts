import {
  Component,
  ChangeDetectionStrategy,
  input,
  output,
  computed,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { PendingChange } from '../../models/organization.models';

@Component({
  selector: 'app-footer-action-bar',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="fixed bottom-0 left-0 right-0 bg-white border-t border-border shadow-lg z-50">
      <div class="max-w-screen-2xl mx-auto px-6 py-3">
        <div class="flex items-center justify-between">
          <!-- Left: Pending Changes Info -->
          <div class="flex items-center gap-4">
            @if (pendingChanges().length > 0) {
              <div class="flex items-center gap-2 text-primary-600">
                <svg class="w-4 h-4 animate-spin" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
                </svg>
                <span class="text-sm font-medium">
                  {{ pendingChanges().length }} changes pending save
                </span>
              </div>
            }

            <div class="h-4 w-px bg-border"></div>

            <span class="text-sm text-text-muted">
              Autosaved at {{ lastAutoSaved() }}
            </span>
          </div>

          <!-- Right: Action Buttons -->
          <div class="flex items-center gap-3">
            <button
              type="button"
              (click)="discard.emit()"
              [disabled]="pendingChanges().length === 0"
              class="px-5 py-2.5 text-sm font-medium text-text border border-border rounded-lg hover:bg-surface-alt disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
            >
              Discard
            </button>
            <button
              type="button"
              (click)="save.emit()"
              [disabled]="pendingChanges().length === 0"
              class="px-5 py-2.5 text-sm font-medium text-white bg-primary-600 rounded-lg hover:bg-primary-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
            >
              Save Configuration
            </button>
          </div>
        </div>
      </div>
    </div>
  `,
})
export class FooterActionBarComponent {
  readonly pendingChanges = input<PendingChange[]>([]);
  readonly lastAutoSaved = input<string>('--:--:--');

  readonly save = output<void>();
  readonly discard = output<void>();
}
