import {
  ApplicationRef,
  createComponent,
  EnvironmentInjector,
  inject,
  Injectable
} from '@angular/core';
import { UiConfirmDialogComponent } from './ui-confirm-dialog.component';
import { UiConfirmConfig } from '../types/ui.types';

@Injectable({ providedIn: 'root' })
export class UiConfirmService {
  private readonly appRef = inject(ApplicationRef);
  private readonly injector = inject(EnvironmentInjector);

  /** Opens a confirmation dialog and resolves to true when confirmed. */
  open(config: UiConfirmConfig): Promise<boolean> {
    return new Promise<boolean>((resolve) => {
      const host = document.createElement('div');
      document.body.appendChild(host);

      const ref = createComponent(UiConfirmDialogComponent, {
        environmentInjector: this.injector,
        hostElement: host
      });

      this.appRef.attachView(ref.hostView);

      let settled = false;
      const finish = (result: boolean) => {
        if (settled) {
          return;
        }
        settled = true;
        sub.unsubscribe();
        this.appRef.detachView(ref.hostView);
        ref.destroy();
        host.remove();
        resolve(result);
      };

      const sub = ref.instance.result.subscribe(finish);
      ref.instance.show(config);
    });
  }

  confirmDelete(message: string, title = 'Delete confirmation'): Promise<boolean> {
    return this.open({ variant: 'delete', title, message, confirmText: 'Delete' });
  }
}
