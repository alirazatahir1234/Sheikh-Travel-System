import {
  AfterViewInit,
  Component,
  ElementRef,
  HostListener,
  OnDestroy,
  ViewChild,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import {
  DEMO_VIDEO_DOWNLOAD_ALLOWLIST,
  NEXUS_ERP_WALKTHROUGH_DEMO_SRC,
  NEXUS_ERP_WALKTHROUGH_DEMO_STATIC,
  NEXUS_ERP_WALKTHROUGH_DEMO_STREAM,
  NEXUS_ERP_WALKTHROUGH_DEMO_TITLE,
  NEXUS_PROMO_120S_CAPTIONS_VTT,
  NEXUS_PROMO_120S_SRC,
  NEXUS_PROMO_120S_TITLE,
  NEXUS_WEBSITE_WALKTHROUGH_DEMO_SRC,
  NexusDemoModalMode,
} from '../../core/demo-media';
import { AuthService } from '../../../../core/services/auth.service';
import { environment } from '../../../../../environments/environment';
import { WebsiteTranslatePipe } from '../website-translate.pipe';

@Component({
  selector: 'app-nexus-demo-modal',
  standalone: true,
  imports: [MatIconModule, RouterLink, WebsiteTranslatePipe],
  templateUrl: './nexus-demo-modal.component.html',
  styleUrl: './nexus-demo-modal.component.scss',
})
export class NexusDemoModalComponent implements AfterViewInit, OnDestroy {
  readonly closed = output<void>();

  /** Initial video mode — ERP walkthrough (default) or public 120s promo. */
  readonly initialMode = input<NexusDemoModalMode>('erp');

  readonly mode = signal<NexusDemoModalMode>('erp');

  readonly erpSrc = NEXUS_ERP_WALKTHROUGH_DEMO_SRC;
  readonly erpTitle = NEXUS_ERP_WALKTHROUGH_DEMO_TITLE;
  readonly promoSrc = NEXUS_PROMO_120S_SRC;
  readonly promoTitle = NEXUS_PROMO_120S_TITLE;
  readonly promoCaptionsSrc = NEXUS_PROMO_120S_CAPTIONS_VTT;
  readonly websiteTourSrc = NEXUS_WEBSITE_WALKTHROUGH_DEMO_SRC;

  @ViewChild('player') private player?: ElementRef<HTMLVideoElement>;

  private readonly doc = inject(DOCUMENT);
  private readonly auth = inject(AuthService);
  private priorBodyOverflow = '';
  downloadInProgress = false;

  ngAfterViewInit(): void {
    this.mode.set(this.initialMode());
    this.priorBodyOverflow = this.doc.body.style.overflow;
    this.doc.body.style.overflow = 'hidden';
  }

  ngOnDestroy(): void {
    this.restoreScroll();
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.close();
  }

  close(): void {
    this.player?.nativeElement.pause();
    this.restoreScroll();
    this.closed.emit();
  }

  onBackdropClick(event: MouseEvent): void {
    if (event.target === event.currentTarget) {
      this.close();
    }
  }

  setMode(next: NexusDemoModalMode): void {
    if (this.mode() === next) return;
    const el = this.player?.nativeElement;
    el?.pause();
    this.mode.set(next);
    // Force reload when switching between stream vs static sources.
    queueMicrotask(() => el?.load());
  }

  onCanPlay(): void {
    void this.player?.nativeElement.play().catch(() => undefined);
  }

  get canDownloadDemo(): boolean {
    if (this.mode() !== 'erp') return false;
    const user = this.auth.getCurrentUser();
    if (!user?.email) return false;
    const email = user.email.trim().toLowerCase();
    return (
      DEMO_VIDEO_DOWNLOAD_ALLOWLIST.some((allowed) => allowed === email) ||
      this.auth.hasRole('SUPER_ADMIN') ||
      this.auth.hasRole('SuperAdmin')
    );
  }

  async downloadDemoForOwner(): Promise<void> {
    if (!this.canDownloadDemo || this.downloadInProgress) return;
    const token = this.auth.getToken();
    if (!token) return;

    this.downloadInProgress = true;
    try {
      if (!environment.production) {
        const anchor = this.doc.createElement('a');
        anchor.href = NEXUS_ERP_WALKTHROUGH_DEMO_STATIC;
        anchor.download = 'sheikhgo-nexus-erp-walkthrough.mp4';
        anchor.rel = 'noopener';
        anchor.click();
        return;
      }

      const url = `${NEXUS_ERP_WALKTHROUGH_DEMO_STREAM}?download=1`;
      const response = await fetch(url, {
        headers: { Authorization: `Bearer ${token}` },
      });
      if (!response.ok) return;

      const blob = await response.blob();
      const objectUrl = URL.createObjectURL(blob);
      const anchor = this.doc.createElement('a');
      anchor.href = objectUrl;
      anchor.download = 'sheikhgo-nexus-erp-walkthrough.mp4';
      anchor.rel = 'noopener';
      anchor.click();
      URL.revokeObjectURL(objectUrl);
    } finally {
      this.downloadInProgress = false;
    }
  }

  private restoreScroll(): void {
    this.doc.body.style.overflow = this.priorBodyOverflow;
  }
}
