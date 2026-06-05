import { Component, forwardRef, Input, OnInit } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';

@Component({
  selector: 'app-datetime-picker',
  standalone: true,
  imports: [
    CommonModule,
    MatFormFieldModule,
    MatInputModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule
  ],
  templateUrl: './datetime-picker.component.html',
  styleUrls: ['./datetime-picker.component.scss'],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => DatetimePickerComponent),
      multi: true
    }
  ]
})
export class DatetimePickerComponent implements ControlValueAccessor, OnInit {
  @Input() label = 'Select Date & Time';
  @Input() required = false;

  selectedDate: Date | null = null;
  selectedTime: string = '';

  private onChange: (value: string) => void = () => {};
  private onTouched: () => void = () => {};

  ngOnInit(): void {
    this.initializeDefaults();
  }

  private initializeDefaults(): void {
    const now = new Date();
    this.selectedDate = now;
    const hours = String(now.getHours()).padStart(2, '0');
    const minutes = String(now.getMinutes()).padStart(2, '0');
    this.selectedTime = `${hours}:${minutes}`;
  }

  writeValue(value: any): void {
    if (value) {
      try {
        const date = new Date(value);
        this.selectedDate = date;
        const hours = String(date.getHours()).padStart(2, '0');
        const minutes = String(date.getMinutes()).padStart(2, '0');
        this.selectedTime = `${hours}:${minutes}`;
      } catch (e) {
        this.initializeDefaults();
      }
    } else {
      this.initializeDefaults();
    }
  }

  registerOnChange(fn: (value: string) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {}

  onDateChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.valueAsDate) {
      this.selectedDate = input.valueAsDate;
    }
  }

  onTimeChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedTime = input.value;
  }

  confirmSelection(): void {
    if (!this.selectedDate || !this.selectedTime) {
      return;
    }

    const [hours, minutes] = this.selectedTime.split(':').map(v => Number(v));
    const merged = new Date(this.selectedDate);
    merged.setHours(hours, minutes, 0, 0);

    const isoValue = merged.toISOString();
    this.onChange(isoValue);
    this.onTouched();
  }

  isSelectionValid(): boolean {
    return !!this.selectedDate && !!this.selectedTime;
  }

  getDisplayValue(): string {
    if (!this.selectedDate || !this.selectedTime) return '';
    const date = this.selectedDate;
    const dayName = date.toLocaleDateString('en-US', { weekday: 'short' });
    const dateStr = date.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
    return `${dayName}, ${dateStr} at ${this.selectedTime}`;
  }
}
