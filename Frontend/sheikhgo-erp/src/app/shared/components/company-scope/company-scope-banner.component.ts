import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

/** "Working in: <company>" strip shown to a Super Admin acting on another company. */
@Component({
  selector: 'app-company-scope-banner',
  standalone: true,
  imports: [MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="scope-banner" role="status">
      <mat-icon aria-hidden="true">apartment</mat-icon>
      <span class="scope-banner__text">{{ prefix }} <strong>{{ companyName }}</strong></span>
      @if (actionLabel) {
        <button type="button" class="scope-banner__action" (click)="action.emit()">{{ actionLabel }}</button>
      }
    </div>
  `,
  styles: [`
    .scope-banner {
      display: flex; align-items: center; flex-wrap: wrap; gap: 6px 10px;
      margin: 12px 0; padding: 10px 14px; border-radius: 10px;
      background: color-mix(in srgb, var(--stb-cat-blue, #2563eb) 10%, transparent);
      border: 1px solid color-mix(in srgb, var(--stb-cat-blue, #2563eb) 30%, transparent);
      color: var(--stb-text, inherit); font-size: 14px;
    }
    .scope-banner mat-icon { font-size: 20px; width: 20px; height: 20px; color: var(--stb-cat-blue, #2563eb); }
    .scope-banner__text { flex: 1 1 auto; min-width: 0; }
    .scope-banner__action {
      border: 0; background: transparent; padding: 4px 6px; border-radius: 6px; cursor: pointer;
      color: var(--stb-cat-blue, #2563eb); font-weight: 600; font-size: 13px;
    }
    .scope-banner__action:hover { text-decoration: underline; }
  `]
})
export class CompanyScopeBannerComponent {
  @Input({ required: true }) companyName = '';
  @Input() prefix = 'Working in';
  @Input() actionLabel: string | null = null;
  @Output() readonly action = new EventEmitter<void>();
}
