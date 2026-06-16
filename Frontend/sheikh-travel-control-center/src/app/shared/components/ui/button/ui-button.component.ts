import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { NgClass } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { UiButtonSize, UiButtonVariant } from '../types/ui.types';

const VARIANT_CLASSES: Record<UiButtonVariant, string> = {
  primary:
    'bg-fleet-primary text-white border border-transparent hover:brightness-110 active:brightness-95',
  secondary:
    'bg-fleet-secondary text-white border border-transparent hover:brightness-110 active:brightness-95',
  success:
    'bg-emerald-600 text-white border border-transparent hover:brightness-110 active:brightness-95',
  danger:
    'bg-fleet-error text-white border border-transparent hover:brightness-110 active:brightness-95',
  outline:
    'bg-transparent text-fleet-primary border border-fleet-primary/40 hover:bg-fleet-primary-soft',
  ghost:
    'bg-transparent text-fleet-muted border border-transparent hover:bg-fleet-surface-muted',
  neutral:
    'bg-fleet-surface-muted text-fleet-text border border-transparent hover:bg-fleet-border'
};

const SIZE_CLASSES: Record<UiButtonSize, string> = {
  sm: 'h-8 px-3 text-[13px] gap-1.5',
  md: 'h-10 px-4 text-sm gap-2',
  lg: 'h-12 px-6 text-base gap-2.5'
};

const ICON_SIZE: Record<UiButtonSize, string> = {
  sm: 'text-[16px]',
  md: 'text-[18px]',
  lg: 'text-[20px]'
};

@Component({
  selector: 'ui-button',
  standalone: true,
  imports: [NgClass, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button
      [type]="type()"
      [disabled]="disabled() || loading()"
      [ngClass]="buttonClasses()"
      (click)="handleClick($event)">
      @if (loading()) {
        <span class="ui-btn-spinner" [ngClass]="iconSize()" aria-hidden="true"></span>
      } @else if (icon() && iconPosition() === 'left') {
        <mat-icon [ngClass]="iconSize()">{{ icon() }}</mat-icon>
      }
      <span class="inline-flex items-center"><ng-content></ng-content></span>
      @if (!loading() && icon() && iconPosition() === 'right') {
        <mat-icon [ngClass]="iconSize()">{{ icon() }}</mat-icon>
      }
    </button>
  `,
  styles: [`
    :host { display: inline-flex; }
    button { width: 100%; }
    .ui-btn-spinner {
      display: inline-block;
      width: 1em;
      height: 1em;
      border: 2px solid currentColor;
      border-right-color: transparent;
      border-radius: 50%;
      animation: ui-btn-spin 0.6s linear infinite;
    }
    @keyframes ui-btn-spin { to { transform: rotate(360deg); } }
    mat-icon {
      width: auto;
      height: auto;
      display: inline-flex;
      align-items: center;
    }
  `]
})
export class UiButtonComponent {
  readonly variant = input<UiButtonVariant>('primary');
  readonly size = input<UiButtonSize>('md');
  readonly disabled = input(false);
  readonly loading = input(false);
  readonly icon = input<string>();
  readonly iconPosition = input<'left' | 'right'>('left');
  readonly type = input<'button' | 'submit' | 'reset'>('button');
  readonly fullWidth = input(false);

  readonly clicked = output<MouseEvent>();

  readonly iconSize = computed(() => ICON_SIZE[this.size()]);

  readonly buttonClasses = computed(() => [
    'inline-flex items-center justify-center font-semibold rounded-sm transition-all',
    'focus:outline-none focus-visible:ring-2 focus-visible:ring-fleet-primary/30',
    'disabled:opacity-50 disabled:cursor-not-allowed select-none',
    SIZE_CLASSES[this.size()],
    VARIANT_CLASSES[this.variant()],
    this.fullWidth() ? 'w-full' : ''
  ].join(' '));

  handleClick(event: MouseEvent): void {
    if (this.disabled() || this.loading()) {
      return;
    }
    this.clicked.emit(event);
  }
}
