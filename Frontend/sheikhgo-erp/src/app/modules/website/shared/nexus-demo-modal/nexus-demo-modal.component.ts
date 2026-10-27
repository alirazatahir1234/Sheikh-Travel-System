import {
  AfterViewInit,
  Component,
  ElementRef,
  HostListener,
  OnDestroy,
  ViewChild,
  inject,
  output,
} from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import {
  NEXUS_WALKTHROUGH_DEMO_SRC,
  NEXUS_WALKTHROUGH_DEMO_TITLE,
} from '../../core/demo-media';

@Component({
  selector: 'app-nexus-demo-modal',
  standalone: true,
  imports: [MatIconModule],
  templateUrl: './nexus-demo-modal.component.html',
  styleUrl: './nexus-demo-modal.component.scss',
})
export class NexusDemoModalComponent implements AfterViewInit, OnDestroy {
  readonly closed = output<void>();

  readonly videoSrc = NEXUS_WALKTHROUGH_DEMO_SRC;
  readonly videoTitle = NEXUS_WALKTHROUGH_DEMO_TITLE;

  @ViewChild('player') private player?: ElementRef<HTMLVideoElement>;

  private readonly doc = inject(DOCUMENT);
  private priorBodyOverflow = '';

  ngAfterViewInit(): void {
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

  onCanPlay(): void {
    void this.player?.nativeElement.play().catch(() => undefined);
  }

  private restoreScroll(): void {
    this.doc.body.style.overflow = this.priorBodyOverflow;
  }
}
