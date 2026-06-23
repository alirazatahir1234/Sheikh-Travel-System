import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

export type ScheduleView = 'list' | 'calendar' | 'timeline';

@Component({
  selector: 'schedule-view-toggle',
  standalone: true,
  imports: [MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="toggle" role="tablist">
      @for (opt of options; track opt.id) {
        <button
          type="button"
          role="tab"
          class="toggle__btn"
          [class.toggle__btn--active]="view() === opt.id"
          [attr.aria-selected]="view() === opt.id"
          (click)="viewChange.emit(opt.id)">
          <mat-icon>{{ opt.icon }}</mat-icon>
          <span>{{ opt.label }}</span>
        </button>
      }
    </div>
  `,
  styles: [`
    .toggle {
      display: inline-flex;
      gap: 0.25rem;
      padding: 0.25rem;
      background: #f1f5f9;
      border-radius: 10px;
    }
    .toggle__btn {
      display: inline-flex;
      align-items: center;
      gap: 0.35rem;
      padding: 0.45rem 0.75rem;
      border: none;
      border-radius: 8px;
      background: transparent;
      color: #64748b;
      font-size: 0.8125rem;
      font-weight: 600;
      cursor: pointer;
    }
    .toggle__btn mat-icon { font-size: 1.125rem; width: 1.125rem; height: 1.125rem; }
    .toggle__btn--active {
      background: #fff;
      color: #0B6B50;
      box-shadow: 0 1px 2px rgba(15, 23, 42, 0.08);
    }
  `]
})
export class ScheduleViewToggleComponent {
  readonly view = input.required<ScheduleView>();
  readonly viewChange = output<ScheduleView>();

  readonly options: { id: ScheduleView; label: string; icon: string }[] = [
    { id: 'list', label: 'List', icon: 'view_list' },
    { id: 'calendar', label: 'Calendar', icon: 'calendar_month' },
    { id: 'timeline', label: 'Timeline', icon: 'timeline' }
  ];
}
