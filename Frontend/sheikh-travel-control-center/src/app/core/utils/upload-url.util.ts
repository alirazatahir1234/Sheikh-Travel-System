import { environment } from '../../../environments/environment';

const PDF_EXTENSION = /\.pdf(\?.*)?$/i;

export function isImageUploadUrl(url: string | null | undefined): boolean {
  if (!url) return false;
  const path = url.split(/[?#]/)[0];
  return /\.(jpe?g|png|webp|gif)$/i.test(path);
}

export function isPdfUploadUrl(url: string | null | undefined): boolean {
  if (!url) return false;
  return PDF_EXTENSION.test(url.split('#')[0]);
}

export function isPdfFile(file: File): boolean {
  return file.type === 'application/pdf' || file.name.toLowerCase().endsWith('.pdf');
}

export function isImageFile(file: File): boolean {
  return file.type.startsWith('image/');
}

/**
 * Turn API-stored file paths into browser-loadable URLs.
 * - Azure / CDN: already absolute https://… — returned as-is
 * - Local dev: http://127.0.0.1:5082/uploads/… (via filesBaseUrl)
 * - Production SPA: /uploads/… (nginx proxies to API)
 */
export function resolveUploadUrl(url: string | null | undefined): string | null {
  if (!url) return null;

  const trimmed = url.trim();
  if (!trimmed) return null;
  if (/^(https?:|blob:)/i.test(trimmed)) return trimmed;

  const path = trimmed.startsWith('/') ? trimmed : `/${trimmed}`;
  const filesBase = (environment as { filesBaseUrl?: string }).filesBaseUrl?.replace(/\/$/, '');
  if (filesBase) return `${filesBase}${path}`;

  const apiBase = environment.apiUrl.replace(/\/api\/?$/, '');
  return apiBase ? `${apiBase}${path}` : path;
}

export function resolveVehicleImageUrl(url: string | null | undefined): string | null {
  if (!isImageUploadUrl(url)) return null;
  return resolveUploadUrl(url);
}

export const VEHICLE_UPLOAD_MAX_BYTES = 2 * 1024 * 1024;
export const VEHICLE_UPLOAD_SIZE_ERROR = 'File size is greater than 2 MB.';

export function vehicleUploadSizeError(file: File): string | null {
  return file.size > VEHICLE_UPLOAD_MAX_BYTES ? VEHICLE_UPLOAD_SIZE_ERROR : null;
}
