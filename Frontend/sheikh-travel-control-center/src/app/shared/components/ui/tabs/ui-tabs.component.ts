import { ChangeDetectionStrategy, Component, input, model } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { UiTab } from '../types/ui.types';

@Component({
  selector: 'ui-tabs',
  standalone: true,
  imports: [MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="border-b border-fleet-border">
      <nav class="-mb-px flex gap-1 overflow-x-auto" role="tablist">
        @for (tab of tabs(); track tab.id) {
          <button
            type="button"
            role="tab"
            [disabled]="tab.disabled"
            [attr.aria-selected]="tab.id === activeId()"
            class="relative flex items-center gap-2 whitespace-nowrap border-b-2 px-4 py-2.5 text-sm font-semibold transition-colors disabled:cursor-not-allowed disabled:opacity-50"
            [class.border-fleet-primary]="tab.id === activeId()"
            [class.text-fleet-primary]="tab.id === activeId()"
            [class.border-transparent]="tab.id !== activeId()"
            [class.text-fleet-text-muted]="tab.id !== activeId()"
            [class.hover:text-fleet-text]="tab.id !== activeId()"
            (click)="select(tab)">
            @if (tab.icon) { <mat-icon class="!text-[18px]">{{ tab.icon }}</mat-icon> }
            {{ tab.label }}
          </button>
        }
      </nav>
    </div>
  `,
  styles: [`
    :host { display: block; }
    mat-icon { display: inline-flex; align-items: center; justify-content: center; }
  `]
})
export class UiTabsComponent {
  readonly tabs = input<UiTab[]>([]);
  readonly activeId = model<string>('');

  select(tab: UiTab): void {
    if (tab.disabled) {
      return;
    }
    this.activeId.set(tab.id);
  }
}
