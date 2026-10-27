import { Pipe, PipeTransform, inject } from '@angular/core';
import { WebsiteI18nService } from '../core/website-i18n.service';

/** Re-evaluates when language changes (marketing site — small surface). */
@Pipe({ name: 'wsTranslate', standalone: true, pure: false })
export class WebsiteTranslatePipe implements PipeTransform {
  private readonly i18n = inject(WebsiteI18nService);

  transform(key: string, fallback?: string): string {
    this.i18n.lang();
    return this.i18n.t(key, fallback);
  }
}
