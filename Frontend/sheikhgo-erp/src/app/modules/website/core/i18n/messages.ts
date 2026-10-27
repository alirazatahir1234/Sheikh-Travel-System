import { MESSAGES_AR } from './messages.ar';
import { MESSAGES_DE } from './messages.de';
import { MESSAGES_EN } from './messages.en';
import { WebsiteLanguage, WebsiteMessageDictionary } from './website-language';

export const WEBSITE_MESSAGES: Record<WebsiteLanguage, WebsiteMessageDictionary> = {
  en: MESSAGES_EN,
  ar: MESSAGES_AR,
  de: MESSAGES_DE,
};
