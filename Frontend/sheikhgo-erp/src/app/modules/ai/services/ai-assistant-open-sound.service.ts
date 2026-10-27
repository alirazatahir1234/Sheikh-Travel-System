import { Injectable } from '@angular/core';

/** sessionStorage — first SheikhGo AI Assistant open per browser tab session. */
const SESSION_OPEN_KEY = 'sheikhgo_ai_assistant_session_opened';

/**
 * Plays a short soft ping (~0.5s, low volume) the first time the assistant is opened in a session.
 * Requires a user gesture when using Web Audio (FAB click); navigation open may be silent under autoplay policy.
 */
@Injectable({ providedIn: 'root' })
export class AiAssistantOpenSoundService {
  notifyAssistantOpened(): void {
    if (typeof sessionStorage === 'undefined') {
      return;
    }
    if (sessionStorage.getItem(SESSION_OPEN_KEY) === '1') {
      return;
    }
    sessionStorage.setItem(SESSION_OPEN_KEY, '1');
    this.playSoftPing();
  }

  private playSoftPing(): void {
    if (typeof window === 'undefined') {
      return;
    }

    try {
      const AudioCtx =
        window.AudioContext
        ?? (window as Window & { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
      if (!AudioCtx) {
        return;
      }

      const ctx = new AudioCtx();
      const oscillator = ctx.createOscillator();
      const gain = ctx.createGain();

      oscillator.type = 'sine';
      oscillator.frequency.setValueAtTime(784, ctx.currentTime);
      oscillator.frequency.exponentialRampToValueAtTime(523.25, ctx.currentTime + 0.42);

      gain.gain.setValueAtTime(0.0001, ctx.currentTime);
      gain.gain.linearRampToValueAtTime(0.07, ctx.currentTime + 0.03);
      gain.gain.exponentialRampToValueAtTime(0.0001, ctx.currentTime + 0.5);

      oscillator.connect(gain);
      gain.connect(ctx.destination);
      oscillator.start(ctx.currentTime);
      oscillator.stop(ctx.currentTime + 0.52);
      oscillator.onended = () => {
        void ctx.close();
      };
    } catch {
      // Autoplay restrictions or missing audio — ignore silently.
    }
  }
}
