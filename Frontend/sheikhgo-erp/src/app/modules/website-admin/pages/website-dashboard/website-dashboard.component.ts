import { Component, OnInit } from '@angular/core';
import { WebsiteAdminService } from '../../services/website-admin.service';
import { WebsiteDashboard } from '../../models/website-admin.model';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';

@Component({
  standalone: false,
  selector: 'app-website-dashboard',
  templateUrl: './website-dashboard.component.html',
  styleUrls: ['./website-dashboard.component.scss']
})
export class WebsiteDashboardComponent implements OnInit {
  loading = true;
  dashboard: WebsiteDashboard | null = null;

  constructor(
    private api: WebsiteAdminService,
    private toast: UiToastService
  ) {}

  ngOnInit(): void {
    this.api.getDashboard().subscribe({
      next: d => {
        this.dashboard = d;
        this.loading = false;
      },
      error: err => {
        this.loading = false;
        this.toast.error(apiErrorMessage(err, 'Failed to load website dashboard.'));
      }
    });
  }

  viewWebsite(): void {
    window.open('/', '_blank');
  }
}
