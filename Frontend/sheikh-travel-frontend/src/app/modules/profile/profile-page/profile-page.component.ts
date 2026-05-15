import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { AuthService } from '../../../core/services/auth.service';
import { environment } from '../../../../environments/environment';
import { OcrSettingsService } from '../../../core/services/ocr-settings.service';
import { OcrMode } from '../../../core/models/ocr.model';

interface ProfileUser {
  fullName: string;
  roles?: string[];
  email?: string;
  phoneNumber?: string;
}

@Component({
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
    private snackBar: MatSnackBar
  ) {
    this.profileForm = this.fb.group({
      fullName: ['', [Validators.required, Validators.maxLength(200)]],
      phoneNumber: ['', Validators.maxLength(30)],
      email: [{ value: '', disabled: true }]
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
    const { fullName, phoneNumber } = this.profileForm.value;

    this.http.put(`${environment.apiUrl}/users/profile`, {
      userId: 0, // Backend uses token
      fullName,
      phoneNumber: phoneNumber || null
    }).subscribe({
      next: () => {
        this.savingProfile = false;
        this.snackBar.open('Profile updated successfully.', 'Close', { duration: 2000 });
        
        // Update local user and persist to session
        if (this.currentUser) {
          this.currentUser.fullName = fullName;
          this.currentUser.phoneNumber = phoneNumber;
        }
        
        // Update the auth session so the header refreshes
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
        this.snackBar.open(this.extractError(err), 'Close', { duration: 4000 });
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
        this.snackBar.open('Password changed successfully.', 'Close', { duration: 2000 });
        this.passwordForm.reset();
      },
      error: (err: HttpErrorResponse) => {
        this.changingPassword = false;
        this.snackBar.open(this.extractError(err), 'Close', { duration: 4000 });
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
      this.snackBar.open('OCR settings saved.', 'Close', { duration: 2200 });
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
