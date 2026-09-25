/** Shared client helper for 24h messaging window badge states. */
export const WhatsAppMessagingWindow = {
  isOpen(expiresAtIso: string | null | undefined, nowMs = Date.now()): boolean {
    if (!expiresAtIso) return false;
    const t = new Date(expiresAtIso).getTime();
    if (Number.isNaN(t)) return false;
    return t > nowMs;
  },

  tone(expiresAtIso: string | null | undefined, nowMs = Date.now()): 'open' | 'amber' | 'closed' {
    if (!this.isOpen(expiresAtIso, nowMs)) return 'closed';
    const remaining = new Date(expiresAtIso!).getTime() - nowMs;
    return remaining < 3600000 ? 'amber' : 'open';
  }
};
