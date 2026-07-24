import { Component, OnInit, OnDestroy, ViewChild, ElementRef, HostListener } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { Subject, BehaviorSubject, debounceTime, distinctUntilChanged, exhaustMap, switchMap, of, Observable, Subscription, map, filter, combineLatest, startWith, tap, finalize, shareReplay, catchError, take } from 'rxjs';
import { MatDialog } from '@angular/material/dialog';
import { AuthService } from '../../core/services/auth.service';
import { NotificationService } from '../../core/services/notification.service';
import { GlobalSearchService, SearchResult } from '../../core/services/global-search.service';
import {
  Notification,
  NotificationType,
  NotificationTypeIcons,
  NotificationTypeColors,
  NotificationPriorityLabels
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
import { isNavItemActive } from '../../core/navigation/nav-route-active.util';
import { resolveTenantType } from '../../core/navigation/tenant-type';
import { APP_PRODUCT_NAME, APP_SIDEBAR_LOGO_PATH } from '../../core/constants/app-brand';

interface NotifMetaRow {
  icon: string;
  label: string;
  value: string;
  critical?: boolean;
}
@Component({
  standalone: false,
  selector: 'app-shell',
  templateUrl: './shell.component.html',
  styleUrls: ['./shell.component.scss']
})
export class ShellComponent implements OnInit, OnDestroy {
  readonly appProductName = APP_PRODUCT_NAME;
  readonly appLogoPath = APP_SIDEBAR_LOGO_PATH;
  private readonly sidebarPinnedStorageKey = 'stb_sidebar_pinned';
  private readonly enabledModules$ = new BehaviorSubject<string[]>([]);
  private latestMenu?: ResolvedMenu;

  menu$!: Observable<ResolvedMenu>;
  currentUser$: AuthService['currentUser$'];
  unreadCount$!: NotificationService['unreadCount'];
  notifications$!: NotificationService['notifications'];
  notifPanelOpen = false;
  expandedNotifId: number | null = null;
  @ViewChild('mainContent') mainContent!: ElementRef<HTMLElement>;

  expandedGroupIds = new Set<string>();

  searchQuery = '';
  searchResults: SearchResult[] = [];
  searchLoading = false;
  showSearchResults = false;
  isSidebarPinned = true;
  isSidebarHovering = false;
  mobileNavOpen = false;
  menuLoading = true;
  menu: ResolvedMenu | null = null;
  readonly skeletonNavRows = [1, 2, 3, 4, 5, 6, 7, 8];
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
      tap(() => { this.menuLoading = true; }),
      exhaustMap(([user, enabledModules]) => {
        const roles = user?.roles ?? [];
        const tenantType = resolveTenantType(roles);

        if (roles.includes('Driver')) {
          return of(resolveMenu({ tenantType, roles, enabledModules }));
        }

        if (!user || !this.auth.getToken()) {
          return of({ groups: [], standaloneItems: [], isDriverLayout: false });
        }

        return this.menuService.loadMenu(roles, enabledModules);
      }),
      tap(resolved => {
        this.menu = resolved;
        queueMicrotask(() => { this.menuLoading = false; });
      }),
      finalize(() => { queueMicrotask(() => { this.menuLoading = false; }); }),
      shareReplay(1)
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
        queueMicrotask(() => {
          this.notificationService.startPolling(120000);
          void this.notificationService.requestBrowserPermission();
        });
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
      this.mainContent?.nativeElement?.scrollTo({ top: 0, behavior: 'instant' });
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
      debounceTime(250),
      distinctUntilChanged(),
      switchMap(query => {
        const trimmed = (query ?? '').trim();
        if (trimmed.length < 2) {
          this.searchLoading = false;
          this.searchResults = [];
          this.showSearchResults = false;
          return of([] as SearchResult[]);
        }

        // Show module hits immediately while entity APIs load
        const quickModules = this.globalSearch.searchModules(trimmed, this.menu ?? this.latestMenu);
        this.searchResults = quickModules;
        this.showSearchResults = true;
        this.searchLoading = true;

        return this.globalSearch.search(trimmed, { menu: this.menu ?? this.latestMenu }).pipe(
          catchError(() => of(quickModules)),
          finalize(() => { this.searchLoading = false; })
        );
      })
    ).subscribe(results => {
      this.searchResults = results;
      this.showSearchResults = this.searchQuery.trim().length >= 2;
    });
  }

  onSearchInput(query: string): void {
    this.searchQuery = query;
    this.searchSubject.next(query);
  }

  onSearchEnter(): void {
    const q = this.searchQuery.trim();
    if (q.length < 2) return;

    if (this.searchResults.length > 0) {
      this.navigateToResult(this.searchResults[0]);
      return;
    }

    // If still loading, wait briefly for results then navigate
    if (this.searchLoading) {
      this.globalSearch.search(q, { menu: this.menu ?? this.latestMenu }).subscribe(results => {
        if (results.length) this.navigateToResult(results[0]);
      });
    }
  }

  onSearchFocus(): void {
    if (this.searchQuery.trim().length >= 2) {
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
    const queryParams: Record<string, string | undefined> = { ...tree.queryParams };

    const candidates = this.latestMenu
      ? [
          ...this.latestMenu.groups.flatMap(g => g.items),
          ...this.latestMenu.standaloneItems
        ]
      : [];

    return isNavItemActive(
      item,
      normalizedPath,
      queryParams,
      candidates,
      this.aliasItemIds
    );
  }

  isGroupActive(group: NavGroup): boolean {
    return group.items.some(item => this.isItemActive(item));
  }

  navigateToResult(result: SearchResult): void {
    this.showSearchResults = false;
    this.searchQuery = '';
    this.searchResults = [];
    void this.router.navigate([result.route], {
      queryParams: result.queryParams ?? undefined
    });
  }

  getResultTypeLabel(type: string): string {
    const labels: Record<string, string> = {
      module: 'Page',
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

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.notifPanelOpen) {
      this.closeNotifPanel();
    }
  }

  toggleNotifPanel(event?: Event): void {
    event?.stopPropagation();
    if (this.notifPanelOpen) {
      this.closeNotifPanel();
      return;
    }
    this.notifPanelOpen = true;
    this.notifications$.pipe(take(1)).subscribe(list => {
      const firstUnread = list.find(n => !n.isRead);
      this.expandedNotifId = (firstUnread ?? list[0])?.id ?? null;
    });
  }

  closeNotifPanel(): void {
    this.notifPanelOpen = false;
  }

  expandNotif(n: Notification): void {
    this.expandedNotifId = n.id;
  }

  displayTitle(n: Notification): string {
    return this.previewMessage(n.title) || n.title || 'Notification';
  }

  channelLabel(channel?: string | null): string {
    if (!channel) return '';
    if (channel === 'InApp') return 'In-App';
    if (channel === 'Sms') return 'SMS';
    return channel;
  }

  priorityLabel(n: Notification): string {
    return NotificationPriorityLabels[n.priority ?? 2] ?? 'Normal';
  }

  priorityBadgeClass(n: Notification): string {
    switch (n.priority ?? 2) {
      case 1: return 'notif-badge--low';
      case 3: return 'notif-badge--high';
      case 4: return 'notif-badge--critical';
      default: return 'notif-badge--normal';
    }
  }

  notifMeta(n: Notification): NotifMetaRow[] | null {
    const rows: NotifMetaRow[] = [];
    const plain = this.previewMessage(n.message);
    const pick = (label: string, icon: string) => {
      const re = new RegExp(`${label}\\s*[:\\-]\\s*(.+)`, 'i');
      const m = plain.match(re);
      if (m?.[1]) {
        rows.push({ icon, label, value: m[1].split(/[.|•]/)[0].trim() });
      }
    };
    pick('Vehicle', 'directions_car');
    pick('Driver', 'person');
    pick('Location', 'place');
    pick('Address', 'place');

    rows.push({
      icon: 'schedule',
      label: 'Time',
      value: new Date(n.createdAt).toLocaleString(undefined, {
        year: 'numeric', month: 'long', day: 'numeric',
        hour: 'numeric', minute: '2-digit'
      })
    });

    const priority = this.priorityLabel(n);
    rows.push({
      icon: 'flag',
      label: 'Priority',
      value: priority,
      critical: (n.priority ?? 2) >= 4
    });

    return rows.length ? rows : null;
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

  openNotificationCenter(event: Event): void {
    event.stopPropagation();
    void this.router.navigate(['/notifications']);
  }

  viewNotificationDetails(n: Notification, event: Event): void {
    event.stopPropagation();
    this.closeNotifPanel();
    if (!n.isRead) {
      this.notificationService.markAsRead([n.id]).subscribe();
    }
    void this.router.navigate(['/notifications'], { queryParams: { id: n.id } });
  }

  onNotificationClick(n: Notification): void {
    this.expandNotif(n);
    if (!n.isRead) {
      this.notificationService.markAsRead([n.id]).subscribe();
    }
  }

  previewMessage(message?: string | null): string {
    if (!message) return '';
    const plain = message
      .replace(/<[^>]+>/g, ' ')
      .replace(/\{\{\s*[\w.]+\s*\}\}/g, ' ')
      .replace(/&nbsp;/gi, ' ')
      .replace(/&amp;/gi, '&')
      .replace(/&lt;/gi, '<')
      .replace(/&gt;/gi, '>')
      .replace(/\s+/g, ' ')
      .trim();
    return plain;
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
