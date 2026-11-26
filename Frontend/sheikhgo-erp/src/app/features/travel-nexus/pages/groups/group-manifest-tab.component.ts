import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import { TravelGroup, TravelGroupManifest } from '../../models/travel.models';
import { asUtc } from '../../models/travel-date.util';
import { TravelGroupService } from '../../services/travel-group.service';
import { MANIFEST_PILGRIM_HEADERS, downloadManifestPdf, downloadManifestXlsx, manifestPilgrimRows } from './manifest-export';

/**
 * Manifest preview + PDF / Excel. Nothing is fetched until the user asks: each generation is audited server-side
 * (it contains passport data) and adds a group history row.
 */
@Component({
  selector: 'app-group-manifest-tab',
  standalone: true,
  imports: [DatePipe, MatButtonModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tn-card">
      <div class="flex flex-wrap items-center justify-between gap-3 border-b border-fleet-border px-5 py-4">
        <div><h3 class="tn-section-title">Manifest</h3>
          <p class="text-xs text-fleet-text-muted">Generating the manifest is recorded in the audit log. Passport numbers are masked unless you may view identity data.</p></div>
        <div class="flex gap-2">
          <button mat-flat-button color="primary" [disabled]="busy()" (click)="generate()"><mat-icon>refresh</mat-icon> {{ manifest() ? 'Regenerate' : 'Generate manifest' }}</button>
          @if (manifest(); as m) {
            <button mat-stroked-button (click)="pdf(m)"><mat-icon>picture_as_pdf</mat-icon> PDF</button>
            <button mat-stroked-button (click)="xlsx(m)"><mat-icon>grid_on</mat-icon> Excel</button>
          }
        </div>
      </div>
      @if (error()) { <div class="tn-card-body"><div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ error() }}</div></div> }
      @if (manifest(); as m) {
        <div class="px-5 py-3 text-xs text-fleet-text-muted">Generated {{ utc(m.generatedAtUtc) | date: 'medium' }} · {{ m.pilgrims.length }} pilgrim(s)
          @if (m.piiMasked) { · <strong>passport numbers masked</strong> }</div>
        <div class="overflow-x-auto">
          <table class="w-full min-w-[1100px] text-left text-xs">
            <thead class="bg-fleet-surface-alt/60"><tr class="font-bold uppercase tracking-wider text-fleet-text-muted">
              @for (h of headers; track h) { <th class="px-3 py-2">{{ h }}</th> }
            </tr></thead>
            <tbody class="divide-y divide-fleet-border">
              @for (r of rows(); track $index) { <tr>@for (c of r; track $index) { <td class="px-3 py-2">{{ c }}</td> }</tr> }
            </tbody>
          </table>
        </div>
      } @else if (!busy()) {
        <p class="px-5 py-10 text-center text-sm text-fleet-text-muted">Generate the manifest of {{ group().confirmedMembers }} confirmed pilgrim(s) to preview and download it.</p>
      }
    </div>
  `,
  styleUrl: '../../travel-nexus.scss'
})
export class GroupManifestTabComponent {
  private readonly api = inject(TravelGroupService);
  private readonly toast = inject(UiToastService);
  private readonly destroyRef = inject(DestroyRef);

  readonly group = input.required<TravelGroup>();
  readonly generated = output<void>();

  protected readonly headers = MANIFEST_PILGRIM_HEADERS;
  protected readonly utc = asUtc;
  protected readonly manifest = signal<TravelGroupManifest | null>(null);
  protected readonly rows = signal<(string | number)[][]>([]);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected generate(): void {
    this.busy.set(true);
    this.error.set(null);
    this.api.getManifest(this.group().id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: m => { this.busy.set(false); this.manifest.set(m); this.rows.set(manifestPilgrimRows(m)); this.generated.emit(); },
      error: err => { this.busy.set(false); this.error.set(apiErrorMessage(err, 'Could not generate the manifest.')); }
    });
  }

  protected pdf(m: TravelGroupManifest): void {
    try { downloadManifestPdf(m); } catch { this.toast.error('Could not build the PDF.'); }
  }
  protected xlsx(m: TravelGroupManifest): void {
    try { downloadManifestXlsx(m); } catch { this.toast.error('Could not build the Excel file.'); }
  }
}
