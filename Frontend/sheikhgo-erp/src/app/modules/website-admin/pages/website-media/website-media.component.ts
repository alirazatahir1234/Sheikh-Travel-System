import { Component, OnInit } from '@angular/core';
import { WebsiteAdminService } from '../../services/website-admin.service';
import { WebsiteMedia } from '../../models/website-admin.model';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import { environment } from '../../../../../environments/environment';

@Component({
  standalone: false,
  selector: 'app-website-media',
  templateUrl: './website-media.component.html',
  styleUrls: ['./website-media.component.scss']
})
export class WebsiteMediaComponent implements OnInit {
  loading = true;
  uploading = false;
  items: WebsiteMedia[] = [];
  altText = '';

  constructor(
    private api: WebsiteAdminService,
    private toast: UiToastService
  ) {}

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading = true;
    this.api.getMedia().subscribe({
      next: rows => {
        this.items = rows;
        this.loading = false;
      },
      error: err => {
        this.loading = false;
        this.toast.error(apiErrorMessage(err, 'Failed to load media.'));
      }
    });
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    this.uploading = true;
    this.api.uploadMedia(file, this.altText || undefined).subscribe({
      next: () => {
        this.uploading = false;
        this.altText = '';
        input.value = '';
        this.toast.success('Media uploaded.');
        this.reload();
      },
      error: err => {
        this.uploading = false;
        this.toast.error(apiErrorMessage(err, 'Upload failed.'));
      }
    });
  }

  resolveUrl(url: string): string {
    if (!url) return '';
    if (/^https?:\/\//i.test(url)) return url;
    const base = (environment as { filesBaseUrl?: string }).filesBaseUrl || '';
    if (url.startsWith('/') && base) return `${base}${url}`;
    return url;
  }

  copyUrl(item: WebsiteMedia): void {
    const url = this.resolveUrl(item.fileUrl);
    navigator.clipboard.writeText(url).then(
      () => this.toast.success('URL copied.'),
      () => this.toast.error('Could not copy URL.')
    );
  }

  remove(item: WebsiteMedia): void {
    if (!confirm(`Delete ${item.fileName}?`)) return;
    this.api.deleteMedia(item.id).subscribe({
      next: () => {
        this.toast.success('Media deleted.');
        this.reload();
      },
      error: err => this.toast.error(apiErrorMessage(err, 'Failed to delete media.'))
    });
  }

  isImage(item: WebsiteMedia): boolean {
    const t = (item.fileType || '').toLowerCase();
    const n = item.fileName.toLowerCase();
    return t.startsWith('image/') || /\.(png|jpe?g|gif|webp|svg)$/.test(n);
  }
}
