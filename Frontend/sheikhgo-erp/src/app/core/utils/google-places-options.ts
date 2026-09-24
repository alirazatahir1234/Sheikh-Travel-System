/**
 * Shared Google Places Autocomplete options for Trips / Routes (and similar forms).
 * AE + PK cover UAE ops tests and Pakistan fleets. No `types` filter so POIs
 * (e.g. Dubai Mall) and street addresses both appear.
 */
export const SHEIKHGO_PLACES_COUNTRIES = ['ae', 'pk'] as const;

export const SHEIKHGO_DIRECTIONS_REGION = 'AE';

export function sheikhGoPlacesAutocompleteOptions(): google.maps.places.AutocompleteOptions {
  return {
    fields: ['formatted_address', 'name', 'geometry'],
    componentRestrictions: { country: [...SHEIKHGO_PLACES_COUNTRIES] }
  };
}
