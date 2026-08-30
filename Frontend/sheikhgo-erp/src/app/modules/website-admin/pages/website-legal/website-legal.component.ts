import { Component, OnInit } from '@angular/core';
import { WebsiteAdminService } from '../../services/website-admin.service';
import { WebsiteLegal } from '../../models/website-admin.model';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';

const DOC_TABS = [
  { key: 'privacy', label: 'Privacy' },
  { key: 'terms', label: 'Terms' },
  { key: 'cookie', label: 'Cookie' }
] as const;

@Component({
  standalone: false,
  selector: 'app-website-legal',
  templateUrl: './website-legal.component.html',
  styleUrls: ['./website-legal.component.scss']
})
export class WebsiteLegalComponent implements OnInit {
  loading = true;
  saving = false;
  readonly tabs = DOC_TABS;
  activeDoc: (typeof DOC_TABS)[number]['key'] = 'privacy';
  docs: Record<string, WebsiteLegal | null> = {
    privacy: null,
    terms: null,
    cookie: null
  };
  form = { title: '', content: '', version: '' };

  constructor(
    private api: WebsiteAdminService,
    private toast: UiToastService
  ) {}

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading = true;
    this.api.getLegal().subscribe({
      next: rows => {
        for (const key of Object.keys(this.docs)) this.docs[key] = null;
        for (const row of rows) {
          const key = row.docType.toLowerCase();
          if (key in this.docs) this.docs[key] = row;
        }
        this.applyActive();
        this.loading = false;
      },
      error: err => {
        this.loading = false;
        this.toast.error(apiErrorMessage(err, 'Failed to load legal documents.'));
      }
    });
  }

  selectTab(key: (typeof DOC_TABS)[number]['key']): void {
    this.activeDoc = key;
    this.applyActive();
  }

  save(): void {
    this.saving = true;
    this.api.updateLegal(this.activeDoc, {
      title: this.form.title,
      content: this.form.content,
      version: this.form.version || null
    }).subscribe({
      next: doc => {
        this.docs[this.activeDoc] = doc;
        this.saving = false;
        this.toast.success('Legal document saved.');
      },
      error: err => {
        this.saving = false;
        this.toast.error(apiErrorMessage(err, 'Failed to save legal document.'));
      }
    });
  }

  publish(): void {
    this.api.publishLegal(this.activeDoc).subscribe({
      next: doc => {
        this.docs[this.activeDoc] = doc;
        this.applyActive();
        this.toast.success('Legal document published.');
      },
      error: err => this.toast.error(apiErrorMessage(err, 'Failed to publish legal document.'))
    });
  }

  private applyActive(): void {
    const doc = this.docs[this.activeDoc];
    this.form = {
      title: doc?.title || this.defaultTitle(),
      content: doc?.content || '',
      version: doc?.version || ''
    };
  }

  private defaultTitle(): string {
    if (this.activeDoc === 'privacy') return 'Privacy Policy';
    if (this.activeDoc === 'terms') return 'Terms and Conditions';
    return 'Cookie Policy';
  }
}
