import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { HttpErrorResponse } from '@angular/common/http';
import { DriverAllowanceRuleService } from '../../../core/services/driver-allowance-rule.service';
import {
  AllowanceCalculationType,
  AllowanceCalculationTypeLabels,
  AllowanceCalculationUnit,
  CreateDriverAllowanceRuleDto,
  UpdateDriverAllowanceRuleDto
} from '../../../core/models/driver-allowance-rule.model';
import { FuelType, FuelTypeLabels } from '../../../core/models/vehicle.model';

@Component({
  selector: 'app-driver-allowance-rule-form',
  templateUrl: './rule-form.component.html',
  styleUrls: ['./rule-form.component.scss']
})
export class DriverAllowanceRuleFormComponent implements OnInit {
  form: FormGroup;
  loading = false;
  submitting = false;
  isEdit = false;
  ruleId: number | null = null;

  readonly calculationTypes = [
    AllowanceCalculationType.FixedAmount,
    AllowanceCalculationType.PerKm,
    AllowanceCalculationType.PerDay,
    AllowanceCalculationType.ProfitPercent
  ];
  readonly fuelTypes = [FuelType.Petrol, FuelType.Diesel, FuelType.CNG];

  constructor(
    private fb: FormBuilder,
    private ruleService: DriverAllowanceRuleService,
    private route: ActivatedRoute,
    private router: Router,
    private snackBar: MatSnackBar
  ) {
    this.form = this.fb.group({
      name:            ['', [Validators.required, Validators.maxLength(150)]],
      calculationType: [AllowanceCalculationType.FixedAmount, Validators.required],
      value:           [0, [Validators.required, Validators.min(0)]],
      priority:        [100, [Validators.required, Validators.min(0)]],
      minDistanceKm:   [null],
      maxDistanceKm:   [null],
      vehicleFuelType: [null],
      routeFilter:     [''],
      isActive:        [true],
      notes:           ['']
    });
  }

  ngOnInit(): void {
    this.route.paramMap.subscribe(params => {
      const id = params.get('id');
      if (!id) return;
      this.isEdit = true;
      this.ruleId = +id;
      this.loading = true;
      this.ruleService.getById(this.ruleId).subscribe({
        next: rule => {
          this.form.patchValue(rule);
          this.loading = false;
        },
        error: () => {
          this.loading = false;
          this.snackBar.open('Failed to load rule.', 'Close', { duration: 3000 });
        }
      });
    });
  }

  submit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.submitting = true;
    const f = this.form.value;

    const basePayload: CreateDriverAllowanceRuleDto = {
      name:            (f.name || '').trim(),
      calculationType: Number(f.calculationType),
      value:           Number(f.value ?? 0),
      priority:        Number(f.priority ?? 100),
      minDistanceKm:   this.numberOrNull(f.minDistanceKm),
      maxDistanceKm:   this.numberOrNull(f.maxDistanceKm),
      vehicleFuelType: f.vehicleFuelType ? Number(f.vehicleFuelType) : null,
      routeFilter:     (f.routeFilter || '').trim() || null,
      notes:           (f.notes || '').trim() || null
    };

    const done = () => {
      this.submitting = false;
      this.snackBar.open(`Rule ${this.isEdit ? 'updated' : 'created'}.`, 'Close', { duration: 2000 });
      this.router.navigate(['/driver-allowance-rules']);
    };

    const fail = (err: HttpErrorResponse) => {
      this.submitting = false;
      this.snackBar.open(this.extractError(err), 'Close', { duration: 4000 });
    };

    if (this.isEdit && this.ruleId) {
      const updatePayload: UpdateDriverAllowanceRuleDto = { ...basePayload, isActive: !!f.isActive };
      this.ruleService.update({ id: this.ruleId, rule: updatePayload })
        .subscribe({ next: done, error: fail });
    } else {
      this.ruleService.create({ rule: basePayload })
        .subscribe({ next: done, error: fail });
    }
  }

  typeLabel(t: AllowanceCalculationType): string { return AllowanceCalculationTypeLabels[t] ?? 'Unknown'; }
  unit(t: AllowanceCalculationType):      string { return AllowanceCalculationUnit[t]          ?? ''; }
  fuelLabel(f: FuelType):                 string { return FuelTypeLabels[f]                    ?? ''; }

  private numberOrNull(v: unknown): number | null {
    if (v === null || v === undefined || v === '') return null;
    const n = Number(v);
    return Number.isFinite(n) ? n : null;
  }

  private extractError(err: HttpErrorResponse): string {
    const body: any = err?.error;
    if (body?.errors) {
      const flat = Object.values(body.errors).flat();
      if (flat.length) return String(flat[0]);
    }
    if (body?.error?.message) return String(body.error.message);
    if (body?.message) return String(body.message);
    return `Operation failed (${err?.status || 'network'}).`;
  }
}
