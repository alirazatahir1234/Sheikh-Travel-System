import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-support-fab',
  standalone: true,
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="fixed bottom-6 right-6 z-40 flex flex-col items-end gap-2">
      @if (open()) {
        <div class="rounded-2xl border border-slate-200 bg-white p-3 shadow-lg text-sm space-y-2 min-w-[10rem]">
          <a
            class="block rounded-lg px-3 py-2 font-semibold text-slate-800 hover:bg-slate-50"
            [href]="whatsAppLink()"
            target="_blank"
            rel="noopener noreferrer"
            >WhatsApp</a
          >
          <a routerLink="/help" class="block rounded-lg px-3 py-2 font-semibold text-slate-800 hover:bg-slate-50"
            >Help center</a
          >
          <a href="tel:+923001234567" class="block rounded-lg px-3 py-2 font-semibold text-slate-800 hover:bg-slate-50"
            >Call support</a
          >
        </div>
      }
      <button
        type="button"
        class="rounded-full bg-primary-600 px-4 py-3 text-sm font-bold text-white shadow-lg hover:bg-primary-700"
        (click)="open.set(!open())"
      >
        Need help?
      </button>
    </div>
  `
})
export class SupportFabComponent {
  readonly open = signal(false);

  whatsAppLink(): string {
    const n = environment.whatsAppNumber.replace(/\D/g, '');
    return `https://wa.me/${n}?text=${encodeURIComponent('Hello Sheikh Travel, I need help with my booking.')}`;
  }
}
