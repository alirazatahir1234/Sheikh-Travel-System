import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { UiButtonComponent } from '../../../../../shared/components/ui/button/ui-button.component';

@Component({
  selector: 'app-wizard-footer',
  standalone: true,
  imports: [UiButtonComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <footer class="mt-8 flex flex-col gap-4 border-t border-fleet-border pt-5 sm:flex-row sm:items-center sm:justify-between">
      <div class="flex items-center gap-3">
        <ui-button variant="ghost" (clicked)="cancel.emit()">Cancel</ui-button>
        @if (saveStatusLabel()) {
          <span class="text-sm text-fleet-text-muted">{{ saveStatusLabel() }}</span>
        } @else if (lastSavedLabel()) {
          <span class="text-sm text-fleet-text-muted">{{ lastSavedLabel() }}</span>
        }
      </div>

      <div class="flex flex-wrap items-center gap-3">
        @if (showBack()) {
          <ui-button variant="outline" (clicked)="back.emit()">Back</ui-button>
        }
        <ui-button variant="outline" [loading]="draftSaving()" (clicked)="saveDraft.emit()">
          {{ saveDraftLabel() }}
        </ui-button>
        <ui-button
          variant="primary"
          [loading]="primaryLoading()"
          [icon]="currentStep() === finalStepId() ? 'check_circle' : 'arrow_forward'"
          iconPosition="right"
          (clicked)="primaryAction.emit()">
          {{ primaryLabel() }}
        </ui-button>
      </div>
    </footer>
  `
})
export class WizardFooterComponent {
  readonly currentStep = input.required<string>();
  readonly firstStepId = input('details');
  readonly finalStepId = input('review');
  readonly draftSaving = input(false);
  readonly primaryLoading = input(false);
  readonly lastSavedAt = input<Date | null>(null);
  readonly lastSavedFormatted = input<string | null>(null);
  /** Preferred label from parent (absolute or session-relative). */
  readonly saveStatusLabel = input<string | null>(null);
  readonly primaryLabel = input('Continue');
  readonly saveDraftLabel = input('Save Draft');
  readonly cancel = output<void>();
  readonly back = output<void>();
  readonly saveDraft = output<void>();
  readonly primaryAction = output<void>();

  readonly showBack = computed(() => this.currentStep() !== this.firstStepId());

  readonly lastSavedLabel = computed(() => {
    const formatted = this.lastSavedFormatted();
    if (formatted) return formatted;
    const at = this.lastSavedAt();
    if (!at || isNaN(at.getTime())) return null;

    const sec = Math.floor((Date.now() - at.getTime()) / 1000);
    if (sec < 0) return null;
    if (sec < 10) return 'Saved just now';
    if (sec < 60) return `Saved ${sec}s ago`;
    const min = Math.floor(sec / 60);
    if (min < 60) return `Saved ${min} minute${min === 1 ? '' : 's'} ago`;
    const hr = Math.floor(min / 60);
    if (hr < 24) return `Saved ${hr} hour${hr === 1 ? '' : 's'} ago`;
    const days = Math.floor(hr / 24);
    if (days < 7) return `Saved ${days} day${days === 1 ? '' : 's'} ago`;
    return null;
  });
}
