import { Component, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { PlatformService } from '../../../core/services/platform.service';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import {
  SubscriptionRevenueDashboard,
  formatCurrency
} from '../../../core/models/platform.model';
import { apiErrorMessage } from '../../../core/utils/api-error.util';

@Component({
  standalone: false,
  selector: 'app-subscription-revenue-dashboard',
  templateUrl: './subscription-revenue-dashboard.component.html',
  styleUrls: ['./subscription-revenue-dashboard.component.scss']
})
export class SubscriptionRevenueDashboardComponent implements OnInit {
  loading = false;
  dashboard: SubscriptionRevenueDashboard | null = null;

  constructor(
    private platform: PlatformService,
    private toast: UiToastService
  ) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    this.platform.getSubscriptionRevenueDashboard().subscribe({
      next: (d) => {
        this.dashboard = d;
        this.loading = false;
      },
      error: (err) => {
        this.loading = false;
        this.toast.error(apiErrorMessage(err, 'Failed to load revenue dashboard.'));
      }
    });
  }

  money(amount: number | null | undefined, currency = 'PKR'): string {
    return formatCurrency(amount ?? 0, currency);
  }
}
