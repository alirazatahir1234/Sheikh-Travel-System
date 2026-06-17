import { environment } from '../../../environments/environment';

const IMAGE_EXTENSIONS = /\.(jpe?g|png|webp|gif)(\?.*)?$/i;

export function isImageUploadUrl(url: string | null | undefined): boolean {
  if (!url) return false;
  return IMAGE_EXTENSIONS.test(url.split('#')[0]);
}

export function resolveUploadUrl(url: string | null | undefined): string | null {
  if (!url) return null;
  if (url.startsWith('http')) return url;
  return `${environment.apiUrl.replace('/api', '')}${url}`;
}

export function resolveVehicleImageUrl(url: string | null | undefined): string | null {
  if (!isImageUploadUrl(url)) return null;
  return resolveUploadUrl(url);
}
