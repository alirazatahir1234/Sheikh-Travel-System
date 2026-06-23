import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';

@Component({
  selector: 'app-driver-tracking-redirect',
  standalone: true,
  template: `<p class="redirect-msg">Opening live map…</p>`,
  styles: [`.redirect-msg { padding: 2rem; color: #64748b; }`],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DriverTrackingRedirectComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    void this.router.navigate(['/gps-tracking/live'], { queryParams: { driverId: id } });
  }
}
