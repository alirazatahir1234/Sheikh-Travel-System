export const environment = {
  production: false,
  // Proxied to http://127.0.0.1:5082 via proxy.conf.json when using ng serve
  apiUrl: '/api',
  tenantSlug: 'default',
  // Google Maps JavaScript API key.
  // Enable: Maps JavaScript API, Places API, Directions API in Google Cloud Console.
  // Leave empty to disable Maps features — the route form will still work with manual input.
  googleMapsApiKey: 'AIzaSyCSdQx1VMXLEnatOOknoTqwwrVRiEOg3tw'
};
