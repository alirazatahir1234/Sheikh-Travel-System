import { Injectable } from '@angular/core';
import { OcrSettings } from '../models/ocr.model';

const OCR_SETTINGS_KEY = 'stb_ocr_settings';

@Injectable({ providedIn: 'root' })
export class OcrSettingsService {
  private readonly defaults: OcrSettings = {
    mode: 'HYBRID',
    confidenceThreshold: 70,
    enableFallback: true,
    saveRawOcr: false,
    azureEnabled: true,
    paddleEnabled: true
  };

  getSettings(): OcrSettings {
    try {
      const raw = localStorage.getItem(OCR_SETTINGS_KEY);
      if (!raw) return { ...this.defaults };
      const parsed = JSON.parse(raw) as Partial<OcrSettings>;
      return {
        ...this.defaults,
        ...parsed,
        confidenceThreshold: Number(parsed.confidenceThreshold ?? this.defaults.confidenceThreshold)
      };
    } catch {
      return { ...this.defaults };
    }
  }

  saveSettings(settings: OcrSettings): void {
    localStorage.setItem(OCR_SETTINGS_KEY, JSON.stringify(settings));
  }
}
