import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { DOCUMENT_UPLOAD_ACCEPT, DOCUMENT_UPLOAD_MAX_BYTES } from '../models/travel.models';

/** Client-side pre-check (the server sniffs the bytes and decides). Returns an error message or null. */
export function precheckDocumentFile(file: File, accept = DOCUMENT_UPLOAD_ACCEPT, maxBytes = DOCUMENT_UPLOAD_MAX_BYTES): string | null {
  const ext = file.name.includes('.') ? file.name.substring(file.name.lastIndexOf('.')).toLowerCase() : '';
  if (!accept.split(',').includes(ext)) return `Only ${accept.replaceAll(',', ', ')} files are accepted.`;
  if (file.size === 0) return 'The file is empty.';
  if (file.size > maxBytes) return `The file is larger than ${Math.round(maxBytes / 1024 / 1024)} MB.`;
  return null;
}

/** Drag-and-drop / click-to-choose zone. Emits a pre-checked file; shows upload progress when given. */
@Component({
  selector: 'app-document-upload',
  standalone: true,
  imports: [MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <label class="tn-drop" [class.dragging]="dragging()" [class.disabled]="disabled()"
      (dragover)="onDragOver($event)" (dragleave)="dragging.set(false)" (drop)="onDrop($event)">
      <input type="file" class="sr-only" [accept]="accept()" [disabled]="disabled()" (change)="onPick($event)" />
      @if (progress() !== null) {
        <span class="text-xs font-semibold">Uploading… {{ progress() }}%</span>
        <span class="tn-progress"><span [style.width.%]="progress()"></span></span>
      } @else {
        <mat-icon class="!text-[20px]">upload_file</mat-icon>
        <span class="text-xs">{{ label() }}</span>
      }
    </label>
    @if (error()) { <span class="tn-error text-xs">{{ error() }}</span> }
  `,
  styles: [`
    :host { display: block; }
    .tn-drop {
      display: flex; align-items: center; justify-content: center; gap: 0.4rem; flex-direction: column;
      border: 1px dashed var(--stb-border-strong); border-radius: var(--stb-radius-sm); padding: 0.6rem 0.8rem;
      color: var(--stb-text-muted); cursor: pointer; background: var(--stb-surface-alt); min-width: 160px;
    }
    .tn-drop.dragging { border-color: var(--stb-primary); background: var(--stb-primary-50); color: var(--stb-primary-600); }
    .tn-drop.disabled { cursor: not-allowed; opacity: 0.6; }
    .tn-progress { display: block; width: 100%; height: 4px; background: var(--stb-border); border-radius: 999px; overflow: hidden; }
    .tn-progress span { display: block; height: 100%; background: var(--stb-primary); }
    .tn-error { color: var(--stb-danger); }
  `]
})
export class DocumentUploadComponent {
  readonly accept = input(DOCUMENT_UPLOAD_ACCEPT);
  readonly maxBytes = input(DOCUMENT_UPLOAD_MAX_BYTES);
  readonly label = input('Drop a PDF/JPG/PNG or click');
  readonly disabled = input(false);
  /** 0–100 while uploading, null otherwise. */
  readonly progress = input<number | null>(null);
  readonly fileSelected = output<File>();

  protected readonly dragging = signal(false);
  protected readonly error = signal<string | null>(null);

  protected onDragOver(e: DragEvent): void {
    e.preventDefault();
    if (!this.disabled()) this.dragging.set(true);
  }

  protected onDrop(e: DragEvent): void {
    e.preventDefault();
    this.dragging.set(false);
    const file = e.dataTransfer?.files?.[0];
    if (file && !this.disabled()) this.emit(file);
  }

  protected onPick(e: Event): void {
    const input = e.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (file) this.emit(file);
  }

  private emit(file: File): void {
    const problem = precheckDocumentFile(file, this.accept(), this.maxBytes());
    this.error.set(problem);
    if (!problem) this.fileSelected.emit(file);
  }
}
