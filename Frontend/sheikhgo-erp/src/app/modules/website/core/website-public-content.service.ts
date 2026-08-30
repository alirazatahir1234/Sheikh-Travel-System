import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { catchError, map, shareReplay } from 'rxjs/operators';
import { environment } from '../../../../environments/environment';

export interface PublicWebsiteSettings {
  id: number;
  siteName: string;
  logoUrl?: string | null;
  faviconUrl?: string | null;
  supportEmail?: string | null;
  salesEmail?: string | null;
  privacyEmail?: string | null;
  phone?: string | null;
  address?: string | null;
  linkedInUrl?: string | null;
  facebookUrl?: string | null;
  xUrl?: string | null;
  youTubeUrl?: string | null;
  defaultMetaTitle?: string | null;
  defaultMetaDescription?: string | null;
}

export interface PublicWebsiteSection {
  id: number;
  pageId: number;
  sectionType: string;
  title?: string | null;
  subtitle?: string | null;
  content?: string | null;
  imageUrl?: string | null;
  buttonText?: string | null;
  buttonUrl?: string | null;
  secondaryButtonText?: string | null;
  secondaryButtonUrl?: string | null;
  displayOrder: number;
  isActive: boolean;
  status: string;
}

export interface PublicWebsiteFeature {
  id: number;
  title: string;
  description?: string | null;
  iconKey?: string | null;
  imageUrl?: string | null;
  linkUrl?: string | null;
  displayOrder: number;
}

export interface PublicWebsiteHome {
  settings: PublicWebsiteSettings;
  sections: PublicWebsiteSection[];
  features: PublicWebsiteFeature[];
}

export interface PublicWebsitePage {
  page: {
    id: number;
    slug: string;
    title: string;
    description?: string | null;
    metaTitle?: string | null;
    metaDescription?: string | null;
  };
  sections: PublicWebsiteSection[];
}

export interface PublicWebsiteLegal {
  id: number;
  docType: string;
  title: string;
  content: string;
  version?: string | null;
  publishedAt?: string | null;
}

@Injectable({ providedIn: 'root' })
export class WebsitePublicContentService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl.replace(/\/$/, '')}/website/public`;

  private settings$?: Observable<PublicWebsiteSettings | null>;

  getHome(): Observable<PublicWebsiteHome | null> {
    return this.http.get<PublicWebsiteHome>(`${this.base}/home`).pipe(
      catchError(() => of(null)),
    );
  }

  getPage(slug: string): Observable<PublicWebsitePage | null> {
    return this.http.get<PublicWebsitePage>(`${this.base}/pages/${encodeURIComponent(slug)}`).pipe(
      catchError(() => of(null)),
    );
  }

  getFeatures(): Observable<PublicWebsiteFeature[]> {
    return this.http.get<PublicWebsiteFeature[]>(`${this.base}/features`).pipe(
      map(list => list ?? []),
      catchError(() => of([])),
    );
  }

  getLegal(docType: string): Observable<PublicWebsiteLegal | null> {
    return this.http.get<PublicWebsiteLegal>(`${this.base}/legal/${encodeURIComponent(docType)}`).pipe(
      catchError(() => of(null)),
    );
  }

  getSettings(): Observable<PublicWebsiteSettings | null> {
    if (!this.settings$) {
      this.settings$ = this.http.get<PublicWebsiteSettings>(`${this.base}/settings`).pipe(
        catchError(() => of(null)),
        shareReplay(1),
      );
    }
    return this.settings$;
  }

  sectionByType(sections: PublicWebsiteSection[] | null | undefined, type: string): PublicWebsiteSection | null {
    return sections?.find(s => s.sectionType.toLowerCase() === type.toLowerCase()) ?? null;
  }
}
