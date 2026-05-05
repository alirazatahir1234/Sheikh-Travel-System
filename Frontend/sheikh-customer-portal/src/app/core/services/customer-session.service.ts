import { Injectable, computed, signal } from '@angular/core';

const KEY_PHONE = 'sts_portal_phone';
const KEY_NAME = 'sts_portal_name';

@Injectable({ providedIn: 'root' })
export class CustomerSessionService {
  readonly phone = signal<string | null>(null);
  readonly fullName = signal<string | null>(null);
  readonly hasSession = computed(() => !!this.phone()?.trim());

  constructor() {
    if (typeof sessionStorage === 'undefined') return;
    this.phone.set(sessionStorage.getItem(KEY_PHONE));
    this.fullName.set(sessionStorage.getItem(KEY_NAME));
  }

  setSession(phone: string, fullName: string): void {
    const p = phone.trim();
    const n = fullName.trim();
    sessionStorage.setItem(KEY_PHONE, p);
    sessionStorage.setItem(KEY_NAME, n);
    this.phone.set(p);
    this.fullName.set(n);
  }

  clear(): void {
    sessionStorage.removeItem(KEY_PHONE);
    sessionStorage.removeItem(KEY_NAME);
    this.phone.set(null);
    this.fullName.set(null);
  }
}
