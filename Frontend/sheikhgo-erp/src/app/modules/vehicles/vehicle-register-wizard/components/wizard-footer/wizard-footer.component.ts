import { ChangeDetectionStrategy, Component, OnDestroy, computed, input, output, signal } from '@angular/core';
import { UiButtonComponent } from '../../../../../shared/components/ui/button/ui-button.component';

@Component({
  selector: 'app-wizard-footer',
  standalone: true,
  imports: [UiButtonComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="wizard-footer-wrap">
      <div class="wizard-footer-spacer" aria-hidden="true"></div>
      <footer class="wizard-footer-bar">
        <div class="wizard-footer-inner">
          <div class="flex items-center gap-3 min-w-0">
            <ui-button variant="ghost" (clicked)="cancel.emit()">Cancel</ui-button>
            @if (saveStatusLabel()) {
              <span class="text-sm text-fleet-text-muted truncate">{{ saveStatusLabel() }}</span>
            } @else if (lastSavedLabel()) {
              <span class="text-sm text-fleet-text-muted truncate">{{ lastSavedLabel() }}</span>
            }
          </div>

          <div class="flex flex-wrap items-center gap-3 wizard-footer-actions">
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
        </div>
      </footer>
    </div>
  `,
  styles: [`
    .wizard-footer-wrap {
      margin-top: 1.5rem;
    }

    .wizard-footer-bar {
      border-top: 1px solid var(--fleet-border, #e2e8f0);
      background: #fff;
      box-shadow: 0 -2px 10px rgba(0, 0, 0, 0.06);
    }

    .wizard-footer-inner {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
      padding-top: 1rem;
      padding-bottom: 1rem;
    }

    .wizard-footer-actions {
      justify-content: flex-end;
    }

    @media (max-width: 639px) {
      .wizard-footer-spacer {
        height: calc(10.5rem + env(safe-area-inset-bottom, 0px));
      }

      .wizard-footer-bar {
        position: fixed;
        left: 0;
        right: 0;
        bottom: 0;
        z-index: 30;
        padding-left: max(1rem, env(safe-area-inset-left, 0px));
        padding-right: max(1rem, env(safe-area-inset-right, 0px));
        padding-bottom: env(safe-area-inset-bottom, 0px);
      }

      .wizard-footer-inner {
        max-width: 80rem;
        margin: 0 auto;
        padding-bottom: calc(1rem + env(safe-area-inset-bottom, 0px));
      }
    }

    @media (min-width: 640px) {
      .wizard-footer-spacer {
        display: none;
      }

      .wizard-footer-wrap {
        margin-top: 2rem;
      }

      .wizard-footer-bar {
        position: relative;
        box-shadow: none;
      }

      .wizard-footer-inner {
        flex-direction: row;
        align-items: center;
        justify-content: space-between;
        padding-bottom: 0;
      }
    }
  `]
})
export class WizardFooterComponent implements OnDestroy {
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

  private readonly _now = signal(Date.now());
  private readonly _ticker = setInterval(() => this._now.set(Date.now()), 10_000);

  ngOnDestroy(): void {
    clearInterval(this._ticker);
  }

  readonly showBack = computed(() => this.currentStep() !== this.firstStepId());

  readonly lastSavedLabel = computed(() => {
    const formatted = this.lastSavedFormatted();
    if (formatted) return formatted;
    const at = this.lastSavedAt();
    if (!at || isNaN(at.getTime())) return null;

    const sec = Math.floor((this._now() - at.getTime()) / 1000);
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
