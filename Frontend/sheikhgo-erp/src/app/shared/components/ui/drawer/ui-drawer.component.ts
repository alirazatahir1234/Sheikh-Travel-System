import { animate, style, transition, trigger } from '@angular/animations';
import {
  ChangeDetectionStrategy,
  Component,
  HostListener,
  input,
  model,
  output
} from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { UiDrawerPosition } from '../types/ui.types';

@Component({
  selector: 'ui-drawer',
  standalone: true,
  imports: [MatIconModule, NgTemplateOutlet],
  changeDetection: ChangeDetectionStrategy.OnPush,
  animations: [
    trigger('backdrop', [
      transition(':enter', [style({ opacity: 0 }), animate('150ms ease-out', style({ opacity: 1 }))]),
      transition(':leave', [animate('120ms ease-in', style({ opacity: 0 }))])
    ]),
    trigger('panelLeft', [
      transition(':enter', [
        style({ transform: 'translateX(-100%)' }),
        animate('220ms cubic-bezier(0.16, 1, 0.3, 1)', style({ transform: 'translateX(0)' }))
      ]),
      transition(':leave', [animate('180ms ease-in', style({ transform: 'translateX(-100%)' }))])
    ]),
    trigger('panelRight', [
      transition(':enter', [
        style({ transform: 'translateX(100%)' }),
        animate('220ms cubic-bezier(0.16, 1, 0.3, 1)', style({ transform: 'translateX(0)' }))
      ]),
      transition(':leave', [animate('180ms ease-in', style({ transform: 'translateX(100%)' }))])
    ])
  ],
  template: `
    @if (open()) {
      <div class="fixed inset-0 z-[1000]">
        <div class="absolute inset-0 bg-slate-900/40 backdrop-blur-sm" @backdrop (click)="onOverlay()"></div>

        @if (position() === 'left') {
          <aside
            class="ui-drawer-panel absolute left-0 top-0 flex h-full max-h-[100dvh] flex-col bg-white shadow-2xl"
            [style.width]="width()"
            @panelLeft>
            <ng-container [ngTemplateOutlet]="content"></ng-container>
          </aside>
        } @else {
          <aside
            class="ui-drawer-panel absolute right-0 top-0 flex h-full max-h-[100dvh] flex-col bg-white shadow-2xl"
            [style.width]="width()"
            @panelRight>
            <ng-container [ngTemplateOutlet]="content"></ng-container>
          </aside>
        }
      </div>
    }

    <ng-template #content>
      @if (title() || showClose()) {
        <header class="flex items-center justify-between gap-4 border-b border-fleet-border px-5 py-4">
          <h2 class="text-base font-bold text-fleet-text">{{ title() }}</h2>
          @if (showClose()) {
            <button
              type="button"
              class="rounded-full p-1 text-fleet-text-muted transition-colors hover:bg-fleet-surface-muted"
              aria-label="Close"
              (click)="close()">
              <mat-icon>close</mat-icon>
            </button>
          }
        </header>
      }
      <div class="flex-1 overflow-y-auto" [class.p-5]="padded()">
        <ng-content></ng-content>
      </div>
    </ng-template>
  `,
  styles: [`
    mat-icon { display: inline-flex; align-items: center; justify-content: center; }
    @media (max-width: 767px) {
      .ui-drawer-panel {
        width: 100% !important;
        max-width: 100vw;
      }
      header button {
        min-width: 44px;
        min-height: 44px;
      }
    }
  `]
})
export class UiDrawerComponent {
  readonly open = model(false);
  readonly position = input<UiDrawerPosition>('right');
  readonly width = input('420px');
  readonly title = input<string>();
  readonly showClose = input(true);
  readonly closeOnOverlay = input(true);
  readonly padded = input(true);

  readonly closed = output<void>();

  close(): void {
    this.open.set(false);
    this.closed.emit();
  }

  onOverlay(): void {
    if (this.closeOnOverlay()) {
      this.close();
    }
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.open()) {
      this.close();
    }
  }
}
