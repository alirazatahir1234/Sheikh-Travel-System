import {
  Component,
  forwardRef,
  Input,
  OnDestroy,
  OnInit
} from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { Subject, of } from 'rxjs';
import { catchError, takeUntil } from 'rxjs/operators';
import { PlatformService } from '../../../core/services/platform.service';
import { Tenant } from '../../../core/models/platform.model';

@Component({
  standalone: false,
  selector: 'app-company-select',
  templateUrl: './company-select.component.html',
  styleUrls: ['./company-select.component.scss'],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => CompanySelectComponent),
      multi: true
    }
  ]
})
export class CompanySelectComponent implements OnInit, OnDestroy, ControlValueAccessor {
  @Input() label = 'Company';
  /** When true, show a read-only company name (Company Admin). */
  @Input() readonlyMode = false;
  /** Display name when list is not loaded (Company Admin / fallback). */
  @Input() lockedLabel = '';
  /** Optional preloaded companies; when set, skips getTenants(). */
  @Input() companies: Tenant[] | null = null;

  tenants: Tenant[] = [];
  filterText = '';
  loading = false;
  value: number | null = null;
  disabled = false;

  private onChange: (v: number | null) => void = () => undefined;
  private touchedFn: () => void = () => undefined;
  private readonly destroy$ = new Subject<void>();

  constructor(private platform: PlatformService) {}

  ngOnInit(): void {
    if (this.readonlyMode) return;
    if (this.companies?.length) {
      this.tenants = this.companies.filter(t => t.isActive !== false);
      return;
    }
    this.loadTenants();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  get filteredTenants(): Tenant[] {
    const q = this.filterText.trim().toLowerCase();
    if (!q) return this.tenants;
    return this.tenants.filter(t =>
      t.name.toLowerCase().includes(q) ||
      (t.slug || '').toLowerCase().includes(q) ||
      (t.code || '').toLowerCase().includes(q)
    );
  }

  get displayName(): string {
    if (this.lockedLabel) return this.lockedLabel;
    const match = this.tenants.find(t => t.id === this.value);
    return match?.name || (this.value != null ? `Company #${this.value}` : '—');
  }

  writeValue(value: number | null): void {
    this.value = value ?? null;
  }

  registerOnChange(fn: (v: number | null) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.touchedFn = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.disabled = isDisabled;
  }

  markTouched(): void {
    this.touchedFn();
  }

  onSelect(next: number | null): void {
    this.value = next;
    this.onChange(next);
    this.touchedFn();
  }

  private loadTenants(): void {
    this.loading = true;
    this.platform.getTenants().pipe(
      catchError(() => of([] as Tenant[])),
      takeUntil(this.destroy$)
    ).subscribe(list => {
      this.tenants = (list ?? []).filter(t => t.isActive !== false);
      this.loading = false;
    });
  }
}
