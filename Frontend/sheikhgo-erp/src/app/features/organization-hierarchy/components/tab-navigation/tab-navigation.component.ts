import {
  Component,
  ChangeDetectionStrategy,
  input,
  output,
  computed,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { TabItem } from '../../models/organization.models';

@Component({
  selector: 'app-tab-navigation',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="px-6 border-t border-border">
      <nav class="flex gap-1 -mb-px">
        @for (tab of tabs(); track tab.id) {
          <button
            type="button"
            (click)="tabChange.emit(tab.id)"
            [class]="getTabClasses(tab.id)"
          >
            {{ tab.label }}
            @if (tab.count !== undefined) {
              <span [class]="getCountClasses(tab.id)">
                ({{ tab.count }})
              </span>
            }
          </button>
        }
      </nav>
    </div>
  `,
})
export class TabNavigationComponent {
  readonly tabs = input.required<TabItem[]>();
  readonly activeTab = input<string>('overview');

  readonly tabChange = output<string>();

  getTabClasses(tabId: string): string {
    const base = 'px-4 py-3 text-sm font-medium border-b-2 transition-colors';
    const isActive = this.activeTab() === tabId;
    
    if (isActive) {
      return `${base} text-primary-600 border-primary-600`;
    }
    return `${base} text-text-muted border-transparent hover:text-text hover:border-border`;
  }

  getCountClasses(tabId: string): string {
    const isActive = this.activeTab() === tabId;
    return isActive ? 'text-primary-500' : 'text-text-soft';
  }
}
