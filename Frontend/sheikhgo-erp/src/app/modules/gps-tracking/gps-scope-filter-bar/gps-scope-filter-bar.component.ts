import { Component } from '@angular/core';
import { GpsScopeFilterService } from '../gps-scope-filter.service';

@Component({
  standalone: false,
  selector: 'app-gps-scope-filter-bar',
  templateUrl: './gps-scope-filter-bar.component.html',
  styleUrls: ['./gps-scope-filter-bar.component.scss']
})
export class GpsScopeFilterBarComponent {
  constructor(readonly scope: GpsScopeFilterService) {}

  onCompanyChange(tenantId: number | null): void {
    this.scope.setCompany(tenantId);
  }

  onVehicleChange(vehicleId: number | null): void {
    this.scope.setVehicle(vehicleId);
  }

  clearScope(): void {
    this.scope.clearScope();
  }
}
