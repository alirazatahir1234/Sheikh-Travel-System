import {
  Component,
  ChangeDetectionStrategy,
  input,
  output,
  signal,
  computed,
  ElementRef,
  inject,
  OnInit,
  OnDestroy,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { OrganizationNode } from '../../models/organization.models';

export interface ContextMenuItem {
  id: string;
  label: string;
  icon?: string;
  disabled?: boolean;
  divider?: boolean;
}

@Component({
  selector: 'app-context-menu',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (visible()) {
      <div
        class="fixed z-[100] bg-white border border-border rounded-lg shadow-lg py-1 min-w-[180px]"
        [style.left.px]="position().x"
        [style.top.px]="position().y"
      >
        @for (item of menuItems(); track item.id) {
          @if (item.divider) {
            <div class="my-1 border-t border-border"></div>
          } @else {
            <button
              type="button"
              (click)="onItemClick(item)"
              [disabled]="item.disabled"
              class="w-full flex items-center gap-2 px-3 py-2 text-sm text-left hover:bg-surface-alt transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
            >
              @switch (item.icon) {
                @case ('edit') {
                  <svg class="w-4 h-4 text-text-muted" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
                  </svg>
                }
                @case ('add') {
                  <svg class="w-4 h-4 text-text-muted" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
                  </svg>
                }
                @case ('user') {
                  <svg class="w-4 h-4 text-text-muted" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
                  </svg>
                }
                @case ('move') {
                  <svg class="w-4 h-4 text-text-muted" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 7h12m0 0l-4-4m4 4l-4 4m0 6H4m0 0l4 4m-4-4l4-4" />
                  </svg>
                }
                @case ('archive') {
                  <svg class="w-4 h-4 text-text-muted" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 8h14M5 8a2 2 0 110-4h14a2 2 0 110 4M5 8v10a2 2 0 002 2h10a2 2 0 002-2V8m-9 4h4" />
                  </svg>
                }
                @case ('delete') {
                  <svg class="w-4 h-4 text-red-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                  </svg>
                }
                @case ('rename') {
                  <svg class="w-4 h-4 text-text-muted" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15.232 5.232l3.536 3.536m-2.036-5.036a2.5 2.5 0 113.536 3.536L6.5 21.036H3v-3.572L16.732 3.732z" />
                  </svg>
                }
                @case ('users') {
                  <svg class="w-4 h-4 text-text-muted" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4.354a4 4 0 110 5.292M15 21H3v-1a6 6 0 0112 0v1zm0 0h6v-1a6 6 0 00-9-5.197M13 7a4 4 0 11-8 0 4 4 0 018 0z" />
                  </svg>
                }
              }
              <span [class.text-red-600]="item.id === 'delete'">{{ item.label }}</span>
            </button>
          }
        }
      </div>
    }
  `,
  host: {
    '(document:click)': 'onDocumentClick($event)',
    '(document:contextmenu)': 'onDocumentContextMenu($event)',
  },
})
export class ContextMenuComponent implements OnDestroy {
  readonly visible = signal(false);
  readonly position = signal({ x: 0, y: 0 });
  readonly node = signal<OrganizationNode | null>(null);

  readonly itemSelected = output<{ action: string; node: OrganizationNode }>();

  readonly menuItems = computed<ContextMenuItem[]>(() => {
    const n = this.node();
    if (!n) return [];

    if (n.type === 'branch') {
      return [
        { id: 'edit', label: 'Edit Branch', icon: 'edit' },
        { id: 'add-department', label: 'Add Department', icon: 'add' },
        { id: 'assign-manager', label: 'Assign Manager', icon: 'user' },
        { id: 'divider1', label: '', divider: true },
        { id: 'move', label: 'Move', icon: 'move' },
        { id: 'archive', label: 'Archive', icon: 'archive' },
        { id: 'divider2', label: '', divider: true },
        { id: 'delete', label: 'Delete', icon: 'delete' },
      ];
    }

    if (n.type === 'department') {
      return [
        { id: 'rename', label: 'Rename', icon: 'rename' },
        { id: 'assign-users', label: 'Assign Users', icon: 'users' },
        { id: 'move', label: 'Move to Branch', icon: 'move' },
        { id: 'divider1', label: '', divider: true },
        { id: 'delete', label: 'Delete', icon: 'delete' },
      ];
    }

    return [];
  });

  show(node: OrganizationNode, x: number, y: number): void {
    this.node.set(node);
    this.position.set({
      x: Math.min(x, window.innerWidth - 200),
      y: Math.min(y, window.innerHeight - 300),
    });
    this.visible.set(true);
  }

  hide(): void {
    this.visible.set(false);
    this.node.set(null);
  }

  onItemClick(item: ContextMenuItem): void {
    if (item.disabled || item.divider) return;

    const n = this.node();
    if (n) {
      this.itemSelected.emit({ action: item.id, node: n });
    }
    this.hide();
  }

  onDocumentClick(event: MouseEvent): void {
    this.hide();
  }

  onDocumentContextMenu(event: MouseEvent): void {
    if (this.visible()) {
      this.hide();
    }
  }

  ngOnDestroy(): void {
    this.hide();
  }
}
