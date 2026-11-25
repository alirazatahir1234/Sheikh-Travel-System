import { ChangeDetectionStrategy, Component, DestroyRef, effect, inject, input, model, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { UiModalComponent } from '../../../shared/components/ui';
import { TravelDocumentFile } from '../models/travel.models';
import { TravelDocumentService } from '../services/travel-document.service';

/**
 * Shows a private document fetched as an authenticated blob. The object URL lives only while the viewer is open
 * and is revoked on close/destroy; nothing is cached or linked.
 */
@Component({
  selector: 'app-document-viewer',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, UiModalComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ui-modal [(open)]="open" [title]="file()?.originalFileName ?? 'Document'" size="lg" (closed)="release()">
      @if (error()) {
        <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ error() }}</div>
      } @else if (!url()) {
        <p class="text-sm text-fleet-text-muted">Loading…</p>
      } @else if (file()?.contentType === 'application/pdf') {
        <iframe class="h-[70vh] w-full rounded border border-fleet-border" [src]="safeUrl()" title="Document preview"></iframe>
      } @else {
        <img class="mx-auto max-h-[70vh] rounded" [src]="safeUrl()" [alt]="file()?.originalFileName" />
      }
      <div modal-footer class="tn-actions">
        <span class="mr-auto self-center text-xs text-fleet-text-muted">Access to this file is logged.</span>
        <button mat-flat-button color="primary" (click)="open.set(false)">Close</button>
      </div>
    </ui-modal>
  `,
  styleUrl: '../travel-nexus.scss'
})
export class DocumentViewerComponent {
  private readonly api = inject(TravelDocumentService);
  private readonly sanitizer = inject(DomSanitizer);

  readonly open = model(false);
  readonly file = input<TravelDocumentFile | null>(null);

  protected readonly url = signal<string | null>(null);
  protected readonly safeUrl = signal<SafeResourceUrl | null>(null);
  protected readonly error = signal<string | null>(null);

  constructor() {
    inject(DestroyRef).onDestroy(() => this.release());
    effect(() => {
      const f = this.file();
      if (!this.open() || !f) return;
      this.release();
      this.error.set(null);
      this.api.downloadFile(f.id).subscribe({
        next: blob => {
          const objectUrl = URL.createObjectURL(blob);
          this.url.set(objectUrl);
          this.safeUrl.set(this.sanitizer.bypassSecurityTrustResourceUrl(objectUrl));
        },
        error: () => this.error.set('You do not have access to this file, or it could not be loaded.')
      });
    });
  }

  release(): void {
    const current = this.url();
    if (current) URL.revokeObjectURL(current);
    this.url.set(null);
    this.safeUrl.set(null);
  }
}
