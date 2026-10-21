export const environment = {
  production: true,
  apiUrl: 'http://20.174.1.230:5082/api',
  /** Same host as SPA; nginx proxies /uploads to the API. Azure blob URLs are absolute and bypass this. */
  filesBaseUrl: '',
  tenantSlug: 'default',
  // Restrict this key by HTTP referrer in Google Cloud Console.
  googleMapsApiKey: 'AIzaSyCSdQx1VMXLEnatOOknoTqwwrVRiEOg3tw'
};
