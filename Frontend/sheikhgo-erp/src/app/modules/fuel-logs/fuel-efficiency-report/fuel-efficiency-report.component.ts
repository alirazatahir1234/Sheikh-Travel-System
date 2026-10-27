import { Component, OnInit } from '@angular/core';
import { FormControl, FormGroup } from '@angular/forms';
import { FuelEfficiencyRow } from '../../../core/models/fuel-log.model';
import { FuelLogService } from '../../../core/services/fuel-log.service';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';

@Component({
  standalone: false,
  selector: 'app-fuel-efficiency-report',
  templateUrl: './fuel-efficiency-report.component.html',
  styleUrls: ['./fuel-efficiency-report.component.scss']
})
export class FuelEfficiencyReportComponent implements OnInit {
  loading = true;
  range = new FormGroup({
    from: new FormControl<string>(this.isoDateMonthsAgo(1)),
    to: new FormControl<string>(this.isoDateToday())
  });

  byVehicle: FuelEfficiencyRow[] = [];
  byDriver: FuelEfficiencyRow[] = [];
  byRoute: FuelEfficiencyRow[] = [];

  constructor(
    private fuelLogService: FuelLogService,
    private toast: UiToastService
  ) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    const from = this.range.value.from ?? undefined;
    const to = this.range.value.to ?? undefined;
    this.fuelLogService.getEfficiency(from, to).subscribe({
      next: report => {
        this.byVehicle = report.byVehicle ?? [];
        this.byDriver = report.byDriver ?? [];
        this.byRoute = report.byRoute ?? [];
        this.loading = false;
      },
      error: () => {
        this.toast.error('Could not load km/L report');
        this.loading = false;
      }
    });
  }

  formatKmL(value: number | null | undefined): string {
    return value != null && value > 0 ? value.toFixed(2) : '—';
  }

  private isoDateToday(): string {
    return new Date().toISOString().slice(0, 10);
  }

  private isoDateMonthsAgo(months: number): string {
    const d = new Date();
    d.setMonth(d.getMonth() - months);
    return d.toISOString().slice(0, 10);
  }
}
