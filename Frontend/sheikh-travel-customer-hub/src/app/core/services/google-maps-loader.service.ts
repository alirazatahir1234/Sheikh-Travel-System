/// <reference types="google.maps" />
import { Injectable } from '@angular/core';
import { Loader } from '@googlemaps/js-api-loader';
import { environment } from '../../../environments/environment';

let loadPromise: Promise<typeof google> | null = null;

@Injectable({ providedIn: 'root' })
export class GoogleMapsLoaderService {
  hasApiKey(): boolean {
    return !!environment.googleMapsApiKey?.trim();
  }

  /** Loads the Maps JavaScript API once; rejects if `googleMapsApiKey` is missing. */
  load(): Promise<typeof google> {
    const key = environment.googleMapsApiKey?.trim();
    if (!key) {
      return Promise.reject(new Error('Missing googleMapsApiKey in environment'));
    }
    if (typeof google !== 'undefined' && google.maps) {
      return Promise.resolve(google);
    }
    if (!loadPromise) {
      const loader = new Loader({ apiKey: key, version: 'weekly' });
      loadPromise = loader.load().then(() => google);
    }
    return loadPromise;
  }
}
