export type WebsiteLanguage = 'en' | 'ar' | 'de';

export const WEBSITE_LANGUAGE_STORAGE_KEY = 'sheikhgo.website.lang';

export const WEBSITE_LANGUAGES: readonly {
  code: WebsiteLanguage;
  label: string;
  nativeLabel: string;
}[] = [
  { code: 'en', label: 'English', nativeLabel: 'EN' },
  { code: 'ar', label: 'Arabic', nativeLabel: 'AR' },
  { code: 'de', label: 'German', nativeLabel: 'DE' },
] as const;

export const DEFAULT_WEBSITE_LANGUAGE: WebsiteLanguage = 'en';

export type WebsiteMessageDictionary = Record<string, string>;
