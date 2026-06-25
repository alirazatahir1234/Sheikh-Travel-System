import { HttpErrorResponse } from '@angular/common/http';
import { VEHICLE_UPLOAD_SIZE_ERROR } from './upload-url.util';

export function apiErrorMessage(error: unknown, fallback: string): string {
  if (error instanceof HttpErrorResponse) {
    const payload = error.error as {
      message?: string;
      title?: string;
      errors?: Record<string, string[] | string>;
      success?: boolean;
    } | string | null;

    if (typeof payload === 'string' && payload.trim()) {
      return normalizeUploadSizeError(payload);
    }

    if (payload && typeof payload === 'object') {
      if (payload.message) return normalizeUploadSizeError(payload.message);
      if (payload.errors) {
        const preferred = Object.entries(payload.errors).find(([key]) => !key.startsWith('command'));
        const first = preferred ? preferred[1] : Object.values(payload.errors)[0];
        if (Array.isArray(first) && first.length) return normalizeUploadSizeError(String(first[0]));
        if (first) return normalizeUploadSizeError(String(first));
      }
      if (payload.title && payload.title !== 'One or more validation errors occurred.') {
        return normalizeUploadSizeError(payload.title);
      }
    }
  }

  if (error instanceof Error && error.message?.trim()) {
    return normalizeUploadSizeError(error.message);
  }

  return fallback;
}

function normalizeUploadSizeError(message: string): string {
  return /2\s*mb|maximum size|file size/i.test(message) ? VEHICLE_UPLOAD_SIZE_ERROR : message;
}
