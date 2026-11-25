import { Directive, TemplateRef, ViewContainerRef, effect, inject, input } from '@angular/core';
import { AuthService } from '../../core/services/auth.service';

/**
 * Structural directive: renders its template only when the current user holds ANY of the given permissions.
 * Usage: `<button *appHasPermission="'Travel.Customer.Manage'">…</button>` or an array of codes.
 * This hides UI only — the API enforces the same permission.
 */
@Directive({
  selector: '[appHasPermission]',
  standalone: true
})
export class HasPermissionDirective {
  private readonly auth = inject(AuthService);
  private readonly tpl = inject(TemplateRef<unknown>);
  private readonly vcr = inject(ViewContainerRef);

  readonly appHasPermission = input.required<string | string[]>();

  private rendered = false;

  constructor() {
    effect(() => {
      const value = this.appHasPermission();
      const codes = Array.isArray(value) ? value : [value];
      const allowed = codes.length === 0 || this.auth.hasAnyPermission(codes);
      if (allowed && !this.rendered) {
        this.vcr.createEmbeddedView(this.tpl);
        this.rendered = true;
      } else if (!allowed && this.rendered) {
        this.vcr.clear();
        this.rendered = false;
      }
    });
  }
}
