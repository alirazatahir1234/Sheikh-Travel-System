import { Component, OnInit } from '@angular/core';
import { MatTableDataSource } from '@angular/material/table';
import { WebsiteAdminService } from '../../services/website-admin.service';
import {
  WebsiteFeature,
  WebsiteFeatureUpsert,
  WEBSITE_CONTENT_STATUSES
} from '../../models/website-admin.model';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';

@Component({
  standalone: false,
  selector: 'app-website-features',
  templateUrl: './website-features.component.html',
  styleUrls: ['./website-features.component.scss']
})
export class WebsiteFeaturesComponent implements OnInit {
  loading = true;
  saving = false;
  dataSource = new MatTableDataSource<WebsiteFeature>([]);
  displayedColumns = ['title', 'status', 'order', 'active', 'actions'];
  editing: WebsiteFeatureUpsert | null = null;
  readonly statuses = WEBSITE_CONTENT_STATUSES;

  constructor(
    private api: WebsiteAdminService,
    private toast: UiToastService
  ) {}

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading = true;
    this.api.getFeatures().subscribe({
      next: rows => {
        this.dataSource.data = rows;
        this.loading = false;
      },
      error: err => {
        this.loading = false;
        this.toast.error(apiErrorMessage(err, 'Failed to load features.'));
      }
    });
  }

  startAdd(): void {
    this.editing = {
      id: null,
      title: '',
      description: '',
      iconKey: '',
      imageUrl: '',
      linkUrl: '',
      displayOrder: this.dataSource.data.length,
      isActive: true,
      status: 'Draft'
    };
  }

  startEdit(row: WebsiteFeature): void {
    this.editing = {
      id: row.id,
      title: row.title,
      description: row.description,
      iconKey: row.iconKey,
      imageUrl: row.imageUrl,
      linkUrl: row.linkUrl,
      displayOrder: row.displayOrder,
      isActive: row.isActive,
      status: row.status
    };
  }

  cancelEdit(): void {
    this.editing = null;
  }

  save(): void {
    if (!this.editing?.title?.trim()) {
      this.toast.warning('Title is required.');
      return;
    }
    this.saving = true;
    const body = this.editing;
    const req$ = body.id
      ? this.api.updateFeature(body.id, body)
      : this.api.upsertFeature(body);

    req$.subscribe({
      next: () => {
        this.saving = false;
        this.editing = null;
        this.toast.success('Feature saved.');
        this.reload();
      },
      error: err => {
        this.saving = false;
        this.toast.error(apiErrorMessage(err, 'Failed to save feature.'));
      }
    });
  }

  publish(row: WebsiteFeature): void {
    this.api.publishFeature(row.id).subscribe({
      next: () => {
        this.toast.success('Feature published.');
        this.reload();
      },
      error: err => this.toast.error(apiErrorMessage(err, 'Failed to publish feature.'))
    });
  }

  remove(row: WebsiteFeature): void {
    if (!confirm(`Delete feature "${row.title}"?`)) return;
    this.api.deleteFeature(row.id).subscribe({
      next: () => {
        this.toast.success('Feature deleted.');
        this.reload();
      },
      error: err => this.toast.error(apiErrorMessage(err, 'Failed to delete feature.'))
    });
  }
}
