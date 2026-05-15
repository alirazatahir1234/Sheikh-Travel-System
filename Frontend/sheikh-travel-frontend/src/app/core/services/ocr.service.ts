import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { OcrExtractRequest, OcrExtractResult, OcrSettings } from '../models/ocr.model';

@Injectable({ providedIn: 'root' })
export class OcrService {
  private readonly legacyCnicEndpoint = `${environment.apiUrl}/customers/extract-cnic`;
  private readonly hybridEndpoint = `${environment.apiUrl}/ocr/extract-identity`;

  constructor(private http: HttpClient) {}

  extractFromDocument(file: File, settings: OcrSettings): Observable<OcrExtractResult> {
    const formData = new FormData();
    formData.append('file', file);

    const threshold = Number(settings.confidenceThreshold);
    const safeThreshold = Number.isFinite(threshold)
      ? Math.max(1, Math.min(100, Math.round(threshold)))
      : 70;

    const payload: OcrExtractRequest = {
      mode: this.toBackendMode(settings.mode),
      confidenceThreshold: safeThreshold,
      enableFallback: settings.enableFallback,
      includeRawText: settings.saveRawOcr
    };
    formData.append('request', JSON.stringify(payload));

    // Preferred hybrid endpoint; backend can map/fallback.
    return this.http.post<OcrExtractResult>(this.hybridEndpoint, formData);
  }

  extractFromLegacyCnicEndpoint(file: File): Observable<OcrExtractResult> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<OcrExtractResult>(this.legacyCnicEndpoint, formData);
  }

  private toBackendMode(mode: OcrSettings['mode']): OcrExtractRequest['mode'] {
    switch (mode) {
      case 'PADDLE_ONLY':
        return 'PaddleOnly';
      case 'AZURE_ONLY':
        return 'AzureOnly';
      default:
        return 'Hybrid';
    }
  }
}
