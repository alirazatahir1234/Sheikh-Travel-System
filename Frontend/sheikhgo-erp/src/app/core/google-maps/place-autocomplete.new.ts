import { NgZone } from '@angular/core';
import { GoogleMapsLoaderService } from '../services/google-maps-loader.service';
import { SHEIKHGO_PLACES_COUNTRIES } from '../utils/google-places-options';

/** Prefer Places API (New); legacy Autocomplete used when New is unavailable. */
export const usePlacesNew = true;

export interface SheikhGoPlaceSelection {
  address: string;
  name: string;
  lat: number;
  lng: number;
  placeId: string;
}

/** Partial request defaults for AutocompleteSuggestion.fetchAutocompleteSuggestions. */
export interface PlacesNewAutocompleteRequest {
  input: string;
  includedRegionCodes?: string[];
  includedPrimaryTypes?: string[];
  sessionToken?: unknown;
  language?: string;
  region?: string;
}

export interface AttachSheikhGoPlacesOptions {
  input: HTMLInputElement;
  ngZone: NgZone;
  mapsLoader: GoogleMapsLoaderService;
  onSelect: (place: SheikhGoPlaceSelection) => void;
  /** Merged onto sheikhGoPlacesNewRequestDefaults(). */
  requestOverrides?: Partial<Omit<PlacesNewAutocompleteRequest, 'input'>>;
  /** Legacy Autocomplete options when New API is unavailable. */
  legacyAutocompleteOptions?: google.maps.places.AutocompleteOptions;
  debounceMs?: number;
}

export interface SheikhGoPlacesAutocompleteHandle {
  destroy(): void;
}

interface PlacesNewLibrary {
  AutocompleteSuggestion?: {
    fetchAutocompleteSuggestions: (
      request: PlacesNewAutocompleteRequest
    ) => Promise<{ suggestions?: PlacesNewSuggestion[] }>;
  };
  AutocompleteSessionToken?: new () => unknown;
  Autocomplete?: typeof google.maps.places.Autocomplete;
}

interface PlacesNewSuggestion {
  placePrediction?: PlacesNewPlacePrediction;
}

interface PlacesNewPlacePrediction {
  placeId?: string;
  text?: { text?: string; toString?: () => string };
  toPlace: () => PlacesNewPlace;
}

interface PlacesNewPlace {
  id?: string;
  displayName?: string;
  formattedAddress?: string;
  location?: google.maps.LatLngLiteral | { lat: number; lng: number };
  fetchFields: (opts: { fields: string[] }) => Promise<void>;
}

export function sheikhGoPlacesNewRequestDefaults(): Omit<
  PlacesNewAutocompleteRequest,
  'input' | 'sessionToken'
> {
  return {
    includedRegionCodes: [...SHEIKHGO_PLACES_COUNTRIES],
    language: 'en'
  };
}

export async function attachSheikhGoPlacesAutocomplete(
  options: AttachSheikhGoPlacesOptions
): Promise<SheikhGoPlacesAutocompleteHandle> {
  const placesLib = (await options.mapsLoader.importLibrary('places')) as PlacesNewLibrary;

  if (usePlacesNew && supportsPlacesNew(placesLib)) {
    try {
      return attachPlacesNew(options, placesLib);
    } catch (err) {
      console.warn('Places API (New) attach failed; falling back to legacy Autocomplete.', err);
    }
  }

  return attachPlacesLegacy(options, placesLib);
}

function supportsPlacesNew(placesLib: PlacesNewLibrary): boolean {
  return typeof placesLib.AutocompleteSuggestion?.fetchAutocompleteSuggestions === 'function';
}

function attachPlacesNew(
  options: AttachSheikhGoPlacesOptions,
  placesLib: PlacesNewLibrary
): SheikhGoPlacesAutocompleteHandle {
  const { input, ngZone, onSelect, requestOverrides, debounceMs = 300 } = options;
  const AutocompleteSuggestion = placesLib.AutocompleteSuggestion!;
  const SessionTokenCtor = placesLib.AutocompleteSessionToken;

  let sessionToken = SessionTokenCtor ? new SessionTokenCtor() : undefined;
  let debounceTimer: ReturnType<typeof setTimeout> | null = null;
  let fetchSeq = 0;
  let destroyed = false;

  const host = ensureRelativeHost(input);
  const dropdown = document.createElement('ul');
  dropdown.className = 'sheikhgo-places-new-dropdown';
  dropdown.setAttribute('role', 'listbox');
  dropdown.hidden = true;
  host.appendChild(dropdown);

  const hideDropdown = (): void => {
    dropdown.hidden = true;
    dropdown.replaceChildren();
  };

  const selectPrediction = async (prediction: PlacesNewPlacePrediction): Promise<void> => {
    hideDropdown();
    try {
      const place = prediction.toPlace();
      await place.fetchFields({
        fields: ['displayName', 'formattedAddress', 'location', 'id']
      });
      const loc = normalizeLatLng(place.location);
      if (!loc) return;

      const name = place.displayName || predictionText(prediction) || '';
      const address = place.formattedAddress || name;
      ngZone.run(() => {
        onSelect({
          address,
          name,
          lat: loc.lat,
          lng: loc.lng,
          placeId: place.id || prediction.placeId || ''
        });
      });
    } catch (err) {
      console.warn('Places API (New) place fetch failed:', err);
    } finally {
      sessionToken = SessionTokenCtor ? new SessionTokenCtor() : undefined;
    }
  };

  const renderSuggestions = (suggestions: PlacesNewSuggestion[]): void => {
    dropdown.replaceChildren();
    const predictions = suggestions
      .map(s => s.placePrediction)
      .filter((p): p is PlacesNewPlacePrediction => !!p);

    if (!predictions.length) {
      hideDropdown();
      return;
    }

    predictions.forEach(prediction => {
      const item = document.createElement('li');
      item.className = 'sheikhgo-places-new-item';
      item.setAttribute('role', 'option');
      item.textContent = predictionText(prediction);
      item.addEventListener('mousedown', event => {
        event.preventDefault();
        void selectPrediction(prediction);
      });
      dropdown.appendChild(item);
    });
    dropdown.hidden = false;
  };

  const onInput = (): void => {
    if (destroyed) return;
    const query = input.value.trim();
    if (debounceTimer) clearTimeout(debounceTimer);

    if (!query) {
      hideDropdown();
      sessionToken = SessionTokenCtor ? new SessionTokenCtor() : undefined;
      return;
    }

    debounceTimer = setTimeout(() => {
      const seq = ++fetchSeq;
      const request: PlacesNewAutocompleteRequest = {
        ...sheikhGoPlacesNewRequestDefaults(),
        ...requestOverrides,
        input: query,
        sessionToken
      };

      void AutocompleteSuggestion.fetchAutocompleteSuggestions(request)
        .then(({ suggestions }) => {
          if (destroyed || seq !== fetchSeq) return;
          renderSuggestions(suggestions ?? []);
        })
        .catch(err => {
          console.warn('Places API (New) suggestions failed:', err);
          hideDropdown();
        });
    }, debounceMs);
  };

  const onBlur = (): void => {
    window.setTimeout(() => hideDropdown(), 150);
  };

  const onKeyDown = (event: KeyboardEvent): void => {
    if (event.key === 'Escape') hideDropdown();
  };

  input.addEventListener('input', onInput);
  input.addEventListener('blur', onBlur);
  input.addEventListener('keydown', onKeyDown);

  return {
    destroy(): void {
      destroyed = true;
      if (debounceTimer) clearTimeout(debounceTimer);
      input.removeEventListener('input', onInput);
      input.removeEventListener('blur', onBlur);
      input.removeEventListener('keydown', onKeyDown);
      dropdown.remove();
    }
  };
}

function attachPlacesLegacy(
  options: AttachSheikhGoPlacesOptions,
  placesLib: PlacesNewLibrary
): SheikhGoPlacesAutocompleteHandle {
  if (!placesLib.Autocomplete) {
    throw new Error('Legacy Places Autocomplete is unavailable.');
  }

  const { input, ngZone, onSelect, legacyAutocompleteOptions } = options;
  const autocomplete = new placesLib.Autocomplete(input, legacyAutocompleteOptions ?? {
    fields: ['formatted_address', 'name', 'geometry', 'place_id'],
    componentRestrictions: { country: [...SHEIKHGO_PLACES_COUNTRIES] }
  });

  const listener = autocomplete.addListener('place_changed', () => {
    const place = autocomplete.getPlace();
    const loc = place.geometry?.location;
    if (!loc) return;
    const name = place.name || '';
    const address = place.formatted_address || name;
    ngZone.run(() => {
      onSelect({
        address,
        name,
        lat: loc.lat(),
        lng: loc.lng(),
        placeId: place.place_id || ''
      });
    });
  });

  return {
    destroy(): void {
      listener.remove();
    }
  };
}

function ensureRelativeHost(input: HTMLInputElement): HTMLElement {
  const parent = input.parentElement;
  if (!parent) return input;
  const position = window.getComputedStyle(parent).position;
  if (position === 'static' || !position) {
    parent.style.position = 'relative';
  }
  return parent;
}

function predictionText(prediction: PlacesNewPlacePrediction): string {
  const text = prediction.text;
  if (!text) return '';
  if (typeof text.text === 'string') return text.text;
  return text.toString?.() ?? '';
}

function normalizeLatLng(
  loc: google.maps.LatLngLiteral | google.maps.LatLng | { lat: number; lng: number } | undefined
): google.maps.LatLngLiteral | null {
  if (!loc) return null;
  const lat = typeof (loc as google.maps.LatLng).lat === 'function'
    ? (loc as google.maps.LatLng).lat()
    : (loc as google.maps.LatLngLiteral).lat;
  const lng = typeof (loc as google.maps.LatLng).lng === 'function'
    ? (loc as google.maps.LatLng).lng()
    : (loc as google.maps.LatLngLiteral).lng;
  if (typeof lat !== 'number' || typeof lng !== 'number') return null;
  return { lat, lng };
}
