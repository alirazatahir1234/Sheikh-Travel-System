import { ChangeDetectionStrategy, Component, computed, output, signal } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { UiModalComponent } from '../modal/ui-modal.component';
import { UiButtonComponent } from '../button/ui-button.component';
import { UiButtonVariant, UiConfirmConfig, UiConfirmVariant } from '../types/ui.types';

interface VariantStyle {
  icon: string;
  iconClass: string;
  confirmVariant: UiButtonVariant;
}

const VARIANT_STYLES: Record<UiConfirmVariant, VariantStyle> = {
  delete: { icon: 'delete', iconClass: 'text-fleet-error bg-fleet-error-soft', confirmVariant: 'danger' },
  warning: { icon: 'warning', iconClass: 'text-amber-600 bg-amber-50', confirmVariant: 'danger' },
  info: { icon: 'info', iconClass: 'text-blue-600 bg-blue-50', confirmVariant: 'primary' }
};

@Component({
  selector: 'ui-confirm-dialog',
  standalone: true,
  imports: [MatIconModule, UiModalComponent, UiButtonComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ui-modal [open]="open()" (openChange)="open.set($event)" size="sm" [showClose]="false" (closed)="resolve(false)">
      <div class="flex gap-4">
        <span class="flex h-11 w-11 shrink-0 items-center justify-center rounded-full" [class]="style().iconClass">
          <mat-icon>{{ style().icon }}</mat-icon>
        </span>
        <div class="min-w-0">
          <h3 class="text-base font-bold text-fleet-text">{{ config().title }}</h3>
          <p class="mt-1 text-sm text-fleet-text-muted">{{ config().message }}</p>
        </div>
      </div>

      <div modal-footer class="flex justify-end gap-3">
        <ui-button variant="ghost" (clicked)="resolve(false)">{{ config().cancelText || 'Cancel' }}</ui-button>
        <ui-button [variant]="style().confirmVariant" (clicked)="resolve(true)">
          {{ config().confirmText || 'Confirm' }}
        </ui-button>
      </div>
    </ui-modal>
  `,
  styles: [`
    mat-icon { display: inline-flex; align-items: center; justify-content: center; }
  `]
})
export class UiConfirmDialogComponent {
  readonly open = signal(false);
  readonly config = signal<UiConfirmConfig>({ title: '', message: '' });

  readonly result = output<boolean>();

  protected readonly style = computed(() => VARIANT_STYLES[this.config().variant ?? 'info']);

  show(config: UiConfirmConfig): void {
    this.config.set(config);
    this.open.set(true);
  }

  resolve(confirmed: boolean): void {
    this.open.set(false);
    this.result.emit(confirmed);
  }
}
