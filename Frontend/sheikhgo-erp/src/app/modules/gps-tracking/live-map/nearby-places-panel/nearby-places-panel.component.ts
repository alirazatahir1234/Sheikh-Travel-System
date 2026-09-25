import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  Output
} from '@angular/core';
import { NearbyPlace } from '../../../../core/models/gps-tracking.model';

export interface NearbyCategoryOption {
  id: string;
  label: string;
  icon: string;
}

/** Curated extras under API category `more` (backend Place types only). */
export const NEARBY_MORE_EXTRAS: { id: string; label: string; icon: string }[] = [
  { id: 'pharmacy', label: 'Pharmacy', icon: 'local_pharmacy' },
  { id: 'convenience', label: 'Convenience', icon: 'storefront' },
  { id: 'mall', label: 'Shopping mall', icon: 'store_mall_directory' }
];

@Component({
  standalone: false,
  selector: 'app-nearby-places-panel',
  templateUrl: './nearby-places-panel.component.html',
  styleUrls: ['./nearby-places-panel.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class NearbyPlacesPanelComponent {
  readonly moreExtras = NEARBY_MORE_EXTRAS;

  @Input() vehicleLabel = 'Map center';
  @Input() categories: NearbyCategoryOption[] = [];
  @Input() selectedCategory = 'fuel';
  @Input() radiusMeters = 1500;
  @Input() radiusOptions: { value: number; label: string }[] = [
    { value: 500, label: '500 m' },
    { value: 1500, label: '1.5 km' },
    { value: 3000, label: '3 km' },
    { value: 5000, label: '5 km' }
  ];
  @Input() places: NearbyPlace[] = [];
  @Input() selectedPlace: NearbyPlace | null = null;
  @Input() loading = false;
  @Input() emptyMessage: string | null = null;
  @Input() apiError: string | null = null;
  @Input() directionsUrl = '';
  @Input() mapsUrl = '';

  @Output() closePanel = new EventEmitter<void>();
  @Output() categoryChange = new EventEmitter<string>();
  @Output() radiusChange = new EventEmitter<number>();
  @Output() placeSelect = new EventEmitter<NearbyPlace>();
  @Output() retry = new EventEmitter<void>();

  /** Categories shown as primary chips (everything except `more`). */
  get primaryCategories(): NearbyCategoryOption[] {
    return this.categories.filter(c => c.id !== 'more');
  }

  get moreCategory(): NearbyCategoryOption | undefined {
    return this.categories.find(c => c.id === 'more');
  }

  get isMoreActive(): boolean {
    return this.selectedCategory === 'more';
  }

  trackByPlace = (_: number, place: NearbyPlace): string => place.placeId;

  categoryIcon(category: string): string {
    return this.categories.find(c => c.id === category)?.icon ?? 'place';
  }

  formatDistance(meters?: number | null): string {
    if (meters == null || !Number.isFinite(meters)) return '';
    if (meters < 1000) return `${Math.round(meters)} m`;
    return `${(meters / 1000).toFixed(1)} km`;
  }

  openingLabel(place: NearbyPlace): string {
    if (place.openingStatus?.trim()) return place.openingStatus.trim();
    if (place.openNow === true) return 'Open';
    if (place.openNow === false) return 'Closed';
    return '';
  }

  shortAddress(address?: string | null): string {
    if (!address?.trim()) return '';
    const parts = address.split(',').map(p => p.trim()).filter(Boolean);
    if (parts.length <= 2) return address.trim();
    return parts.slice(0, 2).join(', ');
  }

  onSelectCategory(id: string): void {
    this.categoryChange.emit(id);
  }

  onSelectMore(): void {
    this.categoryChange.emit('more');
  }

  onRadiusChange(value: number): void {
    this.radiusChange.emit(value);
  }

  onSelectPlace(place: NearbyPlace): void {
    this.placeSelect.emit(place);
  }
}
