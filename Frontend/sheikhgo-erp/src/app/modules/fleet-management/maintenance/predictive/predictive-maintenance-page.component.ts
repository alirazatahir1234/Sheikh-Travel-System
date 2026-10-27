import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MaintenanceService } from '../../../../core/services/maintenance.service';
import { PredictiveMaintenanceAlert } from '../../../../core/models/maintenance.model';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';

@Component({
  selector: 'app-predictive-maintenance-page',
  standalone: true,
  imports: [CommonModule, RouterLink, MatButtonModule, MatIconModule],
  templateUrl: './predictive-maintenance-page.component.html',
  styleUrls: ['./predictive-maintenance-page.component.scss']
})
export class PredictiveMaintenancePageComponent implements OnInit {
  private readonly maintenance = inject(MaintenanceService);
  loading = false;
  error: string | null = null;
  alerts: PredictiveMaintenanceAlert[] = [];

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    this.error = null;
    this.maintenance.getDashboard().subscribe({
      next: dash => {
        this.alerts = dash.predictiveAlerts ?? [];
        this.loading = false;
      },
      error: err => {
        this.error = apiErrorMessage(err);
        this.loading = false;
      }
    });
  }

  severityClass(severity: string): string {
    return severity.toLowerCase() === 'critical' ? 'critical' : 'warning';
  }
}
