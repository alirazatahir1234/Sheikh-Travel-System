import { Component, OnInit, inject } from '@angular/core';
import { NgIf } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { TripFeedbackForm, TripFeedbackService } from '../../../../core/services/trip-feedback.service';
import { WebsiteTranslatePipe } from '../../shared/website-translate.pipe';
import { WebsiteSeoService } from '../../core/seo.service';
import { WebsiteI18nService } from '../../core/website-i18n.service';

@Component({
  selector: 'app-trip-feedback-page',
  standalone: true,
  imports: [NgIf, FormsModule, MatButtonModule, MatIconModule, WebsiteTranslatePipe],
  template: `
    <section class="rate-shell">
      <div class="rate-card" *ngIf="loading">
        <p>{{ 'pages.tripFeedback.loading' | wsTranslate }}</p>
      </div>
      <div class="rate-card" *ngIf="!loading && error">
        <mat-icon aria-hidden="true">error_outline</mat-icon>
        <p>{{ error }}</p>
      </div>
      <div class="rate-card" *ngIf="!loading && submitted">
        <mat-icon aria-hidden="true">check_circle</mat-icon>
        <h1>{{ 'pages.tripFeedback.thanksTitle' | wsTranslate }}</h1>
        <p>{{ 'pages.tripFeedback.thanksLead' | wsTranslate }}</p>
      </div>
      <div class="rate-card" *ngIf="!loading && form && !submitted && !form.alreadySubmitted">
        <span class="kicker">{{ 'pages.tripFeedback.kicker' | wsTranslate }}</span>
        <h1>{{ titleLine }}</h1>
        <p *ngIf="form.driverFirstName">{{ driverLine }}</p>
        <p class="hint">{{ 'pages.tripFeedback.hint' | wsTranslate }}</p>

        @for (dim of dimensions; track dim.key) {
          <div class="dim">
            <label>{{ dim.labelKey | wsTranslate }}</label>
            <div class="stars" role="group" [attr.aria-label]="dim.labelKey | wsTranslate">
              @for (s of starRange; track s) {
                <button type="button" class="star" [class.on]="scores[dim.key] >= s" (click)="scores[dim.key] = s">
                  <mat-icon>{{ scores[dim.key] >= s ? 'star' : 'star_border' }}</mat-icon>
                </button>
              }
            </div>
          </div>
        }

        <label class="comment-label">{{ 'pages.tripFeedback.comment' | wsTranslate }}</label>
        <textarea [(ngModel)]="comment" rows="3" maxlength="500" [placeholder]="'pages.tripFeedback.commentPh' | wsTranslate"></textarea>

        <button mat-flat-button color="primary" class="submit" [disabled]="!canSubmit || submitting" (click)="submit()">
          {{ 'pages.tripFeedback.submit' | wsTranslate }}
        </button>
      </div>
      <div class="rate-card" *ngIf="!loading && form?.alreadySubmitted && !submitted">
        <mat-icon aria-hidden="true">info</mat-icon>
        <p>{{ 'pages.tripFeedback.already' | wsTranslate }}</p>
      </div>
    </section>
  `,
  styles: [`
    .rate-shell { min-height: 70vh; display: flex; align-items: center; justify-content: center; padding: 2rem 1rem; background: #f8fafc; }
    .rate-card { max-width: 480px; width: 100%; background: #fff; border-radius: 16px; padding: 1.75rem; box-shadow: 0 8px 30px rgba(15,23,42,.08); }
    .kicker { font-size: .75rem; text-transform: uppercase; letter-spacing: .08em; color: #0f766e; font-weight: 600; }
    h1 { font-size: 1.35rem; margin: .5rem 0; }
    .hint, .comment-label { color: #64748b; font-size: .9rem; }
    .dim { margin: 1rem 0; }
    .dim label { display: block; font-weight: 600; margin-bottom: .35rem; }
    .stars { display: flex; gap: .15rem; }
    .star { border: none; background: none; cursor: pointer; padding: 0; color: #f59e0b; }
    textarea { width: 100%; margin: .5rem 0 1rem; border-radius: 8px; border: 1px solid #e2e8f0; padding: .5rem; font: inherit; }
    .submit { width: 100%; }
    mat-icon { vertical-align: middle; }
  `],
})
export class TripFeedbackPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly feedback = inject(TripFeedbackService);
  private readonly seo = inject(WebsiteSeoService);
  private readonly i18n = inject(WebsiteI18nService);

  loading = true;
  submitting = false;
  submitted = false;
  error: string | null = null;
  form: TripFeedbackForm | null = null;
  comment = '';
  token = '';

  readonly starRange = [1, 2, 3, 4, 5];
  scores = { politeness: 0, onTimeDelivery: 0, vehicleCondition: 0 };
  readonly dimensions = [
    { key: 'politeness' as const, labelKey: 'pages.tripFeedback.politeness' },
    { key: 'onTimeDelivery' as const, labelKey: 'pages.tripFeedback.onTime' },
    { key: 'vehicleCondition' as const, labelKey: 'pages.tripFeedback.vehicle' },
  ];

  ngOnInit(): void {
    this.seo.setPage({ title: 'Rate your trip', description: 'Post-trip driver feedback', path: '/rate' });
    this.token = this.route.snapshot.paramMap.get('token') ?? '';
    if (!this.token) {
      this.error = 'Invalid link.';
      this.loading = false;
      return;
    }
    this.feedback.getPublicForm(this.token).subscribe({
      next: f => {
        this.form = f;
        this.loading = false;
      },
      error: () => {
        this.error = 'This feedback link is invalid or expired.';
        this.loading = false;
      },
    });
  }

  get titleLine(): string {
    const base = this.i18n.t('pages.tripFeedback.title', 'Rate your trip');
    return this.form?.tripNumber ? `${base} (${this.form.tripNumber})` : base;
  }

  get driverLine(): string {
    const name = this.form?.driverFirstName ?? '';
    return this.i18n.t('pages.tripFeedback.driverLine', 'Your driver').replace('{{name}}', name);
  }

  get canSubmit(): boolean {
    return this.scores.politeness > 0 && this.scores.onTimeDelivery > 0 && this.scores.vehicleCondition > 0;
  }

  submit(): void {
    if (!this.canSubmit || this.submitting) return;
    this.submitting = true;
    this.feedback
      .submitPublic(this.token, {
        politeness: this.scores.politeness,
        onTimeDelivery: this.scores.onTimeDelivery,
        vehicleCondition: this.scores.vehicleCondition,
        comment: this.comment.trim() || undefined,
      })
      .subscribe({
        next: () => {
          this.submitted = true;
          this.submitting = false;
        },
        error: () => {
          this.error = 'Could not submit feedback. The link may have expired.';
          this.submitting = false;
        },
      });
  }
}
