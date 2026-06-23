import {
  ChangeDetectionStrategy,
  Component,
  computed,
  ElementRef,
  forwardRef,
  HostListener,
  inject,
  input,
  signal
} from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { UiSelectOption } from '../types/ui.types';

@Component({
  selector: 'ui-select',
  standalone: true,
  imports: [MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => UiSelectComponent),
      multi: true
    }
  ],
  template: `
    <label class="block">
      @if (label()) {
        <span class="mb-1.5 block text-[13px] font-semibold text-fleet-text">
          {{ label() }}
          @if (required()) { <span class="text-fleet-error">*</span> }
        </span>
      }

      <div class="relative">
        <button
          type="button"
          [disabled]="disabled()"
          class="flex w-full items-center justify-between gap-2 rounded-sm border bg-white px-3 py-2.5 text-left text-sm transition-colors disabled:cursor-not-allowed disabled:bg-fleet-surface-muted"
          [class.border-fleet-border]="!error()"
          [class.border-fleet-error]="!!error()"
          [class.border-fleet-primary]="open()"
          (click)="toggle()">
          <span class="truncate" [class.text-fleet-text-muted]="!hasSelection()">
            {{ displayLabel() }}
          </span>
          <mat-icon class="shrink-0 text-fleet-text-muted transition-transform" [class.rotate-180]="open()">expand_more</mat-icon>
        </button>

        @if (open()) {
          <div class="absolute z-30 mt-1 w-full overflow-hidden rounded-sm border border-fleet-border bg-white shadow-lg">
            @if (searchable()) {
              <div class="border-b border-fleet-border p-2">
                <input
                  type="search"
                  [placeholder]="searchPlaceholder()"
                  class="w-full rounded-sm border border-fleet-border px-2.5 py-1.5 text-sm focus:border-fleet-primary focus:outline-none"
                  [value]="query()"
                  (input)="onSearch($event)"
                  (click)="$event.stopPropagation()" />
              </div>
            }

            <ul class="max-h-60 overflow-y-auto py-1">
              @for (option of filteredOptions(); track option.value) {
                <li>
                  <button
                    type="button"
                    [disabled]="option.disabled"
                    class="flex w-full items-center justify-between gap-2 px-3 py-2 text-left text-sm hover:bg-fleet-surface-alt disabled:cursor-not-allowed disabled:opacity-50"
                    [class.text-fleet-primary]="isSelected(option.value)"
                    [class.font-semibold]="isSelected(option.value)"
                    (click)="selectOption(option)">
                    <span class="truncate">{{ option.label }}</span>
                    @if (isSelected(option.value)) { <mat-icon class="!text-[18px]">check</mat-icon> }
                  </button>
                </li>
              } @empty {
                <li class="px-3 py-4 text-center text-sm text-fleet-text-muted">No options found</li>
              }
            </ul>
          </div>
        }
      </div>

      @if (error()) {
        <span class="mt-1 block text-[12px] font-medium text-fleet-error">{{ error() }}</span>
      }
    </label>
  `,
  styles: [`
    :host { display: block; }
    mat-icon { display: inline-flex; align-items: center; justify-content: center; width: 20px; height: 20px; font-size: 20px; }
  `]
})
export class UiSelectComponent implements ControlValueAccessor {
  private readonly host = inject(ElementRef<HTMLElement>);

  readonly label = input<string>();
  readonly options = input<UiSelectOption[]>([]);
  readonly placeholder = input('Select...');
  readonly searchPlaceholder = input('Search...');
  readonly multiple = input(false);
  readonly searchable = input(false);
  readonly required = input(false);
  readonly error = input<string>();

  protected readonly open = signal(false);
  protected readonly query = signal('');
  protected readonly disabled = signal(false);
  protected readonly selected = signal<string[]>([]);

  private onChange: (value: string | string[] | null) => void = () => {};
  private onTouched: () => void = () => {};

  protected readonly filteredOptions = computed(() => {
    const q = this.query().toLowerCase().trim();
    if (!q) {
      return this.options();
    }
    return this.options().filter((o) => o.label.toLowerCase().includes(q));
  });

  protected readonly hasSelection = computed(() => this.selected().length > 0);

  protected readonly displayLabel = computed(() => {
    const values = this.selected();
    if (!values.length) {
      return this.placeholder();
    }
    const labels = this.options()
      .filter((o) => values.includes(o.value))
      .map((o) => o.label);
    if (this.multiple()) {
      return labels.length > 2 ? `${labels.slice(0, 2).join(', ')} +${labels.length - 2}` : labels.join(', ');
    }
    return labels[0] ?? this.placeholder();
  });

  toggle(): void {
    if (this.disabled()) {
      return;
    }
    this.open.update((v) => !v);
    if (!this.open()) {
      this.onTouched();
    }
  }

  onSearch(event: Event): void {
    this.query.set((event.target as HTMLInputElement).value);
  }

  isSelected(value: string): boolean {
    return this.selected().includes(value);
  }

  selectOption(option: UiSelectOption): void {
    if (option.disabled) {
      return;
    }
    if (this.multiple()) {
      const next = this.isSelected(option.value)
        ? this.selected().filter((v) => v !== option.value)
        : [...this.selected(), option.value];
      this.selected.set(next);
      this.onChange(next);
    } else {
      this.selected.set([option.value]);
      this.onChange(option.value);
      this.open.set(false);
      this.onTouched();
    }
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (this.open() && !this.host.nativeElement.contains(event.target)) {
      this.open.set(false);
      this.onTouched();
    }
  }

  writeValue(value: string | string[] | null): void {
    if (value == null) {
      this.selected.set([]);
    } else if (Array.isArray(value)) {
      this.selected.set(value);
    } else {
      this.selected.set([value]);
    }
  }

  registerOnChange(fn: (value: string | string[] | null) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.disabled.set(isDisabled);
  }
}
