export const environment = {
  production: true,
  /**
   * HTTPS API (Caddy → Kestrel :5082 on Azure VM). Real Let’s Encrypt cert.
   * After Spaceship DNS A `api` → 20.174.1.230 propagates, switch to:
   *   apiUrl: 'https://api.sheikhgo.com/api'
   * (Caddy already serves that host.)
   */
  apiUrl: 'https://20-174-1-230.sslip.io/api',
  /** Same host as SPA; absolute API origin used for /uploads when filesBaseUrl is empty. */
  filesBaseUrl: '',
  tenantSlug: 'default',
  // Inject at build time (CI secret / file replace). Local: environment.secrets.local.ts (gitignored).
  // Restrict browser keys by HTTP referrer in Google Cloud Console — never commit live keys.
  googleMapsApiKey: ''
};
