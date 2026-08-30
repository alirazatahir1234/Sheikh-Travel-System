import { Component, OnInit } from '@angular/core';
import { MatTableDataSource } from '@angular/material/table';
import { WebsiteAdminService } from '../../services/website-admin.service';
import { WebsitePage } from '../../models/website-admin.model';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';

@Component({
  standalone: false,
  selector: 'app-website-pages',
  templateUrl: './website-pages.component.html',
  styleUrls: ['./website-pages.component.scss']
})
export class WebsitePagesComponent implements OnInit {
  loading = true;
  dataSource = new MatTableDataSource<WebsitePage>([]);
  displayedColumns = ['title', 'slug', 'status', 'updatedAt', 'actions'];

  constructor(
    private api: WebsiteAdminService,
    private toast: UiToastService
  ) {}

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading = true;
    this.api.getPages().subscribe({
      next: rows => {
        this.dataSource.data = rows;
        this.loading = false;
      },
      error: err => {
        this.loading = false;
        this.toast.error(apiErrorMessage(err, 'Failed to load pages.'));
      }
    });
  }

  publish(row: WebsitePage): void {
    this.api.publishPage(row.id).subscribe({
      next: () => {
        this.toast.success('Page published.');
        this.reload();
      },
      error: err => this.toast.error(apiErrorMessage(err, 'Failed to publish page.'))
    });
  }
}
