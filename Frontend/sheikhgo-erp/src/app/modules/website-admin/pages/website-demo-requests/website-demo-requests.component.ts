import { Component, OnInit } from '@angular/core';
import { MatTableDataSource } from '@angular/material/table';
import { WebsiteAdminService } from '../../services/website-admin.service';
import { WebsiteDemoRequest, WEBSITE_LEAD_STATUSES } from '../../models/website-admin.model';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';

@Component({
  standalone: false,
  selector: 'app-website-demo-requests',
  templateUrl: './website-demo-requests.component.html',
  styleUrls: ['./website-demo-requests.component.scss']
})
export class WebsiteDemoRequestsComponent implements OnInit {
  loading = true;
  statusFilter = '';
  dataSource = new MatTableDataSource<WebsiteDemoRequest>([]);
  displayedColumns = ['name', 'company', 'email', 'product', 'status', 'createdAt'];
  readonly statuses = WEBSITE_LEAD_STATUSES;

  constructor(
    private api: WebsiteAdminService,
    private toast: UiToastService
  ) {}

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading = true;
    this.api.getDemoRequests(this.statusFilter || undefined).subscribe({
      next: page => {
        this.dataSource.data = page.items ?? [];
        this.loading = false;
      },
      error: err => {
        this.loading = false;
        this.toast.error(apiErrorMessage(err, 'Failed to load demo requests.'));
      }
    });
  }

  updateStatus(row: WebsiteDemoRequest, status: string): void {
    this.api.updateDemoStatus(row.id, status).subscribe({
      next: updated => {
        row.status = updated.status;
        this.toast.success('Status updated.');
      },
      error: err => this.toast.error(apiErrorMessage(err, 'Failed to update status.'))
    });
  }
}
