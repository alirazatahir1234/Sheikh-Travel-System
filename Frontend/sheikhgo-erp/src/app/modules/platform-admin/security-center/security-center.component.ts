import { Component, OnDestroy, OnInit } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { PlatformTenantContextService } from '../../../core/services/platform-tenant-context.service';
import { PlatformService } from '../../../core/services/platform.service';
import { SecurityPolicyValue, Tenant } from '../../../core/models/platform.model';
import { apiErrorMessage } from '../../../core/utils/api-error.util';

@Component({
  standalone: false,
  selector: 'app-security-center',
  templateUrl: './security-center.component.html',
  styleUrls: ['./security-center.component.scss']
})
export class SecurityCenterComponent implements OnInit, OnDestroy {
  loading = false;
  saving = false;
  selectedTenant: Tenant | null = null;
  policies: SecurityPolicyValue[] = [];
  categoryFilter = '';
  drawerOpen = false;
  editing: SecurityPolicyValue | null = null;

  form;
  private destroy$ = new Subject<void>();

  constructor(
    private fb: FormBuilder,
    private route: ActivatedRoute,
    private router: Router,
    private toast: UiToastService,
    private tenantContext: PlatformTenantContextService,
    private platform: PlatformService
  ) {
    this.form = this.fb.group({
      policyValue: ['', Validators.required]
    });
  }

  ngOnInit(): void {
    const tenantIdParam = this.route.snapshot.queryParamMap.get('tenantId');
    if (tenantIdParam) {
      const id = parseInt(tenantIdParam, 10);
      if (Number.isFinite(id)) this.tenantContext.selectTenantById(id);
    }

    this.tenantContext.tenant$.pipe(takeUntil(this.destroy$)).subscribe(t => (this.selectedTenant = t));
    this.tenantContext.tenantId$.pipe(takeUntil(this.destroy$)).subscribe(id => {
      if (id) this.loadPolicies(id);
      else this.policies = [];
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  get categories(): string[] {
    return [...new Set(this.policies.map(p => p.category || 'General'))].sort((a, b) =>
      a.localeCompare(b)
    );
  }

  get filtered(): SecurityPolicyValue[] {
    if (!this.categoryFilter) return this.policies;
    return this.policies.filter(p => (p.category || 'General') === this.categoryFilter);
  }

  policiesInCategory(category: string): SecurityPolicyValue[] {
    return this.filtered.filter(p => (p.category || 'General') === category);
  }

  private loadPolicies(tenantId: number): void {
    this.loading = true;
    this.platform.getSecurityCompanyPolicies(tenantId).subscribe({
      next: rows => {
        this.policies = rows ?? [];
        this.loading = false;
      },
      error: err => {
        this.loading = false;
        this.toast.error(apiErrorMessage(err, 'Failed to load security policies.'));
      }
    });
  }

  openEdit(policy: SecurityPolicyValue): void {
    this.editing = policy;
    this.form.patchValue({ policyValue: policy.effectiveValue ?? policy.defaultValue ?? '' });
    this.drawerOpen = true;
  }

  closeDrawer(): void {
    this.drawerOpen = false;
    this.editing = null;
  }

  savePolicy(): void {
    if (!this.selectedTenant || !this.editing || this.form.invalid) return;
    this.saving = true;
    const value = String(this.form.value.policyValue ?? '');
    this.platform
      .updateSecurityCompanyPolicies({
        tenantId: this.selectedTenant.id,
        values: { [this.editing.policyKey]: value }
      })
      .subscribe({
        next: () => {
          this.saving = false;
          this.toast.success('Security policy updated.');
          this.closeDrawer();
          this.loadPolicies(this.selectedTenant!.id);
        },
        error: err => {
          this.saving = false;
          this.toast.error(apiErrorMessage(err, 'Failed to save security policy.'));
        }
      });
  }

  resetToDefault(policy: SecurityPolicyValue): void {
    if (!this.selectedTenant) return;
    this.saving = true;
    this.platform
      .updateSecurityCompanyPolicies({
        tenantId: this.selectedTenant.id,
        values: { [policy.policyKey]: policy.defaultValue }
      })
      .subscribe({
        next: () => {
          this.saving = false;
          this.toast.success('Policy reset to default.');
          this.loadPolicies(this.selectedTenant!.id);
        },
        error: err => {
          this.saving = false;
          this.toast.error(apiErrorMessage(err, 'Failed to reset policy.'));
        }
      });
  }

  formatUpdated(policy: SecurityPolicyValue): string {
    if (!policy.updatedDate) return '—';
    try {
      return new Date(policy.updatedDate).toLocaleString();
    } catch {
      return policy.updatedDate;
    }
  }

  isBool(policy: SecurityPolicyValue): boolean {
    return (policy.valueType || '').toLowerCase() === 'bool';
  }

  isInt(policy: SecurityPolicyValue): boolean {
    return (policy.valueType || '').toLowerCase() === 'int';
  }

  back(): void {
    this.router.navigate(['/platform']);
  }
}
