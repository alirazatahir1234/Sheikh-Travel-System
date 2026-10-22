export const environment = {
  production: true,
  /**
   * Stable HTTPS API via Caddy → Kestrel on Azure VM (sslip.io).
   * Do NOT point production at ephemeral trycloudflare.com tunnels — they die on restart
   * and break login from https://www.sheikhgo.com (Mixed Content / dead origin).
   * Preferred after DNS: https://api.sheikhgo.com/api
   */
  apiUrl: 'https://20-174-1-230.sslip.io/api',
  /** Same host as SPA; absolute API origin used for /uploads when filesBaseUrl is empty. */
  filesBaseUrl: '',
  tenantSlug: 'default',
  // Inject at build time (CI secret / file replace). Local: environment.secrets.local.ts (gitignored).
  // Restrict browser keys by HTTP referrer in Google Cloud Console — never commit live keys.
  googleMapsApiKey: ''
};
