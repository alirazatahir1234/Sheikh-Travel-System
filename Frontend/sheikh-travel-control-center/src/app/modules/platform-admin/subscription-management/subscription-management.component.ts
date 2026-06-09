import { Component, OnInit, OnDestroy } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Subject, takeUntil } from 'rxjs';
import { PlatformTenantContextService } from '../../../core/services/platform-tenant-context.service';
import { PlatformService } from '../../../core/services/platform.service';
import {
  Tenant,
  SubscriptionOverview,
  SubscriptionDetail,
  Invoice,
  Payment,
  SubscriptionAction,
  TENANT_PLAN_TIERS,
  formatCurrency
} from '../../../core/models/platform.model';
import { apiErrorMessage } from '../../../core/utils/api-error.util';

@Component({
  selector: 'app-subscription-management',
  templateUrl: './subscription-management.component.html',
  styleUrls: ['./subscription-management.component.scss']
})
export class SubscriptionManagementComponent implements OnInit, OnDestroy {
  loading = false;
  saving = false;
  selectedTenant: Tenant | null = null;
  overview: SubscriptionOverview | null = null;

  upgradePlan = '';
  readonly planOptions = TENANT_PLAN_TIERS;

  private destroy$ = new Subject<void>();

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private snackBar: MatSnackBar,
    private tenantContext: PlatformTenantContextService,
    private platform: PlatformService
  ) {}

  ngOnInit(): void {
    const tenantIdParam = this.route.snapshot.queryParamMap.get('tenantId');
    if (tenantIdParam) {
      const id = parseInt(tenantIdParam, 10);
      if (Number.isFinite(id)) {
        this.tenantContext.selectTenantById(id);
      }
    }

    this.tenantContext.tenant$
      .pipe(takeUntil(this.destroy$))
      .subscribe(tenant => {
        this.selectedTenant = tenant;
      });

    this.tenantContext.tenantId$
      .pipe(takeUntil(this.destroy$))
      .subscribe(id => {
        if (id) {
          this.loadOverview(id);
        } else {
          this.overview = null;
        }
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private loadOverview(tenantId: number): void {
    this.loading = true;
    this.platform.getSubscriptionOverview(tenantId).subscribe({
      next: (overview) => {
        this.overview = overview;
        this.upgradePlan = overview.subscription.planName ?? '';
        this.loading = false;
      },
      error: (err) => {
        this.loading = false;
        this.snackBar.open(apiErrorMessage(err, 'Failed to load subscription.'), 'Close', { duration: 4000 });
      }
    });
  }

  get subscription(): SubscriptionDetail | null {
    return this.overview?.subscription ?? null;
  }

  get invoices(): Invoice[] {
    return this.overview?.invoices ?? [];
  }

  get payments(): Payment[] {
    return this.overview?.payments ?? [];
  }

  formatMoney(amount?: number | null, currency?: string | null): string {
    return formatCurrency(amount, currency ?? this.subscription?.currencyCode ?? undefined);
  }

  statusClass(status: string): string {
    const s = status.toLowerCase();
    if (s === 'active') return 'status-active';
    if (s === 'suspended') return 'status-suspended';
    if (s === 'cancelled') return 'status-cancelled';
    return 'status-default';
  }

  invoiceStatusClass(status: string): string {
    const s = status.toLowerCase();
    if (s === 'paid') return 'inv-paid';
    if (s === 'pending') return 'inv-pending';
    if (s === 'overdue') return 'inv-overdue';
    return 'inv-default';
  }

  isExpiringSoon(): boolean {
    const end = this.subscription?.subscriptionEndDate;
    if (!end) return false;
    const days = (new Date(end).getTime() - Date.now()) / (1000 * 60 * 60 * 24);
    return days >= 0 && days <= 30;
  }

  isExpired(): boolean {
    const end = this.subscription?.subscriptionEndDate;
    if (!end) return false;
    return new Date(end).getTime() < Date.now();
  }

  upgrade(): void {
    if (!this.subscription || !this.upgradePlan) return;
    this.runAction({ action: 'Upgrade', planName: this.upgradePlan });
  }

  renew(): void {
    if (!confirm('Renew this subscription for another year? A new invoice will be generated.')) return;
    this.runAction({ action: 'Renew' });
  }

  suspend(): void {
    if (!confirm('Suspend this subscription?')) return;
    this.runAction({ action: 'Suspend' });
  }

  cancel(): void {
    if (!confirm('Cancel this subscription? Auto-renewal will be turned off.')) return;
    this.runAction({ action: 'Cancel' });
  }

  reactivate(): void {
    this.runAction({ action: 'Reactivate' });
  }

  toggleAutoRenew(autoRenew: boolean): void {
    if (!this.subscription) return;
    this.runAction({ action: this.subscription.status === 'Active' ? 'Upgrade' : 'Reactivate', autoRenew });
  }

  private runAction(request: { action: SubscriptionAction; planName?: string; autoRenew?: boolean }): void {
    if (!this.overview) return;
    const tenantId = this.overview.subscription.tenantId;

    this.saving = true;
    this.platform.updateSubscription(tenantId, request).subscribe({
      next: () => {
        this.saving = false;
        this.snackBar.open(`Subscription ${request.action.toLowerCase()} successful.`, 'Close', { duration: 2500 });
        this.loadOverview(tenantId);
      },
      error: (err) => {
        this.saving = false;
        this.snackBar.open(apiErrorMessage(err, 'Action failed.'), 'Close', { duration: 4000 });
      }
    });
  }

  refresh(): void {
    if (this.overview) this.loadOverview(this.overview.subscription.tenantId);
  }

  goBack(): void {
    this.router.navigate(['/platform/tenants']);
  }
}
