import {
  animate,
  style,
  transition,
  trigger
} from '@angular/animations';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  HostListener,
  input,
  model,
  output
} from '@angular/core';
import { NgClass } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { UiModalSize } from '../types/ui.types';

const SIZE_CLASSES: Record<UiModalSize, string> = {
  sm: 'max-w-md',
  md: 'max-w-xl',
  lg: 'max-w-3xl'
};

@Component({
  selector: 'ui-modal',
  standalone: true,
  imports: [NgClass, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  animations: [
    trigger('backdrop', [
      transition(':enter', [style({ opacity: 0 }), animate('150ms ease-out', style({ opacity: 1 }))]),
      transition(':leave', [animate('120ms ease-in', style({ opacity: 0 }))])
    ]),
    trigger('panel', [
      transition(':enter', [
        style({ opacity: 0, transform: 'translateY(12px) scale(0.98)' }),
        animate('180ms cubic-bezier(0.16, 1, 0.3, 1)', style({ opacity: 1, transform: 'translateY(0) scale(1)' }))
      ]),
      transition(':leave', [
        animate('120ms ease-in', style({ opacity: 0, transform: 'translateY(8px) scale(0.98)' }))
      ])
    ])
  ],
  template: `
    @if (open()) {
      <div
        class="fixed inset-0 z-[1000] flex items-center justify-center p-4"
        @backdrop>
        <div
          class="absolute inset-0 bg-slate-900/40 backdrop-blur-sm"
          (click)="onBackdrop()"></div>

        <div
          class="ui-modal-panel relative z-10 flex max-h-[90vh] w-full flex-col overflow-hidden rounded-xl bg-white shadow-2xl"
          [ngClass]="sizeClass()"
          @panel>
          @if (title() || showClose()) {
            <div class="flex items-center justify-between gap-4 border-b border-fleet-border px-6 py-4">
              <h2 class="text-lg font-bold text-fleet-text">{{ title() }}</h2>
              @if (showClose()) {
                <button
                  type="button"
                  class="rounded-full p-1 text-fleet-text-muted transition-colors hover:bg-fleet-surface-muted"
                  aria-label="Close"
                  (click)="close()">
                  <mat-icon>close</mat-icon>
                </button>
              }
            </div>
          }

          <div class="flex-1 overflow-y-auto px-6 py-5">
            <ng-content></ng-content>
          </div>

          <div class="ui-modal-footer border-t border-fleet-border px-6 py-4">
            <ng-content select="[modal-footer]"></ng-content>
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
    mat-icon { display: inline-flex; align-items: center; justify-content: center; }
    @media (max-width: 767px) {
      .ui-modal-panel {
        width: 95vw !important;
        max-width: 95vw !important;
        max-height: calc(100dvh - 1.5rem);
        border-radius: 12px;
      }
      .ui-modal-footer {
        display: flex;
        flex-direction: column;
        gap: 0.5rem;
      }
      .ui-modal-footer ::ng-deep button,
      .ui-modal-footer ::ng-deep ui-button {
        width: 100%;
      }
    }
    @media (min-width: 768px) and (max-width: 1023px) {
      .ui-modal-panel {
        width: 90vw !important;
        max-width: 90vw !important;
      }
    }
  `]
})
export class UiModalComponent {
  readonly open = model(false);
  readonly title = input<string>();
  readonly size = input<UiModalSize>('md');
  readonly showClose = input(true);
  readonly closeOnBackdrop = input(true);
  readonly closeOnEscape = input(true);

  readonly closed = output<void>();

  protected readonly sizeClass = computed(() => SIZE_CLASSES[this.size()]);

  close(): void {
    this.open.set(false);
    this.closed.emit();
  }

  onBackdrop(): void {
    if (this.closeOnBackdrop()) {
      this.close();
    }
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.open() && this.closeOnEscape()) {
      this.close();
    }
  }
}
