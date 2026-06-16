import { Component, OnInit, OnDestroy } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { Subject, BehaviorSubject, debounceTime, distinctUntilChanged, switchMap, of, Observable, Subscription, map, filter, combineLatest, startWith } from 'rxjs';
import { MatDialog } from '@angular/material/dialog';
import { AuthService } from '../../core/services/auth.service';
import { NotificationService } from '../../core/services/notification.service';
import { GlobalSearchService, SearchResult } from '../../core/services/global-search.service';
import {
  Notification,
  NotificationType,
  NotificationTypeIcons,
  NotificationTypeColors
} from '../../core/models/notification.model';
import { HelpDialogComponent } from '../../shared/components/help-dialog/help-dialog.component';
import { LocalTimeContextService, LocalTimeDisplay } from '../../core/services/local-time-context.service';
import { TenantConfigService } from '../../core/services/tenant-config.service';
import { MenuService } from '../../core/services/menu.service';
import { NavGroup, NavItem, ResolvedMenu } from '../../core/navigation/nav-models';
import {
  defaultExpandedGroupIds,
  groupContainingRoute,
  resolveMenu
} from '../../core/navigation/menu-config';
import { resolveTenantType } from '../../core/navigation/tenant-type';

@Component({
  selector: 'app-shell',
  templateUrl: './shell.component.html',
  styleUrls: ['./shell.component.scss']
})
export class ShellComponent implements OnInit, OnDestroy {
  private readonly sidebarPinnedStorageKey = 'stb_sidebar_pinned';
  private readonly enabledModules$ = new BehaviorSubject<string[]>([]);
  private latestMenu?: ResolvedMenu;

  menu$!: Observable<ResolvedMenu>;
  currentUser$: AuthService['currentUser$'];
  unreadCount$!: NotificationService['unreadCount'];
  notifications$!: NotificationService['notifications'];

  expandedGroupIds = new Set<string>();

  searchQuery = '';
  searchResults: SearchResult[] = [];
  searchLoading = false;
  showSearchResults = false;
  isSidebarPinned = true;
  isSidebarHovering = false;
  mobileNavOpen = false;
  private searchSubject = new Subject<string>();
  private searchSub?: Subscription;
  private sessionSub?: Subscription;
  private routerSub?: Subscription;
  private menuSub?: Subscription;

  /** Secondary items that share a route with a primary nav entry. */
  private readonly aliasItemIds = new Set([
    'trips', 'dispatch-board', 'invoices', 'wallets', 'expenses',
    'corporate-accounts', 'passengers', 'vendors',
    'performance-analytics', 'roles-permissions', 'system-configuration'
  ]);
  timeDisplay$: Observable<LocalTimeDisplay>;

  constructor(
    private auth: AuthService,
    private notificationService: NotificationService,
    private globalSearch: GlobalSearchService,
    private localTime: LocalTimeContextService,
    private router: Router,
    private dialog: MatDialog,
    private tenantConfig: TenantConfigService,
    private menuService: MenuService
  ) {
    this.currentUser$ = auth.currentUser$;
    this.unreadCount$ = notificationService.unreadCount;
    this.notifications$ = notificationService.notifications;
    this.timeDisplay$ = this.localTime.clockDisplay$();

    this.menu$ = combineLatest([this.currentUser$, this.enabledModules$]).pipe(
      switchMap(([user, enabledModules]) => {
        const roles = user?.roles ?? [];
        const tenantType = resolveTenantType(roles);

        if (roles.includes('Driver')) {
          return of(resolveMenu({ tenantType, roles, enabledModules }));
        }

        if (!user || !this.auth.getToken()) {
          return of({ groups: [], standaloneItems: [], isDriverLayout: false });
        }

        return this.menuService.loadMenu(roles, enabledModules);
      })
    );
  }

  get homeRoute(): string {
    return this.auth.hasRole('Driver') ? '/my-trips' : '/dashboard';
  }

  get isDriverUser(): boolean {
    return this.auth.hasRole('Driver');
  }

  ngOnInit(): void {
    this.isSidebarPinned = this.readSidebarPinnedPreference();

    this.tenantConfig.loadBranding().subscribe(b => {
      if (b?.enabledModules?.length) {
        this.enabledModules$.next(b.enabledModules);
      }
    });

    this.sessionSub = this.auth.currentUser$.subscribe(user => {
      if (user && this.auth.getToken()) {
        queueMicrotask(() => this.notificationService.startPolling(60000));
      } else {
        this.notificationService.reset();
      }
    });

    this.menuSub = this.menu$.subscribe(menu => {
      this.latestMenu = menu;
      this.expandedGroupIds = defaultExpandedGroupIds(menu);
      this.ensureActiveGroupExpanded(menu);
    });

    this.routerSub = this.router.events.pipe(
      filter((e): e is NavigationEnd => e instanceof NavigationEnd)
    ).subscribe(() => {
      this.closeMobileNav();
      if (this.latestMenu) {
        this.ensureActiveGroupExpanded(this.latestMenu);
      }
    });

    this.initSearch();
  }

  ngOnDestroy(): void {
    this.sessionSub?.unsubscribe();
    this.searchSub?.unsubscribe();
    this.routerSub?.unsubscribe();
    this.menuSub?.unsubscribe();
    this.notificationService.reset();
  }

  private ensureActiveGroupExpanded(menu: ResolvedMenu): void {
    const groupId = groupContainingRoute(menu, this.router.url);
    if (groupId) {
      this.expandedGroupIds = new Set([...this.expandedGroupIds, groupId]);
    }
  }

  private initSearch(): void {
    this.searchSub = this.searchSubject.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      switchMap(query => {
        if (!query || query.trim().length < 2) {
          return of([]);
        }
        this.searchLoading = true;
        return this.globalSearch.search(query);
      })
    ).subscribe(results => {
      this.searchResults = results;
      this.searchLoading = false;
      this.showSearchResults = results.length > 0 || this.searchQuery.length >= 2;
    });
  }

  onSearchInput(query: string): void {
    this.searchQuery = query;
    this.searchSubject.next(query);
  }

  onSearchFocus(): void {
    if (this.searchQuery.length >= 2) {
      this.showSearchResults = true;
    }
  }

  onSearchBlur(): void {
    setTimeout(() => {
      this.showSearchResults = false;
    }, 200);
  }

  get sidebarExpanded(): boolean {
    return this.isSidebarPinned || this.isSidebarHovering || this.mobileNavOpen;
  }

  toggleMobileNav(): void {
    this.mobileNavOpen = !this.mobileNavOpen;
    if (this.mobileNavOpen) {
      this.isSidebarHovering = true;
    } else if (!this.isSidebarPinned) {
      this.isSidebarHovering = false;
    }
  }

  closeMobileNav(): void {
    if (!this.mobileNavOpen) return;
    this.mobileNavOpen = false;
    if (!this.isSidebarPinned) {
      this.isSidebarHovering = false;
    }
  }

  onSidebarEnter(): void {
    if (this.isMobileViewport()) return;
    this.isSidebarHovering = true;
  }

  onSidebarLeave(): void {
    if (this.isMobileViewport() || this.mobileNavOpen) return;
    this.isSidebarHovering = false;
  }

  private isMobileViewport(): boolean {
    return typeof window !== 'undefined' && window.matchMedia('(max-width: 768px)').matches;
  }

  toggleSidebarPin(): void {
    this.isSidebarPinned = !this.isSidebarPinned;
    localStorage.setItem(this.sidebarPinnedStorageKey, String(this.isSidebarPinned));
  }

  private readSidebarPinnedPreference(): boolean {
    const stored = localStorage.getItem(this.sidebarPinnedStorageKey);
    // Default to expanded/pinned sidebar on first load.
    if (stored === null) {
      return true;
    }
    return stored === 'true';
  }

  isGroupExpanded(groupId: string): boolean {
    return this.expandedGroupIds.has(groupId);
  }

  toggleGroup(group: NavGroup, event: Event): void {
    event.preventDefault();
    event.stopPropagation();

    const navigateToPrimary = (): void => {
      const primary = group.items.find(item => !this.aliasItemIds.has(item.id)) ?? group.items[0];
      if (!primary?.route) return;
      this.router.navigate([primary.route], { queryParams: primary.queryParams });
      this.closeMobileNav();
    };

    if (!this.sidebarExpanded || this.isMobileViewport()) {
      this.isSidebarHovering = true;
      this.expandedGroupIds = new Set([...this.expandedGroupIds, group.id]);
      navigateToPrimary();
      return;
    }

    const next = new Set(this.expandedGroupIds);
    if (next.has(group.id)) {
      next.delete(group.id);
    } else {
      next.add(group.id);
    }
    this.expandedGroupIds = next;
  }

  isItemActive(item: NavItem): boolean {
    const tree = this.router.parseUrl(this.router.url);
    const path = tree.root.children['primary']?.segments.map(s => s.path).join('/') ?? '';
    const normalizedPath = '/' + path;

    if (item.queryParams) {
      const onRoute = normalizedPath === item.route;
      return onRoute && Object.entries(item.queryParams).every(
        ([key, value]) => tree.queryParams[key] === value
      );
    }

    const onRoute = normalizedPath === item.route || normalizedPath.startsWith(item.route + '/');
    if (!onRoute) return false;
    if (this.aliasItemIds.has(item.id)) return false;
    return true;
  }

  isGroupActive(group: NavGroup): boolean {
    return group.items.some(item => this.isItemActive(item));
  }

  navigateToResult(result: SearchResult): void {
    this.showSearchResults = false;
    this.searchQuery = '';
    this.router.navigate([result.route]);
  }

  getResultTypeLabel(type: string): string {
    const labels: Record<string, string> = {
      booking: 'Booking',
      vehicle: 'Vehicle',
      driver: 'Driver',
      customer: 'Customer',
      route: 'Route',
      payment: 'Payment',
      fuel_log: 'Fuel Log',
      maintenance: 'Maintenance'
    };
    return labels[type] || type;
  }

  trackById(_i: number, item: { id: string }): string { return item.id; }
  trackByGroupId(_i: number, group: NavGroup): string { return group.id; }
  trackByNotifId(_i: number, n: Notification): number { return n.id; }

  logout(): void { this.auth.logout(); }

  goToProfile(): void {
    this.router.navigate(['/profile']);
  }

  goToSettings(): void {
    this.router.navigate(['/profile'], { queryParams: { tab: 'settings' } });
  }

  initials(fullName?: string | null): string {
    if (!fullName) return '?';
    return fullName.split(' ').map(p => p[0]).slice(0, 2).join('').toUpperCase();
  }

  getNotifIcon(type: NotificationType): string {
    return NotificationTypeIcons[type] ?? 'notifications';
  }

  getNotifColor(type: NotificationType): string {
    return NotificationTypeColors[type] ?? '#64748B';
  }

  markAsRead(n: Notification, event: Event): void {
    event.stopPropagation();
    if (!n.isRead) {
      this.notificationService.markAsRead([n.id]).subscribe();
    }
  }

  markAllAsRead(): void {
    this.notificationService.markAllAsRead().subscribe();
  }

  onNotificationClick(n: Notification): void {
    if (!n.isRead) {
      this.notificationService.markAsRead([n.id]).subscribe();
    }
    if (n.referenceId) {
      switch (n.type) {
        case NotificationType.BookingCreated:
          this.router.navigate(['/bookings', n.referenceId]);
          break;
        case NotificationType.PaymentReceived:
          this.router.navigate(['/payments']);
          break;
        case NotificationType.VehicleOffline:
          this.router.navigate(['/vehicles', n.referenceId, 'edit']);
          break;
        default:
          break;
      }
    }
  }

  formatTimeAgo(dateStr: string): string {
    const date = new Date(dateStr);
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffMins = Math.floor(diffMs / 60000);
    const diffHours = Math.floor(diffMins / 60);
    const diffDays = Math.floor(diffHours / 24);

    if (diffMins < 1) return 'Just now';
    if (diffMins < 60) return `${diffMins}m ago`;
    if (diffHours < 24) return `${diffHours}h ago`;
    if (diffDays < 7) return `${diffDays}d ago`;
    return date.toLocaleDateString();
  }

  openHelp(): void {
    this.dialog.open(HelpDialogComponent, {
      width: '600px',
      maxHeight: '80vh'
    });
  }

  localTimeTooltip(t: LocalTimeDisplay): string {
    return [t.timeZoneId, t.dateLine, t.offsetAndAbbr].filter(Boolean).join(' · ');
  }
}
