import { Component, OnInit, OnDestroy } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { Subject, takeUntil } from 'rxjs';
import { TenantContextService } from '../../../core/services/tenant-context.service';
import { PlatformService } from '../../../core/services/platform.service';
import {
  Tenant,
  SubscriptionOverview,
  SubscriptionDetail,
  Invoice,
  Payment,
  SubscriptionAction,
  CompanyLicense,
  TENANT_PLAN_TIERS,
  BILLING_CYCLES,
  BillingCycle,
  billingCycleMonths,
  formatCurrency
} from '../../../core/models/platform.model';
import { apiErrorMessage } from '../../../core/utils/api-error.util';

@Component({
  standalone: false,
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
  upgradeCycle: BillingCycle = 'Monthly';
  readonly planOptions = TENANT_PLAN_TIERS;
  readonly billingCycles = BILLING_CYCLES;

  private destroy$ = new Subject<void>();

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private toast: UiToastService,
    private tenantContext: TenantContextService,
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
        const cycle = (overview.subscription.billingCycle || 'Monthly') as BillingCycle;
        this.upgradeCycle = cycle === 'Yearly' ? 'Yearly' : 'Monthly';
        this.loading = false;
      },
      error: (err) => {
        this.loading = false;
        this.toast.error(apiErrorMessage(err, 'Failed to load subscription.'));
      }
    });
  }

  get subscription(): SubscriptionDetail | null {
    return this.overview?.subscription ?? null;
  }

  get license(): CompanyLicense | null {
    return this.overview?.license ?? null;
  }

  get licensedModules(): string[] {
    return this.subscription?.licensedModuleCodes
      ?? this.license?.licensedModules
      ?? [];
  }

  get invoices(): Invoice[] {
    return this.overview?.invoices ?? [];
  }

  get payments(): Payment[] {
    return this.overview?.payments ?? [];
  }

  formatLimit(used: number, limit?: number | null): string {
    if (limit == null) return `${used} / ∞`;
    return `${used} / ${limit}`;
  }

  formatMoney(amount?: number | null, currency?: string | null): string {
    return formatCurrency(amount, currency ?? this.subscription?.currencyCode ?? undefined);
  }

  statusClass(status: string): string {
    const s = status.toLowerCase();
    if (s === 'active') return 'status-active';
    if (s === 'pastdue') return 'status-suspended';
    if (s === 'expired') return 'status-cancelled';
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
    const days = this.subscription?.remainingAccessDays;
    if (days == null) return false;
    return days >= 0 && days <= 7;
  }

  isExpired(): boolean {
    const status = (this.subscription?.status || '').toLowerCase();
    if (status === 'expired' || this.subscription?.tenantIsActive === false) return true;
    const days = this.subscription?.remainingAccessDays;
    return days != null && days < 0;
  }

  get isYearly(): boolean {
    return billingCycleMonths(this.subscription?.billingCycle) === 12;
  }

  get planChangePending(): boolean {
    const sub = this.subscription;
    if (!sub || !this.upgradePlan) return false;
    const cycleChanged = this.upgradeCycle !== (this.isYearly ? 'Yearly' : 'Monthly');
    return this.upgradePlan !== sub.planName || cycleChanged;
  }

  upgrade(): void {
    if (!this.subscription || !this.upgradePlan) return;
    this.runAction({
      action: 'Upgrade',
      planName: this.upgradePlan,
      billingCycle: this.upgradeCycle
    });
  }

  renew(): void {
    const period = this.isYearly ? 'year' : 'month';
    if (!confirm(`Renew this subscription for another ${period}? A new invoice will be generated.`)) return;
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

  private runAction(request: { action: SubscriptionAction; planName?: string; autoRenew?: boolean; billingCycle?: string }): void {
    if (!this.overview) return;
    const tenantId = this.overview.subscription.tenantId;

    this.saving = true;
    this.platform.updateSubscription(tenantId, request).subscribe({
      next: () => {
        this.saving = false;
        this.toast.success(`Subscription ${request.action.toLowerCase()} successful.`);
        this.loadOverview(tenantId);
      },
      error: (err) => {
        this.saving = false;
        this.toast.error(apiErrorMessage(err, 'Action failed.'));
      }
    });
  }

  markInvoicePaid(invoice: Invoice): void {
    if (!this.overview || invoice.status === 'Paid') return;
    if (!confirm(`Mark ${invoice.invoiceNumber} as paid?`)) return;
    const tenantId = this.overview.subscription.tenantId;
    this.saving = true;
    this.platform.markSubscriptionInvoicePaid(tenantId, invoice.id).subscribe({
      next: () => {
        this.saving = false;
        this.toast.success('Invoice marked paid — feature lock cleared if no other overdue invoices.');
        this.loadOverview(tenantId);
      },
      error: (err) => {
        this.saving = false;
        this.toast.error(apiErrorMessage(err, 'Failed to mark paid.'));
      }
    });
  }

  emailInvoice(invoice: Invoice): void {
    if (!this.overview) return;
    const tenantId = this.overview.subscription.tenantId;
    this.saving = true;
    this.platform.emailSubscriptionInvoice(tenantId, invoice.id).subscribe({
      next: () => {
        this.saving = false;
        this.toast.success('Invoice emailed to billing contact.');
      },
      error: (err) => {
        this.saving = false;
        this.toast.error(apiErrorMessage(err, 'Failed to send email.'));
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
