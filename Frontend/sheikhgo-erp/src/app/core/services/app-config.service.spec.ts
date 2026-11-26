import { AppConfigService, isApiSameSite } from './app-config.service';
import { environment } from '../../../environments/environment';

describe('AppConfigService', () => {
  const originalApiUrl = environment.apiUrl;
  afterEach(() => { environment.apiUrl = originalApiUrl; });

  const respond = (body: unknown, ok = true) =>
    (() => Promise.resolve({ ok, json: () => Promise.resolve(body) } as Response)) as unknown as typeof fetch;

  it('applies apiUrl from assets/config.json so existing environment.apiUrl readers see it', async () => {
    const service = new AppConfigService();

    await service.load(respond({ apiUrl: 'https://api.example.com/api/', authMode: 'bearer' }));

    expect(service.apiUrl).toBe('https://api.example.com/api');
    expect(environment.apiUrl).toBe('https://api.example.com/api');
  });

  it('requests the file without caching', async () => {
    const spy = jasmine.createSpy('fetch').and.returnValue(
      Promise.resolve({ ok: true, json: () => Promise.resolve({ apiUrl: '/api' }) }));

    await new AppConfigService().load(spy as unknown as typeof fetch);

    expect(spy).toHaveBeenCalledWith('assets/config.json', { cache: 'no-store' });
  });

  it('keeps the build default when the file is missing or unreadable', async () => {
    const missing = new AppConfigService();
    await missing.load(respond(null, false));
    expect(missing.apiUrl).toBe(originalApiUrl);

    const broken = new AppConfigService();
    await broken.load((() => Promise.reject(new Error('offline'))) as unknown as typeof fetch);
    expect(broken.apiUrl).toBe(originalApiUrl);
    expect(broken.authMode).toBe('bearer');
  });

  it('defaults to bearer when authMode is absent', () => {
    const service = new AppConfigService();
    service.apply({ apiUrl: '/api' }, 'http://localhost:4200');
    expect(service.authMode).toBe('bearer');
  });

  it('uses cookie mode when the API is same-origin or same-site', () => {
    const proxied = new AppConfigService();
    proxied.apply({ apiUrl: '/api', authMode: 'cookie' }, 'http://localhost:4200');
    expect(proxied.authMode).toBe('cookie');

    const subdomain = new AppConfigService();
    subdomain.apply({ apiUrl: 'https://api.sheikhgo.com/api', authMode: 'cookie' }, 'https://www.sheikhgo.com');
    expect(subdomain.authMode).toBe('cookie');
  });

  it('falls back to bearer when cookie mode is configured but the API is cross-site', () => {
    const service = new AppConfigService();

    service.apply({ apiUrl: 'https://20-174-1-230.sslip.io/api', authMode: 'cookie' }, 'https://www.sheikhgo.com');

    expect(service.authMode).toBe('bearer');
  });
});

describe('isApiSameSite', () => {
  it('treats a relative API URL as same-site', () => {
    expect(isApiSameSite('/api', 'https://www.sheikhgo.com')).toBeTrue();
  });

  it('treats subdomains of one registrable domain as same-site', () => {
    expect(isApiSameSite('https://api.sheikhgo.com/api', 'https://www.sheikhgo.com')).toBeTrue();
    expect(isApiSameSite('https://api.sheikhgo.com/api', 'https://sheikhgo.com')).toBeTrue();
  });

  it('rejects a different domain, a different scheme and raw IPs', () => {
    expect(isApiSameSite('https://20-174-1-230.sslip.io/api', 'https://www.sheikhgo.com')).toBeFalse();
    expect(isApiSameSite('http://api.sheikhgo.com/api', 'https://www.sheikhgo.com')).toBeFalse();
    expect(isApiSameSite('http://127.0.0.1:5082/api', 'http://localhost:4200')).toBeFalse();
    expect(isApiSameSite('https://20.174.1.230/api', 'https://20.174.1.231')).toBeFalse();
  });

  it('never treats two hosts under a shared suffix as same-site', () => {
    expect(isApiSameSite('https://api-app.vercel.app/api', 'https://erp-app.vercel.app')).toBeFalse();
    expect(isApiSameSite('https://a.sslip.io/api', 'https://b.sslip.io')).toBeFalse();
  });
});
