import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { AuthService } from '../../../core/services/auth.service';
import { environment } from '../../../../environments/environment';
import { OcrSettingsService } from '../../../core/services/ocr-settings.service';
import { OcrMode } from '../../../core/models/ocr.model';
import { LookupService, TimezoneOption } from '../../../core/services/lookup.service';
import { LocalTimeContextService } from '../../../core/services/local-time-context.service';

interface ProfileUser {
  fullName: string;
  roles?: string[];
  email?: string;
  phoneNumber?: string;
  timeZone?: string | null;
}

@Component({
  standalone: false,
  selector: 'app-profile-page',
  templateUrl: './profile-page.component.html',
  styleUrls: ['./profile-page.component.scss']
})
export class ProfilePageComponent implements OnInit {
  profileForm: FormGroup;
  passwordForm: FormGroup;

  selectedTab = 0;
  savingProfile = false;
  changingPassword = false;
  savingOcrSettings = false;
  hideCurrentPassword = true;
  hideNewPassword = true;
  hideConfirmPassword = true;

  currentUser: ProfileUser | null = null;
  timezones: TimezoneOption[] = [];
  ocrSettingsForm: FormGroup;
  readonly ocrModes: Array<{ value: OcrMode; label: string }> = [
    { value: 'HYBRID', label: 'Hybrid Mode (Recommended)' },
    { value: 'PADDLE_ONLY', label: 'PaddleOCR Only' },
    { value: 'AZURE_ONLY', label: 'Azure Only' }
  ];

  constructor(
    private fb: FormBuilder,
    private route: ActivatedRoute,
    private http: HttpClient,
    private auth: AuthService,
    private ocrSettingsService: OcrSettingsService,
    private lookup: LookupService,
    private localTime: LocalTimeContextService,
    private toast: UiToastService) {
    this.profileForm = this.fb.group({
      fullName: ['', [Validators.required, Validators.maxLength(200)]],
      phoneNumber: ['', Validators.maxLength(30)],
      email: [{ value: '', disabled: true }],
      timeZone: ['']
    });

    this.passwordForm = this.fb.group({
      currentPassword: ['Admin@123', Validators.required],
      newPassword: ['', [Validators.required, Validators.minLength(6)]],
      confirmPassword: ['', Validators.required]
    }, { validators: this.passwordMatchValidator });

    this.ocrSettingsForm = this.fb.group({
      mode: ['HYBRID', Validators.required],
      confidenceThreshold: [70, [Validators.required, Validators.min(1), Validators.max(100)]],
      enableFallback: [true],
      saveRawOcr: [true],
      azureEnabled: [true],
      paddleEnabled: [true],
      azureTimeoutSeconds: [5, [Validators.required, Validators.min(1), Validators.max(30)]]
    });
  }

  ngOnInit(): void {
    this.route.queryParams.subscribe(params => {
      if (params['tab'] === 'settings' || params['tab'] === 'security') {
        this.selectedTab = 1;
      } else if (params['tab'] === 'ocr') {
        this.selectedTab = 2;
      }
    });

    const settings = this.ocrSettingsService.getSettings();
    this.ocrSettingsForm.patchValue({
      mode: settings.mode,
      confidenceThreshold: settings.confidenceThreshold,
      enableFallback: settings.enableFallback,
      saveRawOcr: settings.saveRawOcr,
      azureEnabled: settings.azureEnabled,
      paddleEnabled: settings.paddleEnabled,
      azureTimeoutSeconds: 5
    });

    this.lookup.getTimezones().subscribe(list => {
      this.timezones = list ?? [];
    });

    this.auth.currentUser$.subscribe(user => {
      if (user) {
        this.currentUser = {
          fullName: user.fullName,
          roles: user.roles,
          email: user.email,
          phoneNumber: (user as ProfileUser).phoneNumber
        };
        this.profileForm.patchValue({
          fullName: user.fullName,
          phoneNumber: (user as ProfileUser).phoneNumber || '',
          email: user.email || ''
        });
      }
    });

    this.http.get<{
      fullName?: string;
      email?: string;
      phone?: string;
      timeZone?: string | null;
    }>(`${environment.apiUrl}/users/profile`).subscribe({
      next: profile => {
        if (!profile) return;
        this.profileForm.patchValue({
          fullName: profile.fullName || this.profileForm.value.fullName,
          phoneNumber: profile.phone || '',
          email: profile.email || this.profileForm.getRawValue().email,
          timeZone: profile.timeZone || ''
        });
        this.localTime.setUserTimeZone(profile.timeZone);
      },
      error: () => { /* keep auth session values */ }
    });
  }

  passwordMatchValidator(group: FormGroup): { [key: string]: boolean } | null {
    const newPassword = group.get('newPassword')?.value;
    const confirmPassword = group.get('confirmPassword')?.value;
    return newPassword === confirmPassword ? null : { passwordMismatch: true };
  }

  saveProfile(): void {
    if (this.profileForm.invalid) {
      this.profileForm.markAllAsTouched();
      return;
    }

    this.savingProfile = true;
    const { fullName, phoneNumber, timeZone } = this.profileForm.getRawValue();

    this.http.put(`${environment.apiUrl}/users/profile`, {
      userId: 0, // Backend uses token
      fullName,
      phoneNumber: phoneNumber || null,
      timeZone: timeZone || null
    }).subscribe({
      next: () => {
        this.savingProfile = false;
        this.toast.success('Profile updated successfully.');
        this.localTime.setUserTimeZone(timeZone);

        if (this.currentUser) {
          this.currentUser.fullName = fullName;
          this.currentUser.phoneNumber = phoneNumber;
          this.currentUser.timeZone = timeZone || null;
        }

        const current = this.auth.getCurrentUser();
        if (current) {
          const updated = {
            ...current,
            fullName,
            phoneNumber: phoneNumber || undefined
          };
          this.auth.persistSession(updated);
        }
      },
      error: (err: HttpErrorResponse) => {
        this.savingProfile = false;
        this.toast.error(this.extractError(err));
      }
    });
  }

  changePassword(): void {
    if (this.passwordForm.invalid) {
      this.passwordForm.markAllAsTouched();
      return;
    }

    this.changingPassword = true;
    const { currentPassword, newPassword } = this.passwordForm.value;

    this.http.put(`${environment.apiUrl}/users/change-password`, {
      userId: 0, // Backend uses token
      currentPassword,
      newPassword
    }).subscribe({
      next: () => {
        this.changingPassword = false;
        this.toast.success('Password changed successfully.');
        this.passwordForm.reset();
      },
      error: (err: HttpErrorResponse) => {
        this.changingPassword = false;
        this.toast.error(this.extractError(err));
      }
    });
  }

  saveOcrSettings(): void {
    if (this.ocrSettingsForm.invalid) {
      this.ocrSettingsForm.markAllAsTouched();
      return;
    }

    this.savingOcrSettings = true;
    const value = this.ocrSettingsForm.getRawValue();
    this.ocrSettingsService.saveSettings({
      mode: value.mode,
      confidenceThreshold: Number(value.confidenceThreshold),
      enableFallback: !!value.enableFallback,
      saveRawOcr: !!value.saveRawOcr,
      azureEnabled: !!value.azureEnabled,
      paddleEnabled: !!value.paddleEnabled
    });
    setTimeout(() => {
      this.savingOcrSettings = false;
      this.toast.success('OCR settings saved.');
    }, 250);
  }

  private extractError(err: HttpErrorResponse): string {
    const body: unknown = err?.error;
    if (body && typeof body === 'object') {
      const obj = body as Record<string, unknown>;
      if (obj['errors']) {
        const flat = Object.values(obj['errors'] as Record<string, string[]>).flat();
        if (flat.length) return String(flat[0]);
      }
      if (obj['message']) return String(obj['message']);
    }
    return `Operation failed (${err?.status || 'network'}).`;
  }

  initials(name?: string | null): string {
    if (!name) return '?';
    return name.split(' ').map(p => p[0]).slice(0, 2).join('').toUpperCase();
  }
}
