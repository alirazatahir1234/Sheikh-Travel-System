import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { MenuService } from './menu.service';
import { AuthService } from './auth.service';

describe('MenuService', () => {
  let service: MenuService;
  let http: HttpTestingController;
  const isMenu = (url: string) => url.endsWith('/platform/menus/me');

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [{ provide: AuthService, useValue: { getToken: () => 'token', hasPermission: () => true } }]
    });
    service = TestBed.inject(MenuService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('resolveMenu builds the sidebar without any HTTP request', () => {
    const first = service.resolveMenu([], ['ADMIN'], []);
    const second = service.resolveMenu([], ['ADMIN'], ['ai']);

    expect(first).toBeTruthy();
    expect(second).toBeTruthy();
    http.expectNone(r => isMenu(r.url));
  });

  it('loadMenuModules emits null when menus/me fails, so the fallback menu is used', () => {
    let result: unknown = 'unset';
    service.loadMenuModules().subscribe(m => (result = m));

    http.expectOne(r => isMenu(r.url)).flush('boom', { status: 500, statusText: 'Server Error' });

    expect(result).toBeNull();
    expect(service.resolveMenu(null, ['ADMIN'], [])).toBeTruthy();
  });

  it('loadMenu still makes exactly one request', () => {
    service.loadMenu(['ADMIN'], []).subscribe();

    http.expectOne(r => isMenu(r.url)).flush([]);
  });
});
