import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { RentalService } from '../../../core/services/rental.service';
import { VehicleService } from '../../../core/services/vehicle.service';
import { RentalAvailability } from '../../../core/models/rental.model';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';

@Component({
  standalone: false,
  selector: 'app-rental-availability',
  templateUrl: './rental-availability.component.html',
  styleUrls: ['./rental-availability.component.scss']
})
export class RentalAvailabilityComponent implements OnInit {
  form: FormGroup;
  result: RentalAvailability | null = null;
  checking = false;
  vehicles: Array<{ id: number; name?: string; registrationNumber?: string }> = [];

  constructor(
    private fb: FormBuilder,
    private rentalService: RentalService,
    private vehicleService: VehicleService,
    private toast: UiToastService
  ) {
    this.form = this.fb.group({
      vehicleId: [null, Validators.required],
      from: ['', Validators.required],
      to: ['', Validators.required]
    });
  }

  ngOnInit(): void {
    this.vehicleService.getAll(1, 200).subscribe({
      next: r => { this.vehicles = r.items as any[]; },
      error: () => undefined
    });
  }

  check(): void {
    if (this.form.invalid) return;
    this.checking = true;
    this.result = null;
    const v = this.form.value;
    this.rentalService.checkAvailability(
      +v.vehicleId,
      new Date(v.from).toISOString(),
      new Date(v.to).toISOString()
    ).subscribe({
      next: r => { this.result = r; this.checking = false; },
      error: e => {
        this.checking = false;
        this.toast.error(e?.error?.message || 'Availability check failed');
      }
    });
  }
}
