import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  OnInit,
  inject
} from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router } from '@angular/router';
import { catchError, of } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';
import { CompanyScopeService, isScopedArea } from '../../../core/services/company-scope.service';
import { PlatformService } from '../../../core/services/platform.service';
import { PlatformTenantContextService } from '../../../core/services/platform-tenant-context.service';
import { Tenant } from '../../../core/models/platform.model';

/**
 * Top-bar company dropdown for Super Admin. Sets the shared {@link CompanyScopeService} tenant
 * (X-Tenant-Id) so vehicles, drivers, and GPS trackers all inherit one company filter.
 */
@Component({
  selector: 'app-global-company-scope-select',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    MatFormFieldModule,
    MatSelectModule,
    MatIconModule,
    MatTooltipModule
  ],
  template: `
    @if (visible) {
      <div class="global-company-scope" [matTooltip]="tooltip">
        <mat-icon class="global-company-scope__icon" aria-hidden="true">apartment</mat-icon>
        <mat-form-field appearance="outline" class="global-company-scope__field" subscriptSizing="dynamic">
          <mat-label>Company</mat-label>
          <mat-select
            [value]="selectedId"
            [disabled]="loading"
            (selectionChange)="onSelect($event.value)"
            aria-label="Filter by company">
            <mat-option [value]="null">My company</mat-option>
            @for (c of companies; track c.id) {
              <mat-option [value]="c.id">{{ c.name }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
      </div>
    }
  `,
  styles: [`
    :host { display: contents; }

    .global-company-scope {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      max-width: min(220px, 28vw);
      min-width: 0;
      height: 42px;
      margin-right: 4px;
    }

    .global-company-scope__icon {
      flex: 0 0 auto;
      width: 20px;
      height: 20px;
      font-size: 20px;
      color: var(--stb-text-muted, #64748b);
    }

    .global-company-scope__field {
      flex: 1 1 auto;
      min-width: 0;
      width: 160px;
      max-width: 100%;

      ::ng-deep .mat-mdc-form-field-subscript-wrapper {
        display: none;
      }

      ::ng-deep .mat-mdc-text-field-wrapper {
        padding-inline: 8px !important;
      }

      ::ng-deep .mat-mdc-form-field-infix {
        min-height: 36px;
        padding-top: 8px !important;
        padding-bottom: 8px !important;
      }

      ::ng-deep .mat-mdc-select-value,
      ::ng-deep .mat-mdc-select-min-line {
        font-size: 13px;
      }
    }

    @media (max-width: 767px) {
      .global-company-scope {
        max-width: min(140px, 36vw);
      }

      .global-company-scope__field {
        width: 112px;
      }

      .global-company-scope__icon {
        display: none;
      }
    }
  `]
})
export class GlobalCompanyScopeSelectComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly scope = inject(CompanyScopeService);
  private readonly platform = inject(PlatformService);
  private readonly tenantContext = inject(PlatformTenantContextService);
  private readonly router = inject(Router);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);

  readonly visible =
    this.auth.hasRole('SUPER_ADMIN') || this.auth.hasRole('SUPERADMIN');

  companies: Tenant[] = [];
  loading = false;
  selectedId: number | null = null;

  get tooltip(): string {
    if (this.selectedId == null) {
      return 'Showing your home company. Pick a company to filter vehicles, drivers, and trackers.';
    }
    const name =
      this.companies.find(c => c.id === this.selectedId)?.name ??
      `Company #${this.selectedId}`;
    return `Filtering ERP data for ${name}`;
  }

  ngOnInit(): void {
    if (!this.visible) return;

    this.selectedId = this.scope.tenantId() ?? this.tenantContext.currentTenantId;
    if (this.selectedId != null && this.scope.tenantId() == null) {
      this.scope.set(this.selectedId);
    }

    toObservable(this.scope.tenantId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(id => {
        this.selectedId = id;
        this.cdr.markForCheck();
      });

    this.loading = true;
    this.platform
      .getTenants()
      .pipe(
        catchError(() => of([] as Tenant[])),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe(list => {
        this.companies = (list ?? []).filter(t => t.isActive !== false);
        this.loading = false;
        this.cdr.markForCheck();
      });
  }

  onSelect(tenantId: number | null): void {
    if (tenantId != null && tenantId > 0) {
      this.scope.set(tenantId);
      this.tenantContext.selectTenantById(tenantId);
      const match = this.companies.find(c => c.id === tenantId);
      if (match) this.tenantContext.setTenantDetails(match);
    } else {
      this.scope.clear();
      this.tenantContext.clear();
    }
    this.selectedId = tenantId;
    this.syncListRouteQuery(tenantId);
    this.cdr.markForCheck();
  }

  /** Keep vehicles/drivers deep-links and existing NavigationEnd reloads in sync. */
  private syncListRouteQuery(tenantId: number | null): void {
    if (!isScopedArea(this.router.url)) return;
    void this.router.navigate([], {
      queryParams: { tenantId: tenantId != null && tenantId > 0 ? tenantId : null },
      queryParamsHandling: 'merge',
      replaceUrl: true
    });
  }
}
