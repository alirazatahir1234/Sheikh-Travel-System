import { ChangeDetectionStrategy, Component, DestroyRef, inject, input, model, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatIconModule } from '@angular/material/icon';
import { Subject, catchError, debounceTime, distinctUntilChanged, of, switchMap } from 'rxjs';
import { TravelCustomerListItem } from '../models/travel.models';
import { TravelCustomerService } from '../services/travel-customer.service';

/** Search-as-you-type picker over travel customers (server search, 8 results). */
@Component({
  selector: 'app-travel-customer-picker',
  standalone: true,
  imports: [MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (selected(); as c) {
      <div class="tn-input flex items-center justify-between">
        <span><strong>{{ c.fullName }}</strong> <span class="text-fleet-text-muted">· {{ c.phone }}</span></span>
        @if (!disabled()) {
          <button type="button" class="text-fleet-text-muted" aria-label="Clear customer" (click)="clear()"><mat-icon class="!text-[18px]">close</mat-icon></button>
        }
      </div>
    } @else {
      <div class="relative">
        <input class="tn-input" [id]="inputId()" type="search" [placeholder]="placeholder()" autocomplete="off"
          [disabled]="disabled()" (input)="search$.next($any($event.target).value)" />
        @if (results().length || searching()) {
          <ul class="absolute z-20 mt-1 max-h-64 w-full overflow-auto rounded-lg border border-fleet-border bg-white shadow-lg" role="listbox">
            @if (searching()) { <li class="px-3 py-2 text-sm text-fleet-text-muted">Searching…</li> }
            @for (c of results(); track c.customerId) {
              <li role="option" class="cursor-pointer px-3 py-2 text-sm hover:bg-fleet-surface-alt" (mousedown)="pick(c)">
                <div class="font-semibold">{{ c.fullName }}</div>
                <div class="text-xs text-fleet-text-muted">{{ c.customerKind }} · {{ c.phone }}{{ c.city ? ' · ' + c.city : '' }}</div>
              </li>
            }
          </ul>
        }
      </div>
    }
  `,
  styleUrl: '../travel-nexus.scss'
})
export class TravelCustomerPickerComponent {
  private readonly service = inject(TravelCustomerService);
  private readonly destroyRef = inject(DestroyRef);

  readonly selected = model<TravelCustomerListItem | null>(null);
  readonly placeholder = input('Search customers by name or phone…');
  readonly inputId = input('customer-picker');
  readonly disabled = input(false);

  protected readonly results = signal<TravelCustomerListItem[]>([]);
  protected readonly searching = signal(false);
  protected readonly search$ = new Subject<string>();

  constructor() {
    this.search$.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      switchMap(term => {
        if (term.trim().length < 2) { this.searching.set(false); return of([] as TravelCustomerListItem[]); }
        this.searching.set(true);
        return this.service.getCustomers({ page: 1, pageSize: 8, search: term }).pipe(
          switchMap(r => of(r.items)),
          catchError(() => of([] as TravelCustomerListItem[]))
        );
      }),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(items => { this.results.set(items); this.searching.set(false); });
  }

  protected pick(c: TravelCustomerListItem): void {
    this.selected.set(c);
    this.results.set([]);
  }

  protected clear(): void { this.selected.set(null); }
}
