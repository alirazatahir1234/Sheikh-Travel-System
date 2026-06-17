import { environment } from '../../../environments/environment';

const IMAGE_EXTENSIONS = /\.(jpe?g|png|webp|gif)(\?.*)?$/i;
const PDF_EXTENSION = /\.pdf(\?.*)?$/i;

export function isImageUploadUrl(url: string | null | undefined): boolean {
  if (!url) return false;
  return IMAGE_EXTENSIONS.test(url.split('#')[0]);
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

export function resolveUploadUrl(url: string | null | undefined): string | null {
  if (!url) return null;
  if (url.startsWith('http')) return url;
  return `${environment.apiUrl.replace('/api', '')}${url}`;
}

export function resolveVehicleImageUrl(url: string | null | undefined): string | null {
  if (!isImageUploadUrl(url)) return null;
  return resolveUploadUrl(url);
}
