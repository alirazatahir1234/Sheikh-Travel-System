import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { Subject, of } from 'rxjs';
import { catchError, finalize, switchMap, takeUntil, timeout } from 'rxjs/operators';
import { SettingsService } from '../services/settings.service';
import { SETTINGS_CATEGORIES_FALLBACK } from '../config/settings-categories';
import { SETTINGS_SCHEMAS } from '../config/settings-schemas';
import { SettingFieldSchema, SettingsCategory, SettingsValues } from '../models/settings.model';
import { SettingsFormStatus } from '../components/dynamic-settings-form/dynamic-settings-form.component';

@Component({
  standalone: false,
  selector: 'app-settings-page',
  templateUrl: './settings-page.component.html',
  styleUrls: ['./settings-page.component.scss']
})
export class SettingsPageComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly settings = inject(SettingsService);
  private readonly destroy$ = new Subject<void>();

  private apiCategories: SettingsCategory[] = [...SETTINGS_CATEGORIES_FALLBACK];

  category: SettingsCategory | null = null;
  schema: SettingFieldSchema[] = [];
  values: SettingsValues = {};
  loading = true;
  loadError: string | null = null;
  formStatus: SettingsFormStatus = { invalid: false, pristine: true, saving: false };

  onFormStatusChange(status: SettingsFormStatus): void {
    this.formStatus = status;
  }

  ngOnInit(): void {
    // Keep category metadata fresh, but never block the page on this call.
    this.settings.getCategories().pipe(takeUntil(this.destroy$)).subscribe({
      next: cats => {
        this.apiCategories = cats?.length ? cats : [...SETTINGS_CATEGORIES_FALLBACK];
        const currentId = this.route.snapshot.paramMap.get('category');
        if (currentId) {
          this.resolveCategory(currentId);
        }
      },
      error: () => {
        this.apiCategories = [...SETTINGS_CATEGORIES_FALLBACK];
      }
    });

    this.route.paramMap.pipe(
      takeUntil(this.destroy$),
      switchMap(params => {
        const id = params.get('category') ?? 'General';
        this.resolveCategory(id);
        this.loading = true;
        this.loadError = null;
        this.values = {};

        if (!this.category) {
          this.loading = false;
          this.loadError = `Unknown settings category "${id}".`;
          return of({} as SettingsValues);
        }

        if (!this.category.isImplemented) {
          this.loading = false;
          return of({} as SettingsValues);
        }

        return this.settings.getValues(this.category.id).pipe(
          timeout(8000),
          catchError(() => {
            this.loadError = 'Could not load saved values. Showing defaults — you can still edit and save.';
            return of({} as SettingsValues);
          }),
          finalize(() => {
            this.loading = false;
          })
        );
      })
    ).subscribe({
      next: values => {
        this.values = values ?? {};
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.loadError = 'Failed to open this settings page. Try another category or refresh.';
      }
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private resolveCategory(routeId: string): void {
    const needle = routeId.trim().toLowerCase();
    const pool = [
      ...this.apiCategories,
      ...SETTINGS_CATEGORIES_FALLBACK
    ];

    const match =
      pool.find(c => c.id.toLowerCase() === needle) ??
      pool.find(c => c.label.toLowerCase() === needle) ??
      null;

    this.category = match
      ? {
          ...match,
          // Prefer canonical schema key casing when available
          id: this.canonicalCategoryId(match.id)
        }
      : null;

    this.schema = this.category ? this.resolveSchema(this.category.id) : [];
  }

  private canonicalCategoryId(id: string): string {
    const key = Object.keys(SETTINGS_SCHEMAS).find(k => k.toLowerCase() === id.toLowerCase());
    return key ?? id;
  }

  private resolveSchema(categoryId: string): SettingFieldSchema[] {
    const key = Object.keys(SETTINGS_SCHEMAS).find(k => k.toLowerCase() === categoryId.toLowerCase());
    return key ? SETTINGS_SCHEMAS[key] : [];
  }
}
