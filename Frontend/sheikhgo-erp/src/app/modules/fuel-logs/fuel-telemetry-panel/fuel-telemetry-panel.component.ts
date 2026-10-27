import { Component, OnInit } from '@angular/core';
import { VehicleFuelTelemetry } from '../../../core/models/fuel-log.model';
import { FuelLogService } from '../../../core/services/fuel-log.service';
import { VehicleService } from '../../../core/services/vehicle.service';
import { VehicleListItem } from '../../../core/models/vehicle.model';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';

@Component({
  standalone: false,
  selector: 'app-fuel-telemetry-panel',
  templateUrl: './fuel-telemetry-panel.component.html',
  styleUrls: ['./fuel-telemetry-panel.component.scss']
})
export class FuelTelemetryPanelComponent implements OnInit {
  vehicles: VehicleListItem[] = [];
  selectedVehicleId: number | null = null;
  telemetry: VehicleFuelTelemetry | null = null;
  loadingVehicles = true;
  loadingTelemetry = false;

  constructor(
    private vehicleService: VehicleService,
    private fuelLogService: FuelLogService,
    private toast: UiToastService
  ) {}

  ngOnInit(): void {
    this.vehicleService.getAll(1, 500).subscribe({
      next: page => {
        this.vehicles = page.items ?? [];
        this.loadingVehicles = false;
        if (this.vehicles.length === 1) {
          this.selectedVehicleId = this.vehicles[0].id;
          this.loadTelemetry();
        }
      },
      error: () => {
        this.loadingVehicles = false;
        this.toast.error('Could not load vehicles');
      }
    });
  }

  onVehicleChange(vehicleId: number | null): void {
    this.selectedVehicleId = vehicleId;
    this.telemetry = null;
    if (vehicleId) this.loadTelemetry();
  }

  loadTelemetry(): void {
    if (!this.selectedVehicleId) return;
    this.loadingTelemetry = true;
    this.fuelLogService.getTelemetry(this.selectedVehicleId).subscribe({
      next: t => {
        this.telemetry = t;
        this.loadingTelemetry = false;
      },
      error: () => {
        this.toast.error('Could not load OBD fuel telemetry');
        this.loadingTelemetry = false;
      }
    });
  }

  display(value: number | null | undefined, suffix = ''): string {
    return value != null ? `${value}${suffix}` : '—';
  }
}
