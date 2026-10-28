import { Component, OnInit, OnDestroy, ViewChild, ElementRef, HostListener } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { NavigationEnd, Router } from '@angular/router';
import { Subject, BehaviorSubject, debounceTime, distinctUntilChanged, switchMap, of, Observable, Subscription, map, filter, combineLatest, startWith, tap, finalize, shareReplay, catchError, take } from 'rxjs';
import { MatDialog } from '@angular/material/dialog';
import { AuthService } from '../../core/services/auth.service';
import { NotificationService } from '../../core/services/notification.service';
import { SmartAlertService } from '../../core/services/smart-alert.service';
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
import { APP_PRODUCT_NAME, APP_PRODUCT_TAGLINE, APP_SIDEBAR_LOGO_PATH } from '../../core/constants/app-brand';
import { environment } from '../../../environments/environment';
import { AiAssistantOpenSoundService } from '../../modules/ai/services/ai-assistant-open-sound.service';
import { AI_ASSISTANT_UI } from '../../modules/ai/shared/ai-assistant-ui.constants';

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
  readonly appProductTagline = APP_PRODUCT_TAGLINE;
  readonly appLogoPath = APP_SIDEBAR_LOGO_PATH;
  readonly aiUi = AI_ASSISTANT_UI;
  private readonly sidebarPinnedStorageKey = 'stb_sidebar_pinned';
  private readonly enabledModules$ = new BehaviorSubject<string[]>([]);
  private latestMenu?: ResolvedMenu;

  menu$!: Observable<ResolvedMenu>;
  currentUser$: AuthService['currentUser$'];
  unreadCount$!: NotificationService['unreadCount'];
  notifications$!: NotificationService['notifications'];
  smartAlertNewCount$!: SmartAlertService['newCount'];
  /** Unread inbox + new Smart Ops alerts for the top-bar bell badge. */
  bellBadgeCount$!: Observable<number>;
  notifPanelOpen = false;
  /** WhatsApp-style SheikhGo AI Assistant slide-over from the floating bubble. */
  aiChatOpen = false;
  aiAssistantAllowed = false;
  expandedNotifId: number | null = null;
  /** Stable meta rows for the expanded card — never rebuild during change detection. */
  expandedMetaRows: NotifMetaRow[] = [];
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
    'roles-permissions', 'system-configuration'
  ]);
  timeDisplay$: Observable<LocalTimeDisplay>;

  constructor(
    private auth: AuthService,
    private notificationService: NotificationService,
    private smartAlertService: SmartAlertService,
    private globalSearch: GlobalSearchService,
    private localTime: LocalTimeContextService,
    private router: Router,
    private dialog: MatDialog,
    private tenantConfig: TenantConfigService,
    private menuService: MenuService,
    private http: HttpClient,
    private aiAssistantOpenSound: AiAssistantOpenSoundService
  ) {
    this.currentUser$ = auth.currentUser$;
    this.unreadCount$ = notificationService.unreadCount;
    this.notifications$ = notificationService.notifications;
    this.smartAlertNewCount$ = smartAlertService.newCount;
    this.bellBadgeCount$ = combineLatest([
      this.unreadCount$.pipe(startWith(0)),
      this.smartAlertNewCount$.pipe(startWith(0))
    ]).pipe(map(([a, b]) => (a || 0) + (b || 0)));
    this.timeDisplay$ = this.localTime.clockDisplay$();

    this.menu$ = combineLatest([this.currentUser$, this.enabledModules$]).pipe(
      tap(() => { this.menuLoading = true; }),
      // switchMap so branding enabledModules (incl. ai) is not dropped while menus/me is in flight.
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
      this.refreshAiAssistantGate(b?.enabledModules ?? []);
      // Company timezone wins over user preference for display clocks.
      this.localTime.setCompanyTimeZone(b?.timeZone);
    });

    this.sessionSub = this.auth.currentUser$.subscribe(user => {
      if (user && this.auth.getToken()) {
        queueMicrotask(() => {
          this.notificationService.startPolling(120000);
          void this.notificationService.requestBrowserPermission();
          this.smartAlertService.refresh();
          this.loadUserTimeZonePreference();
          this.refreshAiAssistantGate(this.enabledModules$.value);
        });
      } else {
        this.notificationService.reset();
        this.localTime.setUserTimeZone(null);
        this.aiAssistantAllowed = false;
        this.aiChatOpen = false;
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
      // Full AI pages already host chat — close the float panel to avoid double UI.
      if (this.isAiRoute(this.router.url)) {
        this.aiChatOpen = false;
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
    return typeof window !== 'undefined' && window.matchMedia('(max-width: 767px)').matches;
  }

  /** Tablet / compact: default collapsed so maps/content keep horizontal room. */
  private isCompactViewport(): boolean {
    return typeof window !== 'undefined'
      && window.matchMedia('(min-width: 768px) and (max-width: 1199px)').matches;
  }

  toggleSidebarPin(event?: Event): void {
    event?.preventDefault();
    event?.stopPropagation();
    this.isSidebarPinned = !this.isSidebarPinned;
    // Collapse immediately on unpin (do not wait for mouseleave / hover).
    this.isSidebarHovering = this.isSidebarPinned;
    localStorage.setItem(this.sidebarPinnedStorageKey, String(this.isSidebarPinned));
  }

  private readSidebarPinnedPreference(): boolean {
    const stored = localStorage.getItem(this.sidebarPinnedStorageKey);
    if (stored === null) {
      // Desktop (>=1200): pinned/expanded. Tablet: compact by default.
      return !this.isCompactViewport() && !this.isMobileViewport();
    }
    return stored === 'true';
  }

  /** Flatten visible leaf items for icon-only collapsed rail. */
  collapsedLeafItems(menu: ResolvedMenu): NavItem[] {
    if (menu.isDriverLayout) {
      return menu.standaloneItems;
    }
    const seen = new Set<string>();
    const leaves: NavItem[] = [];
    for (const group of menu.groups) {
      for (const item of group.items) {
        if (seen.has(item.id)) continue;
        seen.add(item.id);
        leaves.push(item);
      }
    }
    return leaves;
  }

  onNavLinkClick(): void {
    this.closeMobileNav();
  }

  /**
   * Navigate sidebar leaves via segment commands (not `navigate([fullPath])`),
   * so `/gps-tracking/driver-performance` resolves as two segments.
   * Modifier/middle-clicks keep default `<a routerLink>` behaviour (new tab).
   */
  navigateNavItem(item: NavItem, event?: Event): void {
    if (event instanceof MouseEvent && (event.metaKey || event.ctrlKey || event.shiftKey || event.button === 1)) {
      this.closeMobileNav();
      return;
    }
    event?.preventDefault();
    if (!item?.route) return;
    void this.router.navigate(this.routeCommands(item.route), {
      queryParams: item.queryParams
    });
    this.closeMobileNav();
  }

  /** `/gps-tracking/driver-performance` → `['/gps-tracking', 'driver-performance']` */
  private routeCommands(route: string): string[] {
    const path = (route || '').split('?')[0];
    const segments = path.split('/').filter(Boolean);
    if (!segments.length) return ['/'];
    return ['/' + segments[0], ...segments.slice(1)];
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
      void this.router.navigate(this.routeCommands(primary.route), {
        queryParams: primary.queryParams
      });
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
    void this.router.navigate(this.routeCommands(result.route), {
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

  /** Floating bubble: AI module on + permission/admin, and not already on /ai. */
  get showAiAssistantFab(): boolean {
    if (!this.aiAssistantAllowed || this.aiChatOpen) return false;
    if (this.isDriverUser) return false;
    return !this.isAiRoute(this.router.url);
  }

  openAiChat(event?: Event): void {
    event?.stopPropagation();
    this.closeNotifPanel();
    this.aiAssistantOpenSound.notifyAssistantOpened();
    this.aiChatOpen = true;
  }

  closeAiChat(): void {
    this.aiChatOpen = false;
  }

  openAiChatFullPage(): void {
    this.aiChatOpen = false;
    void this.router.navigate(['/ai/chat']);
  }

  private refreshAiAssistantGate(enabledModules: string[]): void {
    this.aiAssistantAllowed = this.menuService.canAccessAiAssistant(enabledModules);
    if (!this.aiAssistantAllowed) {
      this.aiChatOpen = false;
    }
  }

  private isAiRoute(url: string): boolean {
    const path = (url || '').split('?')[0];
    return path === '/ai' || path.startsWith('/ai/');
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.aiChatOpen) {
      this.closeAiChat();
      return;
    }
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
    this.closeAiChat();
    this.notifPanelOpen = true;
    this.notifications$.pipe(take(1)).subscribe(list => {
      const first = list.find(n => !n.isRead) ?? list[0];
      if (first) {
        this.expandNotif(first);
      } else {
        this.expandedNotifId = null;
        this.expandedMetaRows = [];
      }
    });
  }

  closeNotifPanel(): void {
    this.notifPanelOpen = false;
  }

  expandNotif(n: Notification): void {
    this.expandedNotifId = n.id;
    // Compute once on expand. Calling notifMeta() from the template returned a new
    // array every CD cycle; *ngFor then tore down/recreated mat-icons → main-thread freeze.
    this.expandedMetaRows = this.buildNotifMeta(n);
  }

  trackByMetaLabel(_i: number, row: NotifMetaRow): string {
    return row.label;
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

  private buildNotifMeta(n: Notification): NotifMetaRow[] {
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

    return rows;
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
    this.closeMobileNav();
    this.dialog.open(HelpDialogComponent, {
      width: '96vw',
      maxWidth: '1240px',
      maxHeight: '90vh',
      panelClass: 'stb-help-dialog-panel',
      autoFocus: 'dialog'
    });
  }

  localTimeTooltip(t: LocalTimeDisplay): string {
    return [t.timeZoneId, t.dateLine, t.offsetAndAbbr].filter(Boolean).join(' · ');
  }

  /** User profile timezone is a fallback when the company has no TenantBranding.TimeZone. */
  private loadUserTimeZonePreference(): void {
    this.http
      .get<{ timeZone?: string | null }>(`${environment.apiUrl}/users/profile`)
      .pipe(take(1), catchError(() => of(null)))
      .subscribe(profile => this.localTime.setUserTimeZone(profile?.timeZone));
  }
}
