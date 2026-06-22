import { ApplicationRef, createComponent, EnvironmentInjector, inject, Injectable } from '@angular/core';
import { UiToastComponent, UiToastVariant } from './ui-toast.component';

interface ToastConfig {
  variant: UiToastVariant;
  message: string;
  title?: string;
  duration?: number;
}

@Injectable({ providedIn: 'root' })
export class UiToastService {
  private readonly appRef = inject(ApplicationRef);
  private readonly injector = inject(EnvironmentInjector);

  private container: HTMLElement | null = null;

  private getContainer(): HTMLElement {
    if (!this.container || !document.body.contains(this.container)) {
      const el = document.createElement('div');
      el.style.cssText = [
        'position:fixed',
        'top:1rem',
        'right:1rem',
        'z-index:9999',
        'display:flex',
        'flex-direction:column',
        'gap:0.625rem',
        'pointer-events:none'
      ].join(';');
      document.body.appendChild(el);
      this.container = el;
    }
    return this.container;
  }

  private show(config: ToastConfig): void {
    const container = this.getContainer();
    const host = document.createElement('div');
    host.style.cssText = 'pointer-events:none';
    container.appendChild(host);

    const ref = createComponent(UiToastComponent, {
      environmentInjector: this.injector,
      hostElement: host
    });

    ref.instance.variant = config.variant;
    ref.instance.message = config.message;
    if (config.title !== undefined) ref.instance.title = config.title;
    if (config.duration !== undefined) ref.instance.duration = config.duration;

    ref.instance.onClose = () => {
      this.appRef.detachView(ref.hostView);
      ref.destroy();
      host.remove();
    };

    this.appRef.attachView(ref.hostView);
    ref.changeDetectorRef.detectChanges();
  }

  success(message: string, title?: string, duration = 3500): void {
    this.show({ variant: 'success', message, title, duration });
  }

  error(message: string, title?: string, duration = 4500): void {
    this.show({ variant: 'error', message, title, duration });
  }

  warning(message: string, title?: string, duration = 4000): void {
    this.show({ variant: 'warning', message, title, duration });
  }

  info(message: string, title?: string, duration = 3500): void {
    this.show({ variant: 'info', message, title, duration });
  }
}
