import { Component, OnInit } from '@angular/core';
import { forkJoin } from 'rxjs';
import { WebsiteAdminService } from '../../services/website-admin.service';
import {
  WebsiteSection,
  WebsiteSectionUpsert,
  WEBSITE_CONTENT_STATUSES
} from '../../models/website-admin.model';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';

@Component({
  standalone: false,
  selector: 'app-website-home-editor',
  templateUrl: './website-home-editor.component.html',
  styleUrls: ['./website-home-editor.component.scss']
})
export class WebsiteHomeEditorComponent implements OnInit {
  loading = true;
  saving = false;
  homePageId = 0;
  sections: WebsiteSection[] = [];
  selected: WebsiteSection | null = null;
  form: WebsiteSectionUpsert = this.emptyForm();
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
    forkJoin({
      pages: this.api.getPages(),
      sections: this.api.getHomeSections()
    }).subscribe({
      next: ({ pages, sections }) => {
        this.homePageId = pages.find(p => p.slug.toLowerCase() === 'home')?.id
          ?? sections[0]?.pageId
          ?? 0;
        this.sections = sections;
        this.loading = false;
        if (this.selected) {
          const match = sections.find(s => s.id === this.selected!.id);
          if (match) this.select(match);
        } else if (!this.form.pageId && this.homePageId) {
          this.form.pageId = this.homePageId;
        }
      },
      error: err => {
        this.loading = false;
        this.toast.error(apiErrorMessage(err, 'Failed to load home sections.'));
      }
    });
  }

  select(section: WebsiteSection): void {
    this.selected = section;
    this.form = {
      id: section.id,
      pageId: section.pageId,
      sectionType: section.sectionType,
      title: section.title,
      subtitle: section.subtitle,
      content: section.content,
      imageUrl: section.imageUrl,
      buttonText: section.buttonText,
      buttonUrl: section.buttonUrl,
      secondaryButtonText: section.secondaryButtonText,
      secondaryButtonUrl: section.secondaryButtonUrl,
      displayOrder: section.displayOrder,
      isActive: section.isActive,
      status: section.status
    };
  }

  newSection(): void {
    const pageId = this.homePageId || this.sections[0]?.pageId || 0;
    this.selected = null;
    this.form = this.emptyForm(pageId);
  }

  save(): void {
    if (!this.form.pageId) {
      this.toast.warning('No home page id available. Open Pages and ensure a home page exists.');
      return;
    }
    this.saving = true;
    const req$ = this.form.id
      ? this.api.updateSection(this.form.id, this.form)
      : this.api.upsertSection(this.form);

    req$.subscribe({
      next: section => {
        this.saving = false;
        this.toast.success('Section saved.');
        this.select(section);
        this.reload();
      },
      error: err => {
        this.saving = false;
        this.toast.error(apiErrorMessage(err, 'Failed to save section.'));
      }
    });
  }

  publish(): void {
    if (!this.form.id) return;
    this.api.publishSection(this.form.id).subscribe({
      next: section => {
        this.toast.success('Section published.');
        this.select(section);
        this.reload();
      },
      error: err => this.toast.error(apiErrorMessage(err, 'Failed to publish section.'))
    });
  }

  private emptyForm(pageId = 0): WebsiteSectionUpsert {
    return {
      id: null,
      pageId,
      sectionType: 'content',
      title: '',
      subtitle: '',
      content: '',
      buttonText: '',
      buttonUrl: '',
      secondaryButtonText: '',
      secondaryButtonUrl: '',
      displayOrder: this.sections.length,
      isActive: true,
      status: 'Draft'
    };
  }
}
