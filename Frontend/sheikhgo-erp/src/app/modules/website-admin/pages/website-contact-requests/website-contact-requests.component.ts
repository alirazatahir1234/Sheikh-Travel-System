import { Component, OnInit } from '@angular/core';
import { MatTableDataSource } from '@angular/material/table';
import { WebsiteAdminService } from '../../services/website-admin.service';
import { WebsiteContactRequest, WEBSITE_LEAD_STATUSES } from '../../models/website-admin.model';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';

@Component({
  standalone: false,
  selector: 'app-website-contact-requests',
  templateUrl: './website-contact-requests.component.html',
  styleUrls: ['./website-contact-requests.component.scss']
})
export class WebsiteContactRequestsComponent implements OnInit {
  loading = true;
  statusFilter = '';
  dataSource = new MatTableDataSource<WebsiteContactRequest>([]);
  displayedColumns = ['name', 'company', 'email', 'message', 'status', 'createdAt'];
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
    this.api.getContactRequests(this.statusFilter || undefined).subscribe({
      next: page => {
        this.dataSource.data = page.items ?? [];
        this.loading = false;
      },
      error: err => {
        this.loading = false;
        this.toast.error(apiErrorMessage(err, 'Failed to load contact requests.'));
      }
    });
  }

  updateStatus(row: WebsiteContactRequest, status: string): void {
    this.api.updateContactStatus(row.id, status).subscribe({
      next: updated => {
        row.status = updated.status;
        this.toast.success('Status updated.');
      },
      error: err => this.toast.error(apiErrorMessage(err, 'Failed to update status.'))
    });
  }
}
