import { NgTemplateOutlet } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  contentChildren,
  effect,
  input,
  signal
} from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { UiEmptyStateComponent } from '../empty-state/ui-empty-state.component';
import { UiTableCellDirective } from './ui-table-cell.directive';
import { UiTableColumn, UiTableSort } from '../types/ui.types';

type TableRow = Record<string, unknown>;

@Component({
  selector: 'ui-data-table',
  standalone: true,
  imports: [NgTemplateOutlet, MatIconModule, UiEmptyStateComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="overflow-hidden rounded-xl border border-fleet-border bg-white">
      @if (searchable() || title()) {
        <div class="flex flex-col gap-3 border-b border-fleet-border px-5 py-4 sm:flex-row sm:items-center sm:justify-between">
          <h3 class="text-base font-bold text-fleet-text">{{ title() }}</h3>
          @if (searchable()) {
            <div class="relative sm:w-72">
              <mat-icon class="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 !text-[20px] text-fleet-text-muted">search</mat-icon>
              <input
                type="search"
                [placeholder]="searchPlaceholder()"
                class="w-full rounded-full border border-fleet-border bg-fleet-surface py-2 pl-10 pr-4 text-sm focus:border-fleet-primary focus:outline-none"
                [value]="query()"
                (input)="onSearch($event)" />
            </div>
          }
        </div>
      }

      <div class="stb-table-scroll overflow-x-auto">
        <table class="stb-data-table w-full min-w-[640px] text-left">
          <thead class="sticky top-0 z-10 bg-fleet-surface-alt/60">
            <tr>
              @for (col of columns(); track col.key) {
                <th
                  class="px-5 py-3 text-[11px] font-bold uppercase tracking-wider text-fleet-text-muted"
                  [style.width]="col.width"
                  [class.cursor-pointer]="col.sortable"
                  [class.select-none]="col.sortable"
                  [class.text-center]="col.align === 'center'"
                  [class.text-right]="col.align === 'right'"
                  (click)="col.sortable ? toggleSort(col.key) : null">
                  <span class="inline-flex items-center gap-1">
                    {{ col.label }}
                    @if (col.sortable && sort()?.key === col.key) {
                      <mat-icon class="!text-[16px]">{{ sort()?.direction === 'asc' ? 'arrow_upward' : 'arrow_downward' }}</mat-icon>
                    }
                  </span>
                </th>
              }
              @if (actionsTemplate()) {
                <th class="px-5 py-3 text-right text-[11px] font-bold uppercase tracking-wider text-fleet-text-muted">
                  {{ actionsLabel() }}
                </th>
              }
            </tr>
          </thead>

          <tbody class="divide-y divide-fleet-border">
            @if (loading()) {
              @for (skeleton of skeletonRows(); track $index) {
                <tr>
                  @for (col of columns(); track col.key) {
                    <td class="px-5 py-4"><div class="h-3.5 w-3/4 animate-pulse rounded bg-fleet-surface-muted"></div></td>
                  }
                  @if (actionsTemplate()) {
                    <td class="px-5 py-4"><div class="ml-auto h-3.5 w-12 animate-pulse rounded bg-fleet-surface-muted"></div></td>
                  }
                </tr>
              }
            } @else if (!pagedRows().length) {
              <tr>
                <td [attr.colspan]="totalColumns()" class="p-0">
                  <ng-content select="[table-empty]">
                    <ui-empty-state [icon]="emptyIcon()" [title]="emptyTitle()" [description]="emptyDescription()"></ui-empty-state>
                  </ng-content>
                </td>
              </tr>
            } @else {
              @for (row of pagedRows(); track $index) {
                <tr class="transition-colors hover:bg-fleet-surface-alt/40">
                  @for (col of columns(); track col.key) {
                    <td
                      class="px-5 py-4 text-sm text-fleet-text"
                      [class.text-center]="col.align === 'center'"
                      [class.text-right]="col.align === 'right'">
                      @if (cellTemplate(col.key); as tpl) {
                        <ng-container [ngTemplateOutlet]="tpl" [ngTemplateOutletContext]="{ $implicit: row, row: row, index: $index }"></ng-container>
                      } @else {
                        {{ display(row, col) }}
                      }
                    </td>
                  }
                  @if (actionsTemplate(); as tpl) {
                    <td class="px-5 py-4 text-right">
                      <ng-container [ngTemplateOutlet]="tpl" [ngTemplateOutletContext]="{ $implicit: row, row: row, index: $index }"></ng-container>
                    </td>
                  }
                </tr>
              }
            }
          </tbody>
        </table>
      </div>

      @if (!loading() && pageCount() > 1) {
        <div class="stb-table-footer flex items-center justify-between gap-3 border-t border-fleet-border px-5 py-3 text-sm">
          <span class="text-fleet-text-muted">
            Showing {{ rangeStart() }}–{{ rangeEnd() }} of {{ filteredRows().length }}
          </span>
          <div class="flex items-center gap-1">
            <button
              type="button"
              class="rounded-sm p-1.5 text-fleet-text-muted transition-colors hover:bg-fleet-surface-muted disabled:opacity-40"
              [disabled]="page() === 0"
              (click)="prevPage()">
              <mat-icon class="!text-[20px]">chevron_left</mat-icon>
            </button>
            <span class="px-2 font-semibold text-fleet-text">{{ page() + 1 }} / {{ pageCount() }}</span>
            <button
              type="button"
              class="rounded-sm p-1.5 text-fleet-text-muted transition-colors hover:bg-fleet-surface-muted disabled:opacity-40"
              [disabled]="page() >= pageCount() - 1"
              (click)="nextPage()">
              <mat-icon class="!text-[20px]">chevron_right</mat-icon>
            </button>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    :host { display: block; min-width: 0; }
    mat-icon { display: inline-flex; align-items: center; justify-content: center; }
    @media (max-width: 767px) {
      .stb-table-footer {
        flex-direction: column;
        align-items: stretch;
      }
      .stb-table-footer > div:last-child {
        display: flex;
        justify-content: space-between;
      }
    }
  `]
})
export class UiDataTableComponent {
  readonly columns = input<UiTableColumn[]>([]);
  // Permissive row type: the table renders arbitrary record shapes by column key.
  readonly data = input<readonly any[]>([]);
  readonly title = input<string>();
  readonly loading = input(false);
  readonly searchable = input(false);
  readonly searchPlaceholder = input('Search...');
  readonly pageSize = input(10);
  readonly actionsLabel = input('Actions');
  readonly emptyIcon = input('inbox');
  readonly emptyTitle = input('No records found');
  readonly emptyDescription = input<string>();

  private readonly cellDirectives = contentChildren(UiTableCellDirective);

  protected readonly query = signal('');
  protected readonly sort = signal<UiTableSort | null>(null);
  protected readonly page = signal(0);

  private readonly templateMap = computed(() => {
    const map = new Map<string, UiTableCellDirective>();
    for (const dir of this.cellDirectives()) {
      map.set(dir.key(), dir);
    }
    return map;
  });

  constructor() {
    // Reset pagination whenever the data set or filter changes.
    effect(() => {
      this.data();
      this.query();
      this.page.set(0);
    }, { allowSignalWrites: true });
  }

  protected readonly filteredRows = computed(() => {
    const q = this.query().toLowerCase().trim();
    let rows = this.data();
    if (q) {
      rows = rows.filter((row) =>
        this.columns().some((col) => String(row[col.key] ?? '').toLowerCase().includes(q))
      );
    }
    const sort = this.sort();
    if (sort) {
      rows = [...rows].sort((a, b) => {
        const av = a[sort.key];
        const bv = b[sort.key];
        const cmp = this.compare(av, bv);
        return sort.direction === 'asc' ? cmp : -cmp;
      });
    }
    return rows;
  });

  protected readonly pageCount = computed(() =>
    Math.max(1, Math.ceil(this.filteredRows().length / this.pageSize()))
  );

  protected readonly pagedRows = computed(() => {
    const start = this.page() * this.pageSize();
    return this.filteredRows().slice(start, start + this.pageSize());
  });

  protected readonly rangeStart = computed(() =>
    this.filteredRows().length === 0 ? 0 : this.page() * this.pageSize() + 1
  );

  protected readonly rangeEnd = computed(() =>
    Math.min(this.filteredRows().length, (this.page() + 1) * this.pageSize())
  );

  protected readonly totalColumns = computed(() =>
    this.columns().length + (this.actionsTemplate() ? 1 : 0)
  );

  protected readonly skeletonRows = computed(() => Array.from({ length: Math.min(this.pageSize(), 5) }));

  cellTemplate(key: string) {
    const dir = this.templateMap().get(key);
    return dir && key !== 'actions' ? dir.template : null;
  }

  actionsTemplate() {
    return this.templateMap().get('actions')?.template ?? null;
  }

  display(row: TableRow, col: UiTableColumn): string {
    const value = row[col.key];
    return value == null ? '' : String(value);
  }

  onSearch(event: Event): void {
    this.query.set((event.target as HTMLInputElement).value);
  }

  toggleSort(key: string): void {
    const current = this.sort();
    if (current?.key === key) {
      this.sort.set({ key, direction: current.direction === 'asc' ? 'desc' : 'asc' });
    } else {
      this.sort.set({ key, direction: 'asc' });
    }
  }

  prevPage(): void {
    this.page.update((p) => Math.max(0, p - 1));
  }

  nextPage(): void {
    this.page.update((p) => Math.min(this.pageCount() - 1, p + 1));
  }

  private compare(a: unknown, b: unknown): number {
    if (typeof a === 'number' && typeof b === 'number') {
      return a - b;
    }
    return String(a ?? '').localeCompare(String(b ?? ''), undefined, { numeric: true });
  }
}
