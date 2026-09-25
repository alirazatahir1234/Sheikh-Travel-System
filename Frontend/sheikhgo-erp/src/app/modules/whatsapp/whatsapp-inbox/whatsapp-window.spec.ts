import { WhatsAppMessagingWindow } from './whatsapp-window';

describe('WhatsAppMessagingWindow (client helper)', () => {
  it('treats missing expiry as closed', () => {
    expect(WhatsAppMessagingWindow.isOpen(null, Date.now())).toBe(false);
  });

  it('is open when expiry is in the future', () => {
    const now = Date.now();
    expect(WhatsAppMessagingWindow.isOpen(new Date(now + 60_000).toISOString(), now)).toBe(true);
  });

  it('is amber under one hour', () => {
    const now = Date.now();
    expect(WhatsAppMessagingWindow.tone(new Date(now + 30 * 60_000).toISOString(), now)).toBe('amber');
  });

  it('is closed when expired', () => {
    const now = Date.now();
    expect(WhatsAppMessagingWindow.tone(new Date(now - 1000).toISOString(), now)).toBe('closed');
  });
});
