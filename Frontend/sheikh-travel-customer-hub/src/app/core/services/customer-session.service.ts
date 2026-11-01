import { Injectable, computed, signal } from '@angular/core';

const KEY_PHONE = 'sts_portal_phone';
const KEY_NAME = 'sts_portal_name';
const KEY_TOKEN = 'sts_portal_token';

@Injectable({ providedIn: 'root' })
export class CustomerSessionService {
  readonly phone = signal<string | null>(null);
  readonly fullName = signal<string | null>(null);
  readonly accessToken = signal<string | null>(null);
  readonly hasSession = computed(() => !!this.phone()?.trim());
  readonly sessionVersion = signal(0);

  constructor() {
    if (typeof sessionStorage === 'undefined') return;
    this.phone.set(sessionStorage.getItem(KEY_PHONE));
    this.fullName.set(sessionStorage.getItem(KEY_NAME));
    this.accessToken.set(sessionStorage.getItem(KEY_TOKEN));
  }

  isAuthenticated(): boolean {
    const phone = this.phone()?.trim();
    const token = this.accessToken()?.trim();
    if (!phone || !token) return false;
    return this.isValidPortalToken(token);
  }

  private isValidPortalToken(token: string): boolean {
    try {
      const parts = token.split('.');
      if (parts.length < 2) return true;
      const payload = parts[1].replace(/-/g, '+').replace(/_/g, '/');
      const padded = payload + '='.repeat((4 - (payload.length % 4)) % 4);
      const json = JSON.parse(atob(padded)) as Record<string, unknown>;
      const exp = Number(json['exp']);
      if (Number.isFinite(exp) && exp * 1000 <= Date.now()) {
        return false;
      }
      const roleClaim =
        json['role'] ??
        json['roles'] ??
        json['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'];
      if (typeof roleClaim === 'string') {
        return roleClaim === 'PortalCustomer';
      }
      if (Array.isArray(roleClaim)) {
        return roleClaim.includes('PortalCustomer');
      }
      return true;
    } catch {
      return false;
    }
  }

  /** Alias used in templates (OTP sign-in with JWT). */
  isSignedIn(): boolean {
    return this.isAuthenticated();
  }

  /** Saves name/phone for form prefill after anonymous booking (no JWT). */
  setLocalProfile(phone: string, fullName: string): void {
    const p = phone.trim();
    const n = fullName.trim();
    sessionStorage.setItem(KEY_PHONE, p);
    sessionStorage.setItem(KEY_NAME, n);
    this.phone.set(p);
    this.fullName.set(n);
    this.bumpVersion();
  }

  setSession(phone: string, fullName: string, accessToken: string): void {
    const p = phone.trim();
    const n = fullName.trim();
    const t = accessToken.trim();
    sessionStorage.setItem(KEY_PHONE, p);
    sessionStorage.setItem(KEY_NAME, n);
    sessionStorage.setItem(KEY_TOKEN, t);
    this.phone.set(p);
    this.fullName.set(n);
    this.accessToken.set(t);
    this.bumpVersion();
  }

  clear(): void {
    sessionStorage.removeItem(KEY_PHONE);
    sessionStorage.removeItem(KEY_NAME);
    sessionStorage.removeItem(KEY_TOKEN);
    this.phone.set(null);
    this.fullName.set(null);
    this.accessToken.set(null);
    this.bumpVersion();
  }

  bumpVersion(): void {
    this.sessionVersion.update((v) => v + 1);
  }
}
