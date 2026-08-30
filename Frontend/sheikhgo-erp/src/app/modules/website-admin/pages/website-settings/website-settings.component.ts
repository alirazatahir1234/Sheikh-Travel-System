import { Component, OnInit } from '@angular/core';
import { WebsiteAdminService } from '../../services/website-admin.service';
import { WebsiteSettingsUpdate } from '../../models/website-admin.model';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';

@Component({
  standalone: false,
  selector: 'app-website-settings',
  templateUrl: './website-settings.component.html',
  styleUrls: ['./website-settings.component.scss']
})
export class WebsiteSettingsComponent implements OnInit {
  loading = true;
  saving = false;
  form: WebsiteSettingsUpdate = { siteName: '' };

  constructor(
    private api: WebsiteAdminService,
    private toast: UiToastService
  ) {}

  ngOnInit(): void {
    this.api.getSettings().subscribe({
      next: s => {
        this.form = {
          siteName: s.siteName,
          logoUrl: s.logoUrl,
          faviconUrl: s.faviconUrl,
          supportEmail: s.supportEmail,
          salesEmail: s.salesEmail,
          privacyEmail: s.privacyEmail,
          phone: s.phone,
          address: s.address,
          linkedInUrl: s.linkedInUrl,
          facebookUrl: s.facebookUrl,
          xUrl: s.xUrl,
          youTubeUrl: s.youTubeUrl,
          defaultMetaTitle: s.defaultMetaTitle,
          defaultMetaDescription: s.defaultMetaDescription,
          analyticsId: s.analyticsId
        };
        this.loading = false;
      },
      error: err => {
        this.loading = false;
        this.toast.error(apiErrorMessage(err, 'Failed to load settings.'));
      }
    });
  }

  save(): void {
    if (!this.form.siteName?.trim()) {
      this.toast.warning('Site name is required.');
      return;
    }
    this.saving = true;
    this.api.updateSettings(this.form).subscribe({
      next: s => {
        this.form.siteName = s.siteName;
        this.saving = false;
        this.toast.success('Settings saved.');
      },
      error: err => {
        this.saving = false;
        this.toast.error(apiErrorMessage(err, 'Failed to save settings.'));
      }
    });
  }
}
