import { Component, OnDestroy, OnInit } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { forkJoin, Subject, takeUntil } from 'rxjs';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { PlatformService } from '../../../core/services/platform.service';
import { LookupService } from '../../../core/services/lookup.service';
import { UserService } from '../../../core/services/user.service';
import {
  Branch,
  BranchPayload,
  BranchStatus,
  DEFAULT_CURRENCY,
  BRANCH_STATUS_OPTIONS,
  BRANCH_TYPES,
  normalizeBranch,
  normalizeBranchStatus
} from '../../../core/models/platform.model';
import { User } from '../../../core/models/user.model';
import { apiErrorMessage } from '../../../core/utils/api-error.util';
import {
  branchNameValidator,
  phoneMaxDigitsValidator,
  branchAddressValidator,
  branchCityValidator,
  BRANCH_ADDRESS_MAX_LENGTH,
  BRANCH_CITY_MAX_LENGTH
} from './branch-form.validators';
import { UiSelectOption } from '../../../shared/components/ui/types/ui.types';

@Component({
  standalone: false,
  selector: 'app-branch-form',
  templateUrl: './branch-form.component.html',
  styleUrls: ['./branch-form.component.scss']
})
export class BranchFormComponent implements OnInit, OnDestroy {
  loading = true;
  saving = false;
  isEdit = false;
  branchId?: number;
  /** When set, use tenant-scoped branch APIs (from tenant detail / hierarchy). */
  tenantId: number | null = null;
  /** Full branch list for the tenant (includes current branch while editing). */
  private allBranches: Branch[] = [];
  branches: Branch[] = [];
  managers: User[] = [];

  readonly branchTypes = BRANCH_TYPES;
  countries: string[] = [];
  timezones: string[] = [];
  currencies: string[] = [];
  countryOptions: UiSelectOption[] = [];
  timezoneOptions: UiSelectOption[] = [];
  currencyOptions: UiSelectOption[] = [];
  readonly statusOptions = BRANCH_STATUS_OPTIONS;
  readonly BranchStatus = BranchStatus;
  readonly addressMaxLength = BRANCH_ADDRESS_MAX_LENGTH;
  readonly cityMaxLength = BRANCH_CITY_MAX_LENGTH;

  form;

  private readonly destroy$ = new Subject<void>();

  constructor(
    private fb: FormBuilder,
    private route: ActivatedRoute,
    private router: Router,
    private platform: PlatformService,
    private lookup: LookupService,
    private userService: UserService,
    private toast: UiToastService
  ) {
    this.form = this.fb.group({
      branchCode: ['', [Validators.required, Validators.maxLength(50)]],
      name: ['', [Validators.required, Validators.maxLength(200), branchNameValidator()]],
      branchType: ['Hub'],
      parentBranchId: [null as number | null],
      branchManagerUserId: [null as number | null],
      phone: ['', [phoneMaxDigitsValidator(15)]],
      email: ['', [Validators.email, Validators.maxLength(200)]],
      address: ['', [Validators.maxLength(BRANCH_ADDRESS_MAX_LENGTH), branchAddressValidator()]],
      city: ['', [Validators.maxLength(BRANCH_CITY_MAX_LENGTH), branchCityValidator()]],
      country: ['United Arab Emirates'],
      timeZone: ['Asia/Dubai'],
      currencyCode: [DEFAULT_CURRENCY],
      status: [BranchStatus.Active as BranchStatus, Validators.required],
      isGpsEnabled: [true]
    });
  }

  /** Preserve tenant context when navigating back to the list. */
  get listQueryParams(): { tenantId?: number } {
    return this.tenantId != null ? { tenantId: this.tenantId } : {};
  }

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    this.isEdit = !!idParam;
    this.branchId = idParam ? Number(idParam) : undefined;
    if (this.isEdit && (this.branchId == null || !Number.isFinite(this.branchId) || this.branchId <= 0)) {
      this.toast.error('Invalid branch id.');
      void this.navigateBackToList();
      return;
    }

    const tenantRaw = this.route.snapshot.queryParamMap.get('tenantId');
    const tenantParsed = tenantRaw != null && tenantRaw !== '' ? Number(tenantRaw) : NaN;
    this.tenantId = Number.isFinite(tenantParsed) && tenantParsed > 0 ? tenantParsed : null;

    forkJoin({
      countries: this.lookup.getCountryNames(),
      currencies: this.lookup.getCurrencyCodes(),
      timezones: this.lookup.getTimezoneIds()
    }).pipe(takeUntil(this.destroy$)).subscribe({
      next: ({ countries, currencies, timezones }) => {
        this.countries = countries;
        this.currencies = currencies;
        this.timezones = timezones;
        this.countryOptions = this.toSelectOptions(countries);
        this.currencyOptions = this.toSelectOptions(currencies);
        this.timezoneOptions = this.toSelectOptions(timezones);
        this.ensureLookupSelections();
      },
      error: () => { /* Lookups non-blocking; form keeps defaults. */ }
    });

    this.loadFormData();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  get addressLength(): number {
    return String(this.form.get('address')?.value ?? '').length;
  }

  isStatusSelected(value: BranchStatus): boolean {
    return normalizeBranchStatus(this.form.get('status')?.value) === value;
  }

  private loadFormData(): void {
    const branches$ = this.tenantId != null
      ? this.platform.getBranchesForTenant(this.tenantId)
      : this.platform.getBranches();
    const users$ = this.userService.getAll(1, 500, this.tenantId);

    if (this.isEdit && this.branchId) {
      if (this.tenantId != null) {
        // No get-by-id-for-tenant endpoint — resolve from the tenant branch list.
        forkJoin({ branches: branches$, users: users$ }).pipe(takeUntil(this.destroy$)).subscribe({
          next: ({ branches, users }) => {
            const branch = branches.find(b => b.id === this.branchId);
            if (!branch) {
              this.loading = false;
              this.toast.error('Branch not found for this company.');
              void this.navigateBackToList();
              return;
            }
            this.initForm(branches, users.items, normalizeBranch(branch));
          },
          error: (err) => {
            this.loading = false;
            this.toast.error(apiErrorMessage(err, 'Failed to load branch.'));
            void this.navigateBackToList();
          }
        });
      } else {
        forkJoin({
          branches: branches$,
          users: users$,
          branch: this.platform.getBranchById(this.branchId)
        }).pipe(takeUntil(this.destroy$)).subscribe({
          next: ({ branches, users, branch }) =>
            this.initForm(branches, users.items, normalizeBranch(branch)),
          error: (err) => {
            this.loading = false;
            this.toast.error(apiErrorMessage(err, 'Failed to load branch.'));
            void this.navigateBackToList();
          }
        });
      }
      return;
    }

    forkJoin({ branches: branches$, users: users$ }).pipe(takeUntil(this.destroy$)).subscribe({
      next: ({ branches, users }) => this.initForm(branches, users.items),
      error: (err) => {
        this.loading = false;
        this.toast.error(apiErrorMessage(err, 'Failed to load form data.'));
      }
    });
  }

  private initForm(branches: Branch[], users: User[], branch?: Branch): void {
    this.allBranches = (branches ?? []).map(normalizeBranch);
    this.branches = this.allBranches.filter(b => !this.branchId || b.id !== this.branchId);
    this.managers = users.filter(u => u.isActive);

    if (branch) {
      this.form.patchValue({
        branchCode: branch.branchCode,
        name: branch.name,
        branchType: branch.branchType ?? 'Hub',
        parentBranchId: branch.parentBranchId ?? null,
        branchManagerUserId: branch.branchManagerUserId ?? null,
        phone: branch.phone ?? '',
        email: branch.email ?? '',
        address: branch.address ?? '',
        city: branch.city ?? '',
        country: branch.country ?? 'United Arab Emirates',
        timeZone: branch.timeZone ?? 'Asia/Dubai',
        currencyCode: branch.currencyCode ?? DEFAULT_CURRENCY,
        status: normalizeBranchStatus(branch.status),
        isGpsEnabled: !!branch.isGpsEnabled
      });
    }

    this.ensureLookupSelections();
    this.loading = false;
  }

  private toSelectOptions(values: string[]): UiSelectOption[] {
    return values.map(value => ({ value, label: value }));
  }

  private ensureLookupSelections(): void {
    if (!this.countries.length && !this.timezones.length && !this.currencies.length) {
      return;
    }
    const country = this.pickExistingOrDefault(
      this.form.get('country')?.value,
      this.countries,
      'United Arab Emirates'
    );
    const timeZone = this.pickExistingOrDefault(
      this.form.get('timeZone')?.value,
      this.timezones,
      'Asia/Dubai'
    );
    const currencyCode = this.pickExistingOrDefault(
      this.form.get('currencyCode')?.value,
      this.currencies,
      DEFAULT_CURRENCY
    );
    this.form.patchValue({ country, timeZone, currencyCode }, { emitEvent: false });
  }

  private pickExistingOrDefault(
    current: string | null | undefined,
    options: string[],
    fallback: string
  ): string {
    if (current && options.includes(current)) return current;
    if (options.includes(fallback)) return fallback;
    return options[0] ?? fallback;
  }

  reset(): void {
    if (this.isEdit && this.branchId) {
      this.loading = true;
      if (this.tenantId != null) {
        this.platform.getBranchesForTenant(this.tenantId).pipe(takeUntil(this.destroy$)).subscribe({
          next: rows => {
            const branch = rows.find(b => b.id === this.branchId);
            if (!branch) {
              this.loading = false;
              this.toast.error('Branch not found.');
              return;
            }
            this.initForm(rows, this.managers, normalizeBranch(branch));
          },
          error: (err) => {
            this.loading = false;
            this.toast.error(apiErrorMessage(err, 'Failed to reload branch.'));
          }
        });
        return;
      }

      this.platform.getBranchById(this.branchId).pipe(takeUntil(this.destroy$)).subscribe({
        next: branch => this.initForm(this.allBranches, this.managers, normalizeBranch(branch)),
        error: (err) => {
          this.loading = false;
          this.toast.error(apiErrorMessage(err, 'Failed to reload branch.'));
        }
      });
      return;
    }

    this.form.reset({
      branchCode: '',
      name: '',
      branchType: 'Hub',
      parentBranchId: null,
      branchManagerUserId: null,
      phone: '',
      email: '',
      address: '',
      city: '',
      country: 'United Arab Emirates',
      timeZone: 'Asia/Dubai',
      currencyCode: DEFAULT_CURRENCY,
      status: BranchStatus.Active,
      isGpsEnabled: true
    });
    this.ensureLookupSelections();
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.toast.error('Please fix validation errors before saving.');
      return;
    }
    if (this.saving) return;

    const payload = this.buildPayload();
    this.saving = true;

    if (this.isEdit && this.branchId) {
      const req = this.tenantId != null
        ? this.platform.updateBranchForTenant(this.tenantId, this.branchId, payload)
        : this.platform.updateBranch(this.branchId, payload);
      req.pipe(takeUntil(this.destroy$)).subscribe({
        next: () => this.onSaveSuccess(true),
        error: (err: unknown) => this.onSaveError(err)
      });
    } else {
      const req = this.tenantId != null
        ? this.platform.createBranchForTenant(this.tenantId, payload)
        : this.platform.createBranch(payload);
      req.pipe(takeUntil(this.destroy$)).subscribe({
        next: () => this.onSaveSuccess(false),
        error: (err: unknown) => this.onSaveError(err)
      });
    }
  }

  private onSaveSuccess(isEdit: boolean): void {
    this.saving = false;
    this.toast.success(isEdit ? 'Branch updated.' : 'Branch created.');
    void this.navigateBackToList();
  }

  private onSaveError(err: unknown): void {
    this.saving = false;
    this.toast.error(apiErrorMessage(err, 'Save failed.'));
  }

  private buildPayload(): BranchPayload {
    const v = this.form.getRawValue();
    return {
      branchCode: v.branchCode!.trim().toUpperCase(),
      name: v.name!.trim(),
      branchType: v.branchType || null,
      parentBranchId: v.parentBranchId ?? null,
      branchManagerUserId: v.branchManagerUserId ?? null,
      phone: v.phone?.trim() || null,
      email: v.email?.trim() || null,
      address: v.address?.trim() || null,
      city: v.city?.trim() || null,
      country: v.country || null,
      timeZone: v.timeZone || null,
      currencyCode: v.currencyCode?.trim()?.toUpperCase() || null,
      status: normalizeBranchStatus(v.status),
      isGpsEnabled: !!v.isGpsEnabled
    };
  }

  cancel(): void {
    void this.navigateBackToList();
  }

  private navigateBackToList(): Promise<boolean> {
    return this.router.navigate(
      ['/platform/branches'],
      this.tenantId != null ? { queryParams: { tenantId: this.tenantId } } : undefined
    );
  }
}
