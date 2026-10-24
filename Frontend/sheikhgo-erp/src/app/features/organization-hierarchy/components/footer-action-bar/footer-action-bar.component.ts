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
    <div class="fixed bottom-0 left-0 right-0 bg-white border-t border-border shadow-[0_-4px_12px_rgba(15,23,42,0.06)] z-50">
      <div class="max-w-screen-2xl mx-auto px-4 py-3 md:px-6">
        <div class="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <!-- Left: Pending Changes Info -->
          <div class="flex items-center gap-3 min-w-0">
            @if (pendingChanges().length > 0) {
              <div class="flex items-center gap-2 text-primary-600">
                <span class="inline-flex w-2 h-2 rounded-full bg-primary-600 flex-shrink-0" aria-hidden="true"></span>
                <span class="text-sm font-medium">
                  {{ pendingChanges().length }} change{{ pendingChanges().length === 1 ? '' : 's' }} pending
                </span>
              </div>
              @if (lastAutoSaved() && lastAutoSaved() !== '--:--:--') {
                <div class="hidden sm:block h-4 w-px bg-border"></div>
                <span class="hidden sm:inline text-sm text-text-muted truncate">
                  Last saved {{ lastAutoSaved() }}
                </span>
              }
            } @else {
              <span class="text-sm text-text-muted">
                @if (lastAutoSaved() && lastAutoSaved() !== '--:--:--') {
                  All changes saved · {{ lastAutoSaved() }}
                } @else {
                  No unsaved hierarchy changes
                }
              </span>
            }
          </div>

          <!-- Right: Action Buttons -->
          <div class="flex items-center gap-2 sm:gap-3">
            <button
              type="button"
              (click)="discard.emit()"
              [disabled]="pendingChanges().length === 0"
              class="flex-1 sm:flex-none px-4 py-2.5 text-sm font-medium text-text border border-border rounded-lg hover:bg-surface-alt disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
            >
              Discard
            </button>
            <button
              type="button"
              (click)="save.emit()"
              [disabled]="pendingChanges().length === 0"
              class="flex-1 sm:flex-none px-4 py-2.5 text-sm font-medium text-white bg-primary-600 rounded-lg hover:bg-primary-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
            >
              Save configuration
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
