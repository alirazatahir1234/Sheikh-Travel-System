import { ChangeDetectorRef, Component, OnDestroy, OnInit, inject } from '@angular/core';
import { Subscription } from 'rxjs';
import { VehicleListItem } from '../../../core/models/vehicle.model';
import { GpsScopeFilterService } from '../gps-scope-filter.service';

@Component({
  standalone: false,
  selector: 'app-gps-scope-filter-bar',
  templateUrl: './gps-scope-filter-bar.component.html',
  styleUrls: ['./gps-scope-filter-bar.component.scss']
})
export class GpsScopeFilterBarComponent implements OnInit, OnDestroy {
  readonly scope = inject(GpsScopeFilterService);
  private readonly cdr = inject(ChangeDetectorRef);

  vehicles: VehicleListItem[] = [];
  vehiclesLoading = false;

  private subs = new Subscription();

  ngOnInit(): void {
    this.vehicles = this.scope.vehicles;
    this.vehiclesLoading = this.scope.vehiclesLoading;

    this.subs.add(this.scope.vehicles$.subscribe(list => {
      this.vehicles = list;
      this.cdr.markForCheck();
    }));
    this.subs.add(this.scope.vehiclesLoading$.subscribe(v => {
      this.vehiclesLoading = v;
      this.cdr.markForCheck();
    }));
    this.subs.add(this.scope.scope$.subscribe(() => this.cdr.markForCheck()));
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }

  onVehicleChange(vehicleId: number | null): void {
    this.scope.setVehicle(vehicleId);
  }

  clearScope(): void {
    this.scope.clearScope();
  }
}
