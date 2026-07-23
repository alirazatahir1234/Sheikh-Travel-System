export const environment = {
  production: true,
  apiUrl: 'https://sheikh-travel-system-production.up.railway.app/api',
  /** Same host as SPA; nginx proxies /uploads to the API. Azure blob URLs are absolute and bypass this. */
  filesBaseUrl: '',
  tenantSlug: 'default',
  // Restrict this key by HTTP referrer in Google Cloud Console.
  googleMapsApiKey: 'AIzaSyCSdQx1VMXLEnatOOknoTqwwrVRiEOg3tw'
};
