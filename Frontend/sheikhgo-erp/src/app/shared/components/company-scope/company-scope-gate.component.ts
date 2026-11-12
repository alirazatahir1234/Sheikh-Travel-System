import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  EventEmitter,
  Input,
  OnInit,
  Output,
  computed,
  inject,
  signal
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatIconModule } from '@angular/material/icon';
import { catchError, of } from 'rxjs';
import { PlatformService } from '../../../core/services/platform.service';
import { Tenant } from '../../../core/models/platform.model';

/**
 * Super Admin step shown before a create wizard starts: pick the company the new record belongs to.
 * The wizard only initialises (and, for vehicles, creates its server draft) after confirmation.
 */
@Component({
  selector: 'app-company-scope-gate',
  standalone: true,
  imports: [MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="company-gate mx-auto mt-6 max-w-xl" aria-labelledby="company-gate-title">
      <div class="company-gate__icon"><mat-icon>apartment</mat-icon></div>
      <h2 id="company-gate-title" class="company-gate__title">Which company is this {{ entityLabel }} for?</h2>
      <p class="company-gate__lead">
        As Super Admin you can add a {{ entityLabel }} to any company. Everything in this form — branches,
        departments, GPS devices and the saved record — will belong to the company you choose.
      </p>

      <label class="company-gate__label" for="company-gate-search">Company</label>
      <input
        id="company-gate-search"
        class="company-gate__input"
        type="search"
        placeholder="Search by name, code or slug…"
        autocomplete="off"
        [value]="filter()"
        (input)="filter.set($any($event.target).value)"
        [disabled]="loading()" />

      <select
        class="company-gate__input mt-2"
        aria-label="Company"
        [disabled]="loading() || !filtered().length"
        (change)="selectedId.set(+$any($event.target).value || null)">
        <option value="" [selected]="selectedId() == null" disabled>— Select company —</option>
        @for (t of filtered(); track t.id) {
          <option [value]="t.id" [selected]="t.id === selectedId()">
            {{ t.name }}{{ t.code ? ' · ' + t.code : '' }}
          </option>
        }
      </select>

      @if (loading()) {
        <p class="company-gate__hint">Loading companies…</p>
      } @else if (!tenants().length) {
        <p class="company-gate__hint">No active companies found.</p>
      } @else if (!filtered().length) {
        <p class="company-gate__hint">No companies match “{{ filter() }}”.</p>
      }

      <div class="company-gate__actions">
        <button type="button" class="company-gate__btn" (click)="cancelled.emit()">Cancel</button>
        <button
          type="button"
          class="company-gate__btn company-gate__btn--primary"
          [disabled]="!selectedTenant()"
          (click)="confirm()">
          Continue
          <mat-icon>arrow_forward</mat-icon>
        </button>
      </div>
    </section>
  `,
  styles: [`
    .company-gate {
      padding: 28px;
      border-radius: var(--stb-radius, 12px);
      background: var(--stb-surface, #fff);
      box-shadow: var(--stb-shadow, 0 1px 3px rgba(0, 0, 0, .08));
      color: var(--stb-text, inherit);
    }
    .company-gate__icon mat-icon { font-size: 32px; width: 32px; height: 32px; color: var(--stb-cat-blue, #2563eb); }
    .company-gate__title { margin: 8px 0 6px; font-size: 1.2rem; font-weight: 700; }
    .company-gate__lead { margin: 0 0 20px; font-size: .9rem; color: var(--stb-text-muted, #6b7280); }
    .company-gate__label { display: block; margin-bottom: 6px; font-size: 13px; font-weight: 600; }
    .company-gate__input {
      width: 100%; height: 40px; padding: 0 12px; border-radius: 8px;
      border: 1px solid rgba(127, 127, 127, .35); background: transparent; color: inherit; font: inherit;
    }
    .company-gate__hint { margin: 8px 0 0; font-size: 12px; color: var(--stb-text-muted, #6b7280); }
    .company-gate__actions { display: flex; justify-content: flex-end; gap: 10px; margin-top: 24px; flex-wrap: wrap; }
    .company-gate__btn {
      display: inline-flex; align-items: center; gap: 6px; height: 40px; padding: 0 18px;
      border-radius: 8px; border: 1px solid rgba(127, 127, 127, .35); background: transparent;
      color: inherit; font-weight: 600; cursor: pointer;
    }
    .company-gate__btn mat-icon { font-size: 18px; width: 18px; height: 18px; }
    .company-gate__btn--primary { border-color: transparent; background: var(--stb-cat-blue, #2563eb); color: #fff; }
    .company-gate__btn:disabled { opacity: .5; cursor: not-allowed; }
    @media (max-width: 480px) { .company-gate { padding: 20px 16px; } }
  `]
})
export class CompanyScopeGateComponent implements OnInit {
  /** e.g. "driver" / "vehicle". */
  @Input() entityLabel = 'record';
  /** Preselected company (route ?tenantId or the platform's selected company). */
  @Input() initialTenantId: number | null = null;

  @Output() readonly confirmed = new EventEmitter<Tenant>();
  @Output() readonly cancelled = new EventEmitter<void>();

  private readonly platform = inject(PlatformService);
  private readonly destroyRef = inject(DestroyRef);

  readonly loading = signal(true);
  readonly tenants = signal<Tenant[]>([]);
  readonly filter = signal('');
  readonly selectedId = signal<number | null>(null);

  readonly filtered = computed(() => {
    const q = this.filter().trim().toLowerCase();
    const list = this.tenants();
    if (!q) return list;
    return list.filter(t =>
      t.name.toLowerCase().includes(q)
      || (t.code ?? '').toLowerCase().includes(q)
      || (t.slug ?? '').toLowerCase().includes(q));
  });

  readonly selectedTenant = computed(() =>
    this.tenants().find(t => t.id === this.selectedId()) ?? null);

  ngOnInit(): void {
    this.selectedId.set(this.initialTenantId);
    this.platform.getTenants().pipe(
      catchError(() => of([] as Tenant[])),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(list => {
      const active = (list ?? []).filter(t => t.isActive !== false)
        .sort((a, b) => a.name.localeCompare(b.name));
      this.tenants.set(active);
      if (!active.some(t => t.id === this.selectedId())) this.selectedId.set(null);
      this.loading.set(false);
    });
  }

  confirm(): void {
    const tenant = this.selectedTenant();
    if (tenant) this.confirmed.emit(tenant);
  }
}
