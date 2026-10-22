export const environment = {
  production: true,
  /**
   * HTTPS API via free Cloudflare quick tunnel → Kestrel :5082 on Azure VM.
   * Caveat: trycloudflare.com URL usually changes when sheikhgo-cloudflared restarts.
   * Current URL on VM: /var/lib/sheikhgo/cloudflared-quick-url.txt
   * Caddy/sslip.io remains available as backup: https://20-174-1-230.sslip.io/api
   */
  apiUrl: 'https://differ-checkout-arrived-cool.trycloudflare.com/api',
  /** Same host as SPA; absolute API origin used for /uploads when filesBaseUrl is empty. */
  filesBaseUrl: '',
  tenantSlug: 'default',
  // Inject at build time (CI secret / file replace). Local: environment.secrets.local.ts (gitignored).
  // Restrict browser keys by HTTP referrer in Google Cloud Console — never commit live keys.
  googleMapsApiKey: ''
};
