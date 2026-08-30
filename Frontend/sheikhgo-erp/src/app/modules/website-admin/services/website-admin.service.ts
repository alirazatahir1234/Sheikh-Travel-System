import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { PagedResult } from '../../../core/models/common.model';
import {
  WebsiteContactRequest,
  WebsiteDashboard,
  WebsiteDemoRequest,
  WebsiteFeature,
  WebsiteFeatureUpsert,
  WebsiteLegal,
  WebsiteLegalUpdate,
  WebsiteMedia,
  WebsitePage,
  WebsitePageUpdate,
  WebsiteSection,
  WebsiteSectionUpsert,
  WebsiteSettings,
  WebsiteSettingsUpdate
} from '../models/website-admin.model';

@Injectable({ providedIn: 'root' })
export class WebsiteAdminService {
  private readonly base = `${environment.apiUrl}/website`;

  constructor(private http: HttpClient) {}

  getDashboard(): Observable<WebsiteDashboard> {
    return this.http.get<WebsiteDashboard>(`${this.base}/dashboard`);
  }

  getSettings(): Observable<WebsiteSettings> {
    return this.http.get<WebsiteSettings>(`${this.base}/settings`);
  }

  updateSettings(body: WebsiteSettingsUpdate): Observable<WebsiteSettings> {
    return this.http.put<WebsiteSettings>(`${this.base}/settings`, body);
  }

  getPages(): Observable<WebsitePage[]> {
    return this.http.get<WebsitePage[]>(`${this.base}/pages`);
  }

  updatePage(id: number, body: WebsitePageUpdate): Observable<WebsitePage> {
    return this.http.put<WebsitePage>(`${this.base}/pages/${id}`, body);
  }

  publishPage(id: number): Observable<WebsitePage> {
    return this.http.post<WebsitePage>(`${this.base}/pages/${id}/publish`, {});
  }

  getHomeSections(): Observable<WebsiteSection[]> {
    return this.http.get<WebsiteSection[]>(`${this.base}/home/sections`);
  }

  getPageSections(pageId: number): Observable<WebsiteSection[]> {
    return this.http.get<WebsiteSection[]>(`${this.base}/pages/${pageId}/sections`);
  }

  upsertSection(body: WebsiteSectionUpsert): Observable<WebsiteSection> {
    return this.http.post<WebsiteSection>(`${this.base}/sections`, body);
  }

  updateSection(id: number, body: WebsiteSectionUpsert): Observable<WebsiteSection> {
    return this.http.put<WebsiteSection>(`${this.base}/sections/${id}`, body);
  }

  deleteSection(id: number): Observable<boolean> {
    return this.http.delete<boolean>(`${this.base}/sections/${id}`);
  }

  publishSection(id: number): Observable<WebsiteSection> {
    return this.http.post<WebsiteSection>(`${this.base}/sections/${id}/publish`, {});
  }

  getFeatures(): Observable<WebsiteFeature[]> {
    return this.http.get<WebsiteFeature[]>(`${this.base}/features`);
  }

  upsertFeature(body: WebsiteFeatureUpsert): Observable<WebsiteFeature> {
    return this.http.post<WebsiteFeature>(`${this.base}/features`, body);
  }

  updateFeature(id: number, body: WebsiteFeatureUpsert): Observable<WebsiteFeature> {
    return this.http.put<WebsiteFeature>(`${this.base}/features/${id}`, body);
  }

  deleteFeature(id: number): Observable<boolean> {
    return this.http.delete<boolean>(`${this.base}/features/${id}`);
  }

  publishFeature(id: number): Observable<WebsiteFeature> {
    return this.http.post<WebsiteFeature>(`${this.base}/features/${id}/publish`, {});
  }

  getLegal(docType?: string): Observable<WebsiteLegal[]> {
    let params = new HttpParams();
    if (docType) params = params.set('docType', docType);
    return this.http.get<WebsiteLegal[]>(`${this.base}/legal`, { params });
  }

  updateLegal(docType: string, body: WebsiteLegalUpdate): Observable<WebsiteLegal> {
    return this.http.put<WebsiteLegal>(`${this.base}/legal/${docType}`, body);
  }

  publishLegal(docType: string): Observable<WebsiteLegal> {
    return this.http.post<WebsiteLegal>(`${this.base}/legal/${docType}/publish`, {});
  }

  getMedia(): Observable<WebsiteMedia[]> {
    return this.http.get<WebsiteMedia[]>(`${this.base}/media`);
  }

  uploadMedia(file: File, altText?: string): Observable<WebsiteMedia> {
    const form = new FormData();
    form.append('file', file, file.name);
    if (altText) form.append('altText', altText);
    return this.http.post<WebsiteMedia>(`${this.base}/media`, form);
  }

  deleteMedia(id: number): Observable<boolean> {
    return this.http.delete<boolean>(`${this.base}/media/${id}`);
  }

  getContactRequests(status?: string, page = 1, pageSize = 50): Observable<PagedResult<WebsiteContactRequest>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (status) params = params.set('status', status);
    return this.http.get<PagedResult<WebsiteContactRequest>>(`${this.base}/contact-requests`, { params });
  }

  updateContactStatus(id: number, status: string): Observable<WebsiteContactRequest> {
    return this.http.put<WebsiteContactRequest>(`${this.base}/contact-requests/${id}/status`, { status });
  }

  getDemoRequests(status?: string, page = 1, pageSize = 50): Observable<PagedResult<WebsiteDemoRequest>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (status) params = params.set('status', status);
    return this.http.get<PagedResult<WebsiteDemoRequest>>(`${this.base}/demo-requests`, { params });
  }

  updateDemoStatus(id: number, status: string): Observable<WebsiteDemoRequest> {
    return this.http.put<WebsiteDemoRequest>(`${this.base}/demo-requests/${id}/status`, { status });
  }
}
