import { Component, OnInit } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { forkJoin } from 'rxjs';
import { PlatformService } from '../../../core/services/platform.service';
import { UserService } from '../../../core/services/user.service';
import {
  Branch,
  BranchPayload,
  BranchStatus,
  BRANCH_COUNTRIES,
  BRANCH_CURRENCIES,
  DEFAULT_CURRENCY,
  BRANCH_STATUS_OPTIONS,
  BRANCH_TIMEZONES,
  BRANCH_TYPES
} from '../../../core/models/platform.model';
import { User } from '../../../core/models/user.model';
import { apiErrorMessage } from '../../../core/utils/api-error.util';

@Component({
  selector: 'app-branch-form',
  templateUrl: './branch-form.component.html',
  styleUrls: ['./branch-form.component.scss']
})
export class BranchFormComponent implements OnInit {
  loading = true;
  saving = false;
  isEdit = false;
  branchId?: number;
  branches: Branch[] = [];
  managers: User[] = [];

  readonly branchTypes = BRANCH_TYPES;
  readonly countries = BRANCH_COUNTRIES;
  readonly timezones = BRANCH_TIMEZONES;
  readonly currencies = BRANCH_CURRENCIES;
  readonly statusOptions = BRANCH_STATUS_OPTIONS;
  readonly BranchStatus = BranchStatus;

  form;

  constructor(
    private fb: FormBuilder,
    private route: ActivatedRoute,
    private router: Router,
    private platform: PlatformService,
    private userService: UserService,
    private snackBar: MatSnackBar
  ) {
    this.form = this.fb.group({
      branchCode: ['', [Validators.required, Validators.maxLength(50)]],
      name: ['', [Validators.required, Validators.maxLength(200)]],
      branchType: ['Hub'],
      parentBranchId: [null as number | null],
      branchManagerUserId: [null as number | null],
      phone: [''],
      email: ['', Validators.email],
      address: [''],
      city: [''],
      country: ['United Arab Emirates'],
      timeZone: ['Asia/Dubai'],
      currencyCode: [DEFAULT_CURRENCY],
      status: [BranchStatus.Active, Validators.required],
      isGpsEnabled: [true]
    });
  }

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    this.isEdit = !!idParam;
    this.branchId = idParam ? Number(idParam) : undefined;

    const requests = {
      branches: this.platform.getBranches(),
      users: this.userService.getAll(1, 500)
    };

    if (this.isEdit && this.branchId) {
      forkJoin({ ...requests, branch: this.platform.getBranchById(this.branchId) }).subscribe({
        next: ({ branches, users, branch }) => this.initForm(branches, users.items, branch),
        error: () => {
          this.loading = false;
          this.snackBar.open('Failed to load branch.', 'Close', { duration: 4000 });
          void this.router.navigate(['/platform/branches']);
        }
      });
    } else {
      forkJoin(requests).subscribe({
        next: ({ branches, users }) => this.initForm(branches, users.items),
        error: () => {
          this.loading = false;
          this.snackBar.open('Failed to load form data.', 'Close', { duration: 4000 });
        }
      });
    }
  }

  private initForm(branches: Branch[], users: User[], branch?: Branch): void {
    this.branches = branches.filter(b => !this.branchId || b.id !== this.branchId);
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
        status: branch.status,
        isGpsEnabled: branch.isGpsEnabled
      });
    }

    this.loading = false;
  }

  reset(): void {
    if (this.isEdit && this.branchId) {
      this.loading = true;
      this.platform.getBranchById(this.branchId).subscribe({
        next: branch => {
          this.initForm(this.branches, this.managers, branch);
          this.loading = false;
        },
        error: () => { this.loading = false; }
      });
      return;
    }
    this.form.reset({
      branchType: 'Hub',
      country: 'United Arab Emirates',
      timeZone: 'Asia/Dubai',
      currencyCode: DEFAULT_CURRENCY,
      status: BranchStatus.Active,
      isGpsEnabled: true
    });
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.snackBar.open('Please fix validation errors before saving.', 'Close', { duration: 3000 });
      return;
    }
    if (this.saving) return;

    const payload = this.buildPayload();
    this.saving = true;

    if (this.isEdit && this.branchId) {
      this.platform.updateBranch(this.branchId, payload).subscribe({
        next: () => this.onSaveSuccess(true),
        error: (err: unknown) => this.onSaveError(err)
      });
    } else {
      this.platform.createBranch(payload).subscribe({
        next: () => this.onSaveSuccess(false),
        error: (err: unknown) => this.onSaveError(err)
      });
    }
  }

  private onSaveSuccess(isEdit: boolean): void {
    this.saving = false;
    this.snackBar.open(isEdit ? 'Branch updated.' : 'Branch created.', 'Close', { duration: 2500 });
    void this.router.navigate(['/platform/branches']);
  }

  private onSaveError(err: unknown): void {
    this.saving = false;
    this.snackBar.open(apiErrorMessage(err, 'Save failed.'), 'Close', { duration: 4000 });
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
      status: v.status ?? BranchStatus.Active,
      isGpsEnabled: !!v.isGpsEnabled
    };
  }

  cancel(): void {
    void this.router.navigate(['/platform/branches']);
  }
}
