import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  inject,
  OnDestroy,
  OnInit
} from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { NgClass } from '@angular/common';

export type UiToastVariant = 'success' | 'error' | 'warning' | 'info';

@Component({
  selector: 'ui-toast',
  standalone: true,
  imports: [MatIconModule, NgClass],
  templateUrl: './ui-toast.component.html',
  styleUrls: ['./ui-toast.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class UiToastComponent implements OnInit, OnDestroy {
  private readonly cdr = inject(ChangeDetectorRef);

  variant: UiToastVariant = 'info';
  title?: string;
  message: string = '';
  duration: number = 3500;
  onClose: () => void = () => {};

  visible = false;
  leaving = false;
  progress = 100;

  private progressInterval?: ReturnType<typeof setInterval>;
  private dismissTimeout?: ReturnType<typeof setTimeout>;

  readonly ICONS: Record<UiToastVariant, string> = {
    success: 'check_circle',
    error: 'error',
    warning: 'warning',
    info: 'info'
  };

  readonly DEFAULT_TITLES: Record<UiToastVariant, string> = {
    success: 'Success',
    error: 'Error',
    warning: 'Warning',
    info: 'Info'
  };

  get displayTitle(): string {
    return this.title ?? this.DEFAULT_TITLES[this.variant];
  }

  ngOnInit(): void {
    // Trigger enter animation on next frame
    requestAnimationFrame(() => {
      this.visible = true;
      this.cdr.markForCheck();
    });

    const tickMs = 30;
    const steps = this.duration / tickMs;
    const decrement = 100 / steps;

    this.progressInterval = setInterval(() => {
      this.progress = Math.max(0, this.progress - decrement);
      this.cdr.markForCheck();
    }, tickMs);

    this.dismissTimeout = setTimeout(() => this.dismiss(), this.duration);
  }

  ngOnDestroy(): void {
    clearInterval(this.progressInterval);
    clearTimeout(this.dismissTimeout);
  }

  dismiss(): void {
    if (this.leaving) return;
    clearInterval(this.progressInterval);
    clearTimeout(this.dismissTimeout);
    this.leaving = true;
    this.cdr.markForCheck();
    // Wait for exit animation, then call close callback
    setTimeout(() => this.onClose(), 280);
  }
}
