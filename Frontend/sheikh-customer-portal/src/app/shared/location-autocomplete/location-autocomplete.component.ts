/// <reference types="google.maps" />
import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  forwardRef,
  inject,
  input,
  output,
  ViewChild
} from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { GoogleMapsLoaderService } from '../../core/services/google-maps-loader.service';

export interface LocationValue {
  address: string;
  lat: number;
  lng: number;
}

@Component({
  selector: 'app-location-autocomplete',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => LocationAutocompleteComponent),
      multi: true
    }
  ],
  template: `
    <label class="block text-xs font-semibold uppercase text-slate-500">{{ label() }}</label>
    <input
      #inputEl
      type="text"
      class="mt-1 w-full rounded-xl border border-slate-200 px-3 py-2.5 text-sm"
      [placeholder]="placeholder()"
      [disabled]="!maps.hasApiKey()"
    />
    @if (!maps.hasApiKey()) {
      <p class="mt-1 text-xs text-amber-800">Maps API key required for address search.</p>
    }
  `
})
export class LocationAutocompleteComponent implements ControlValueAccessor, AfterViewInit {
  readonly maps = inject(GoogleMapsLoaderService);
  readonly label = input('Location');
  readonly placeholder = input('Search address…');
  readonly locationChange = output<LocationValue>();

  @ViewChild('inputEl') inputEl?: ElementRef<HTMLInputElement>;

  private onChange: (v: LocationValue | null) => void = () => {};
  private onTouched: () => void = () => {};

  ngAfterViewInit(): void {
    void this.initAutocomplete();
  }

  writeValue(value: LocationValue | null): void {
    if (this.inputEl?.nativeElement && value?.address) {
      this.inputEl.nativeElement.value = value.address;
    }
  }

  registerOnChange(fn: (v: LocationValue | null) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    if (this.inputEl) this.inputEl.nativeElement.disabled = isDisabled;
  }

  private async initAutocomplete(): Promise<void> {
    if (!this.maps.hasApiKey() || !this.inputEl) return;
    try {
      const g = await this.maps.load();
      const ac = new g.maps.places.Autocomplete(this.inputEl.nativeElement, {
        fields: ['formatted_address', 'geometry']
      });
      ac.addListener('place_changed', () => {
        const place = ac.getPlace();
        const loc = place.geometry?.location;
        if (!loc || !place.formatted_address) return;
        const val: LocationValue = {
          address: place.formatted_address,
          lat: loc.lat(),
          lng: loc.lng()
        };
        this.onChange(val);
        this.locationChange.emit(val);
        this.onTouched();
      });
    } catch {
      // ignore
    }
  }
}
