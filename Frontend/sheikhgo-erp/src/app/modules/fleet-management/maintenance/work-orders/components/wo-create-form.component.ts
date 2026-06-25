import {
  ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, effect, inject, input, output, signal
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CurrencyPipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { UiToastService } from '../../../../../shared/components/ui/toast/ui-toast.service';
import { VehicleListItem } from '../../../../../core/models/vehicle.model';
import { Workshop } from '../../../../../core/models/maintenance.model';
import {
  defaultWoCreateForm,
  firstWoCreateFormError,
  MAX_COST,
  NOTES_MAX_LENGTH,
  toCreateWorkOrderPayload,
  validateWoCreateForm,
  WO_MAINTENANCE_TYPES,
  WO_PRIORITIES,
  WO_SERVICE_CHIPS,
  woCreateFormErrorMessages,
  WoCreateFormField,
  WoCreateFormValue
} from '../utils/wo-create-form.util';
import { CreateWorkOrderPayload } from '../../../../../core/models/maintenance.model';

const MAINT_TYPE_ICONS: Record<string, string> = {
  Preventive: 'verified',
  Corrective: 'build',
  Emergency: 'warning'
};

@Component({
  selector: 'wo-create-form',
  standalone: true,
  imports: [ReactiveFormsModule, MatIconModule, CurrencyPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './wo-create-form.component.html',
  styleUrls: ['./wo-create-form.component.scss']
})
export class WoCreateFormComponent {
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly toast = inject(UiToastService);
  private lastResetKey = -1;

  readonly priorities = WO_PRIORITIES;
  readonly maintTypes = WO_MAINTENANCE_TYPES;
  readonly serviceChips = WO_SERVICE_CHIPS;
  readonly notesMaxLength = NOTES_MAX_LENGTH;

  readonly vehicles = input<VehicleListItem[]>([]);
  readonly workshops = input<Workshop[]>([]);
  readonly saving = input(false);
  readonly resetKey = input(0);

  readonly submit = output<CreateWorkOrderPayload>();
  readonly cancel = output<void>();

  readonly submitted = signal(false);
  readonly errors = signal<Partial<Record<WoCreateFormField, string>>>({});
  readonly formValid = signal(false);
  readonly selectedServiceItems = signal<string[]>([]);

  readonly form = this.fb.nonNullable.group({
    vehicleId: [0, [Validators.required, Validators.min(1)]],
    workshopId: [0],
    priority: ['Medium', Validators.required],
    maintenanceType: ['Preventive', Validators.required],
    serviceItems: [[] as string[]],
    startDate: [''],
    estimatedCompletionDate: [''],
    laborCost: [0, [Validators.min(0), Validators.max(MAX_COST)]],
    partsCost: [0, [Validators.min(0), Validators.max(MAX_COST)]],
    notes: ['', Validators.maxLength(NOTES_MAX_LENGTH)]
  });

  constructor() {
    this.form.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.formValid.set(this.isValid());
      if (this.submitted() && !this.saving()) {
        this.syncErrors();
      }
      this.cdr.markForCheck();
    });

    effect(() => {
      const key = this.resetKey();
      if (key === this.lastResetKey) return;
      this.lastResetKey = key;
      this.reset();
    });

    effect(() => {
      this.vehicles();
      this.workshops();
      this.cdr.markForCheck();
    });

    this.formValid.set(this.isValid());
  }

  maintTypeIcon(type: string): string {
    return MAINT_TYPE_ICONS[type] ?? 'build';
  }

  estimatedTotal(): number {
    const raw = this.form.getRawValue();
    return (Number(raw.partsCost) || 0) + (Number(raw.laborCost) || 0);
  }

  notesLength(): number {
    return this.form.controls.notes.value.length;
  }

  validationMessages(): string[] {
    return woCreateFormErrorMessages(this.errors());
  }

  isServiceItemSelected(chip: string): boolean {
    return this.selectedServiceItems().includes(chip);
  }

  toggleServiceItem(chip: string): void {
    const items = [...this.selectedServiceItems()];
    const idx = items.indexOf(chip);
    if (idx >= 0) items.splice(idx, 1);
    else items.push(chip);
    this.selectedServiceItems.set(items);
    this.form.patchValue({ serviceItems: items });
    this.formValid.set(this.isValid());
    if (this.submitted()) {
      this.syncErrors();
    }
    this.cdr.markForCheck();
  }

  onSubmit(): void {
    if (this.saving()) return;

    this.submitted.set(true);
    this.form.markAllAsTouched();
    this.syncErrors();
    this.formValid.set(this.isValid());

    if (!this.formValid()) {
      const first = firstWoCreateFormError(this.errors());
      if (first) {
        this.toast.warning(first.message, 'Check required fields');
        this.scrollToField(first.field);
      }
      this.cdr.markForCheck();
      return;
    }

    this.submit.emit(toCreateWorkOrderPayload(this.getFormValue()));
  }

  onCancel(): void {
    this.reset();
    this.cancel.emit();
  }

  showError(field: WoCreateFormField): boolean {
    if (this.saving()) return false;
    return this.submitted() && !!this.errors()[field];
  }

  fieldError(field: WoCreateFormField): string {
    return this.errors()[field] ?? '';
  }

  private reset(): void {
    this.clearValidationState();
    this.selectedServiceItems.set([]);
    this.form.reset({ ...defaultWoCreateForm(), serviceItems: [] as string[] });
    this.formValid.set(this.isValid());
  }

  private clearValidationState(): void {
    this.submitted.set(false);
    this.errors.set({});
    this.form.markAsUntouched();
    this.form.markAsPristine();
  }

  private isValid(): boolean {
    return validateWoCreateForm(this.getFormValue()).valid;
  }

  private syncErrors(): void {
    this.errors.set(validateWoCreateForm(this.getFormValue()).errors);
  }

  private getFormValue(): WoCreateFormValue {
    const raw = this.form.getRawValue();
    const serviceItems = raw.serviceItems?.length
      ? [...raw.serviceItems]
      : [...this.selectedServiceItems()];
    return {
      vehicleId: Number(raw.vehicleId) || 0,
      workshopId: Number(raw.workshopId) || 0,
      priority: String(raw.priority ?? '').trim() || 'Medium',
      maintenanceType: String(raw.maintenanceType ?? '').trim() || 'Preventive',
      serviceItems,
      startDate: String(raw.startDate ?? '').trim(),
      estimatedCompletionDate: String(raw.estimatedCompletionDate ?? '').trim(),
      laborCost: Number(raw.laborCost) || 0,
      partsCost: Number(raw.partsCost) || 0,
      notes: String(raw.notes ?? '')
    };
  }

  private scrollToField(field: WoCreateFormField): void {
    queueMicrotask(() => {
      const el = document.querySelector(`[data-field="${field}"]`);
      el?.scrollIntoView({ behavior: 'smooth', block: 'center' });
    });
  }
}
