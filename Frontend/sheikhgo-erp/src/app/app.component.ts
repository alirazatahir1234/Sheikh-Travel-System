import { Component, OnDestroy, OnInit } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { Subject, filter, take, takeUntil, timer } from 'rxjs';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent implements OnInit, OnDestroy {
  splashVisible = true;
  splashHiding = false;

  private readonly destroy$ = new Subject<void>();
  private readonly minSplashMs = 1800;
  private readonly maxSplashMs = 4500;
  private readonly exitFadeMs = 500;
  private splashStartedAt = Date.now();
  private hideScheduled = false;
  private navigationReady = false;

  constructor(private readonly router: Router) {}

  ngOnInit(): void {
    document.getElementById('app-initial-splash')?.remove();
    this.splashStartedAt = Date.now();

    if (this.router.navigated) {
      this.navigationReady = true;
      this.scheduleSplashHide();
    }

    this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd),
      take(1),
      takeUntil(this.destroy$)
    ).subscribe(() => {
      this.navigationReady = true;
      this.scheduleSplashHide();
    });

    timer(this.maxSplashMs).pipe(takeUntil(this.destroy$)).subscribe(() => {
      this.navigationReady = true;
      this.scheduleSplashHide();
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private scheduleSplashHide(): void {
    if (this.hideScheduled) {
      return;
    }

    if (!this.navigationReady) {
      return;
    }

    const elapsed = Date.now() - this.splashStartedAt;
    const remaining = Math.max(0, this.minSplashMs - elapsed);

    this.hideScheduled = true;
    timer(remaining).pipe(takeUntil(this.destroy$)).subscribe(() => this.beginSplashExit());
  }

  private beginSplashExit(): void {
    if (!this.splashVisible) {
      return;
    }

    this.splashHiding = true;
    timer(this.exitFadeMs).pipe(takeUntil(this.destroy$)).subscribe(() => {
      this.splashVisible = false;
    });
  }
}
