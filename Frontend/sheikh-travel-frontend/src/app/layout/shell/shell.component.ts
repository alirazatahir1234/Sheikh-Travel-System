import { Component, OnInit, OnDestroy } from '@angular/core';
import { Router } from '@angular/router';
import { Subject, debounceTime, distinctUntilChanged, switchMap, of, Observable, Subscription, map } from 'rxjs';
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

interface NavItem {
  label: string;
  icon: string;
  route: string;
  adminOnly?: boolean;
}

@Component({
  selector: 'app-shell',
  templateUrl: './shell.component.html',
  styleUrls: ['./shell.component.scss']
})
export class ShellComponent implements OnInit, OnDestroy {
  private allNavItems: NavItem[] = [
    { label: 'Dashboard', icon: 'dashboard',      route: '/dashboard' },
    { label: 'Bookings',  icon: 'confirmation_number', route: '/bookings' },
    { label: 'Vehicles',  icon: 'directions_bus', route: '/vehicles' },
    { label: 'Drivers',   icon: 'badge',          route: '/drivers' },
    { label: 'Customers', icon: 'group',          route: '/customers' },
    { label: 'Routes',    icon: 'alt_route',      route: '/routes' },
    { label: 'Fuel Logs', icon: 'local_gas_station', route: '/fuel-logs' },
    { label: 'Maintenance', icon: 'build',        route: '/maintenance' },
    { label: 'Tracking',  icon: 'my_location',    route: '/tracking' },
    { label: 'Payments',  icon: 'account_balance_wallet', route: '/payments' },
    { label: 'Reports',   icon: 'insights',       route: '/reports' },
    { label: 'Allowance Rules', icon: 'rule',     route: '/driver-allowance-rules', adminOnly: true },
    { label: 'Users',     icon: 'manage_accounts', route: '/users', adminOnly: true },
    { label: 'Audit Logs', icon: 'history',       route: '/audit-logs', adminOnly: true },
  ];

  navItems$!: Observable<NavItem[]>;
  currentUser$: AuthService['currentUser$'];
  unreadCount$!: NotificationService['unreadCount'];
  notifications$!: NotificationService['notifications'];

  // Global search
  searchQuery = '';
  searchResults: SearchResult[] = [];
  searchLoading = false;
  showSearchResults = false;
  isSidebarPinned = false;
  isSidebarHovering = false;
  private searchSubject = new Subject<string>();
  private searchSub?: Subscription;
  private sessionSub?: Subscription;
  timeDisplay$: Observable<LocalTimeDisplay>;

  constructor(
    private auth: AuthService,
    private notificationService: NotificationService,
    private globalSearch: GlobalSearchService,
    private localTime: LocalTimeContextService,
    private router: Router,
    private dialog: MatDialog
  ) {
    this.currentUser$ = auth.currentUser$;
    this.unreadCount$ = notificationService.unreadCount;
    this.notifications$ = notificationService.notifications;
    this.timeDisplay$ = this.localTime.clockDisplay$();

    // Filter nav items based on user role
    this.navItems$ = this.currentUser$.pipe(
      map(user => {
        const isAdmin = user?.roles?.includes('Admin') ?? false;
        return this.allNavItems.filter(item => !item.adminOnly || isAdmin);
      })
    );
  }

  ngOnInit(): void {
    this.sessionSub = this.auth.currentUser$.subscribe(user => {
      if (user && this.auth.getToken()) {
        queueMicrotask(() => this.notificationService.startPolling(60000));
      } else {
        this.notificationService.reset();
      }
    });
    this.initSearch();
  }

  ngOnDestroy(): void {
    this.sessionSub?.unsubscribe();
    this.searchSub?.unsubscribe();
    this.notificationService.reset();
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
    return this.isSidebarPinned || this.isSidebarHovering;
  }

  onSidebarEnter(): void {
    this.isSidebarHovering = true;
  }

  onSidebarLeave(): void {
    this.isSidebarHovering = false;
  }

  toggleSidebarPin(): void {
    this.isSidebarPinned = !this.isSidebarPinned;
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

  trackByRoute(_i: number, item: NavItem): string { return item.route; }
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
