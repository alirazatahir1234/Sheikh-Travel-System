import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { forkJoin } from 'rxjs';
import { WebsiteAdminService } from '../../services/website-admin.service';
import {
  WebsitePage,
  WebsitePageUpdate,
  WebsiteSection,
  WebsiteSectionUpsert,
  WEBSITE_CONTENT_STATUSES
} from '../../models/website-admin.model';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';

@Component({
  standalone: false,
  selector: 'app-website-page-editor',
  templateUrl: './website-page-editor.component.html',
  styleUrls: ['./website-page-editor.component.scss']
})
export class WebsitePageEditorComponent implements OnInit {
  loading = true;
  savingPage = false;
  savingSection = false;
  pageId = 0;
  page: WebsitePage | null = null;
  pageForm: WebsitePageUpdate = { title: '' };
  sections: WebsiteSection[] = [];
  selected: WebsiteSection | null = null;
  sectionForm: WebsiteSectionUpsert = this.emptySection();
  readonly statuses = WEBSITE_CONTENT_STATUSES;

  constructor(
    private route: ActivatedRoute,
    private api: WebsiteAdminService,
    private toast: UiToastService
  ) {}

  ngOnInit(): void {
    this.pageId = Number(this.route.snapshot.paramMap.get('id'));
    if (!this.pageId) {
      this.loading = false;
      this.toast.error('Invalid page id.');
      return;
    }
    this.reload();
  }

  reload(): void {
    this.loading = true;
    forkJoin({
      pages: this.api.getPages(),
      sections: this.api.getPageSections(this.pageId)
    }).subscribe({
      next: ({ pages, sections }) => {
        this.page = pages.find(p => p.id === this.pageId) ?? null;
        if (this.page) {
          this.pageForm = {
            title: this.page.title,
            description: this.page.description,
            metaTitle: this.page.metaTitle,
            metaDescription: this.page.metaDescription,
            ogImage: this.page.ogImage,
            status: this.page.status
          };
        }
        this.sections = sections;
        this.loading = false;
        if (this.selected) {
          const match = sections.find(s => s.id === this.selected!.id);
          if (match) this.selectSection(match);
        }
      },
      error: err => {
        this.loading = false;
        this.toast.error(apiErrorMessage(err, 'Failed to load page.'));
      }
    });
  }

  savePage(): void {
    if (!this.page) return;
    this.savingPage = true;
    this.api.updatePage(this.page.id, this.pageForm).subscribe({
      next: page => {
        this.page = page;
        this.savingPage = false;
        this.toast.success('Page updated.');
      },
      error: err => {
        this.savingPage = false;
        this.toast.error(apiErrorMessage(err, 'Failed to update page.'));
      }
    });
  }

  publishPage(): void {
    if (!this.page) return;
    this.api.publishPage(this.page.id).subscribe({
      next: page => {
        this.page = page;
        this.pageForm.status = page.status;
        this.toast.success('Page published.');
      },
      error: err => this.toast.error(apiErrorMessage(err, 'Failed to publish page.'))
    });
  }

  selectSection(section: WebsiteSection): void {
    this.selected = section;
    this.sectionForm = {
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
    this.selected = null;
    this.sectionForm = this.emptySection(this.pageId);
  }

  saveSection(): void {
    this.savingSection = true;
    const req$ = this.sectionForm.id
      ? this.api.updateSection(this.sectionForm.id, this.sectionForm)
      : this.api.upsertSection(this.sectionForm);

    req$.subscribe({
      next: section => {
        this.savingSection = false;
        this.toast.success('Section saved.');
        this.selectSection(section);
        this.reload();
      },
      error: err => {
        this.savingSection = false;
        this.toast.error(apiErrorMessage(err, 'Failed to save section.'));
      }
    });
  }

  publishSection(): void {
    if (!this.sectionForm.id) return;
    this.api.publishSection(this.sectionForm.id).subscribe({
      next: section => {
        this.toast.success('Section published.');
        this.selectSection(section);
        this.reload();
      },
      error: err => this.toast.error(apiErrorMessage(err, 'Failed to publish section.'))
    });
  }

  private emptySection(pageId = this.pageId): WebsiteSectionUpsert {
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
