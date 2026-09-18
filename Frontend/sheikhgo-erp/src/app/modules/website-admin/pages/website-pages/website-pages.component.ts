import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { MatTableDataSource } from '@angular/material/table';
import { WebsiteAdminService } from '../../services/website-admin.service';
import {
  WebsitePage,
  WebsitePageCreate,
  WEBSITE_CONTENT_STATUSES
} from '../../models/website-admin.model';
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
  loadError: string | null = null;
  creating = false;
  showCreate = false;
  createForm: WebsitePageCreate = this.emptyCreate();
  dataSource = new MatTableDataSource<WebsitePage>([]);
  displayedColumns = ['title', 'slug', 'status', 'updatedAt', 'actions'];
  readonly statuses = WEBSITE_CONTENT_STATUSES;

  constructor(
    private api: WebsiteAdminService,
    private toast: UiToastService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.reload();
  }

  get showEmpty(): boolean {
    return !this.loading && !this.loadError && this.dataSource.data.length === 0;
  }

  reload(): void {
    this.loading = true;
    this.loadError = null;
    this.api.getPages().subscribe({
      next: rows => {
        this.dataSource.data = rows;
        this.loading = false;
      },
      error: err => {
        this.loading = false;
        this.dataSource.data = [];
        this.loadError = apiErrorMessage(err, 'Failed to load pages.');
        this.toast.error(this.loadError);
      }
    });
  }

  openCreate(): void {
    this.createForm = this.emptyCreate();
    this.showCreate = true;
  }

  cancelCreate(): void {
    this.showCreate = false;
  }

  create(): void {
    const slug = this.createForm.slug?.trim().toLowerCase() ?? '';
    const title = this.createForm.title?.trim() ?? '';
    if (!slug || !title) {
      this.toast.warning('Slug and title are required.');
      return;
    }
    this.creating = true;
    this.api.createPage({ ...this.createForm, slug, title }).subscribe({
      next: page => {
        this.creating = false;
        this.showCreate = false;
        this.toast.success('Page created.');
        void this.router.navigate(['/website/pages', page.id]);
      },
      error: err => {
        this.creating = false;
        this.toast.error(apiErrorMessage(err, 'Failed to create page.'));
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

  private emptyCreate(): WebsitePageCreate {
    return {
      slug: '',
      title: '',
      description: '',
      metaTitle: '',
      metaDescription: '',
      status: 'Draft'
    };
  }
}
