import { AfterViewInit, Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { NgClass } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { MatIconModule } from '@angular/material/icon';
import { WebsiteSeoService } from '../../core/seo.service';
import { WEBSITE_BRAND } from '../../core/brand';
import { WebsitePublicContentService } from '../../core/website-public-content.service';
import { WebsiteI18nService } from '../../core/website-i18n.service';
import { NEXUS_MODULE_SHOTS } from '../../core/site-catalog';
import { NEWS_ANNOUNCEMENTS } from '../../core/news-items';
import { WebsiteTranslatePipe } from '../../shared/website-translate.pipe';
import { NexusDemoModalComponent } from '../../shared/nexus-demo-modal/nexus-demo-modal.component';

@Component({
  selector: 'app-home-page',
  standalone: true,
  imports: [RouterLink, NgClass, MatIconModule, NexusDemoModalComponent, WebsiteTranslatePipe],
  templateUrl: './home.page.html',
  styleUrl: './home.page.scss',
})
export class HomePage implements OnInit, AfterViewInit, OnDestroy {
  readonly brand = WEBSITE_BRAND;
  readonly i18n = inject(WebsiteI18nService);
  private readonly seo = inject(WebsiteSeoService);
  private readonly content = inject(WebsitePublicContentService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private fragmentSub?: Subscription;

  nexusImageError = false;
  readonly demoModalOpen = signal(false);
  readonly newsPreview = NEWS_ANNOUNCEMENTS.slice(0, 4);

  readonly heroPerks = computed(() => {
    this.i18n.lang();
    return [
      { icon: 'my_location', text: this.i18n.t('home.perk.gps') },
      { icon: 'auto_awesome', text: this.i18n.t('home.perk.ai') },
      { icon: 'shield', text: this.i18n.t('home.perk.secure') },
      { icon: 'hub', text: this.i18n.t('home.perk.nexus') },
    ] as const;
  });

  readonly pillLinks = computed(() => {
    this.i18n.lang();
    return [
      { label: this.i18n.t('home.pill.dashboard'), fragment: 'see-the-platform', icon: 'dashboard' },
      { label: this.i18n.t('home.pill.modules'), fragment: 'platform-modules', icon: 'view_module' },
      { label: this.i18n.t('home.pill.clientBilling'), fragment: 'client-billing', icon: 'receipt_long' },
      { label: this.i18n.t('home.pill.driverPayments'), fragment: 'driver-payments', icon: 'account_balance_wallet' },
      { label: this.i18n.t('home.pill.smartAssignment'), fragment: 'smart-assignment', icon: 'auto_awesome' },
      { label: this.i18n.t('home.pill.driverVoice'), fragment: 'driver-voice', icon: 'mic' },
      { label: this.i18n.t('home.pill.driverOffline'), fragment: 'driver-offline-sync', icon: 'sync_disabled' },
      { label: this.i18n.t('home.pill.capabilities'), fragment: 'platform-features', icon: 'grid_view' },
      { label: this.i18n.t('home.pill.ai'), link: '/ai-solutions', icon: 'auto_awesome' },
      { label: this.i18n.t('home.pill.pricing'), link: '/pricing', icon: 'payments' },
      { label: this.i18n.t('home.pill.nexus'), link: '/services', icon: 'hub' },
      { label: this.i18n.t('home.pill.demo'), fragment: 'cta', icon: 'rocket_launch' },
    ];
  });

  readonly nexusCallouts = computed(() => {
    this.i18n.lang();
    return [
      { title: this.i18n.t('home.callout.liveGps.title'), text: this.i18n.t('home.callout.liveGps.text'), icon: 'gps_fixed' },
      { title: this.i18n.t('home.callout.ai.title'), text: this.i18n.t('home.callout.ai.text'), icon: 'smart_toy' },
      { title: this.i18n.t('home.callout.geo.title'), text: this.i18n.t('home.callout.geo.text'), icon: 'fence' },
      { title: this.i18n.t('home.callout.alerts.title'), text: this.i18n.t('home.callout.alerts.text'), icon: 'notifications_active' },
    ] as const;
  });

  readonly platformCapabilities = computed(() => {
    this.i18n.lang();
    const voice = {
      title: this.i18n.t('home.capability.voice.title'),
      text: this.i18n.t('home.capability.voice.text'),
      icon: 'mic' as const,
      link: '/',
      linkLabel: this.i18n.t('home.pill.driverVoice'),
    };
    const offlineDriver = {
      title: this.i18n.t('home.capability.offlineDriver.title'),
      text: this.i18n.t('home.capability.offlineDriver.text'),
      icon: 'cloud_off' as const,
      link: '/',
      linkLabel: this.i18n.t('home.pill.driverOffline'),
    };
    return [
      ...this._platformCapabilitiesBase,
      voice,
      offlineDriver,
    ] as const;
  });

  private readonly _platformCapabilitiesBase = [
    {
      title: 'GPS Tracking',
      text: 'Device registry, live positions, and operational map views tied to your fleet.',
      icon: 'gps_fixed',
      link: '/gps-tracking',
      linkLabel: 'GPS Tracking',
    },
    {
      title: 'Live Tracking',
      text: 'Follow units in motion with speed, ignition, and connectivity at a glance.',
      icon: 'my_location',
      link: '/live-tracking',
      linkLabel: 'Live map',
    },
    {
      title: 'Geofencing',
      text: 'Office, depot, and customer zones with alerts when boundaries are crossed.',
      icon: 'fence',
      link: '/gps-tracking',
      linkLabel: 'Geofences',
    },
    {
      title: 'Playback & History',
      text: 'Replay routes with stops and timestamps for audits, disputes, and coaching.',
      icon: 'route',
      link: '/gps-tracking',
      linkLabel: 'Trip history',
    },
    {
      title: 'Offline Log Sync',
      text: 'Recover trips and positions when devices reconnect — fewer silent data gaps.',
      icon: 'sync',
      link: '/fleet-management',
      linkLabel: 'Fleet platform',
    },
    {
      title: 'Fleet Intelligence',
      text: 'Health scores, digests, and heuristic recommendations you can explain to stakeholders.',
      icon: 'insights',
      link: '/fleet-management',
      linkLabel: 'Intelligence',
    },
    {
      title: 'Vehicle Health Alerts',
      text: 'Speeding, geofence, fuel, panic, and device events routed to the right people.',
      icon: 'warning',
      link: '/gps-alerts',
      linkLabel: 'GPS alerts',
    },
    {
      title: 'AI Solutions',
      text: 'Assistants and copilots that query live GPS and Nexus tools — optional LLM when configured.',
      icon: 'auto_awesome',
      link: '/ai-solutions',
      linkLabel: 'AI Solutions',
    },
    {
      title: 'SheikhGo Nexus',
      text: 'Vehicles, drivers, fuel, maintenance, bookings, reports, and tenant-ready operations.',
      icon: 'business',
      link: WEBSITE_BRAND.loginPath,
      linkLabel: 'Sign in',
    },
    {
      title: 'NEXUS Dashboard',
      text: 'Unified fleet intelligence UI — map, alerts, modules, and AI panel in one workspace.',
      icon: 'dashboard',
      link: '/',
      linkLabel: 'See showcase',
    },
  ] as const;

  readonly nexusModuleShots = NEXUS_MODULE_SHOTS;

  readonly clientTestimonials = [
    {
      name: 'Sara Khalil',
      role: 'Operations Director',
      company: 'Gulf Line Transport',
      initials: 'SK',
      quote:
        'We replaced three spreadsheets and a dispatch WhatsApp thread with one Nexus workspace. Live GPS, trip status, and fuel receipts sit on the same screen — our morning stand-ups are half as long.',
    },
    {
      name: 'James Okonkwo',
      role: 'Fleet Manager',
      company: 'MetroCoach Rentals',
      initials: 'JO',
      quote:
        'Geofence alerts and trip playback cut our “where is the bus?” calls dramatically. Maintenance due dates tied to odometer readings finally match what drivers actually run.',
    },
    {
      name: 'Fatima Al-Rashid',
      role: 'CEO',
      company: 'Desert Wings Logistics',
      initials: 'FA',
      quote:
        'Multi-branch visibility without exporting CSVs every night. Bookings, vehicle assignments, and GPS health in one tenant — our auditors get clear trails when they ask.',
    },
    {
      name: 'Daniel Meyer',
      role: 'Head of Dispatch',
      company: 'Alpine Shuttle Co.',
      initials: 'DM',
      quote:
        'The AI assistant answers from our live map and alert feed, not generic chatbot fluff. Drivers still use our process; dispatch just spends less time hunting data.',
    },
  ] as const;

  readonly aiFeatures = [
    'AI Assistant & chat gateway',
    'Nexus AI Management Center',
    'Fleet health & morning digest',
    'Heuristic recommendations',
    'WhatsApp AI Assist (human-approved)',
    'Driver & fleet mobile copilot',
  ] as const;

  readonly aiSpotlightCards = [
    {
      title: 'Tool-first assistant',
      text: 'Structured answers from trips, fuel, alerts, and live map data.',
      icon: 'support_agent',
    },
    {
      title: 'Connected insights',
      text: 'Vehicles, drivers, and operations records in one explainable view.',
      icon: 'hub',
    },
  ] as const;

  ngOnInit(): void {
    this.seo.set(
      'SheikhGo Nexus | Fleet Intelligence, GPS & AI',
      'SheikhGo NEXUS — live GPS tracking, geofencing, playback, offline sync, fleet health alerts, Nexus operations, and honest AI assistants for transport teams.',
      '/',
    );

    this.content.getHome().subscribe(home => {
      if (!home?.settings) return;
      if (home.settings.defaultMetaTitle || home.settings.defaultMetaDescription) {
        this.seo.set(
          home.settings.defaultMetaTitle ||
            'SheikhGo Nexus | Fleet Intelligence, GPS & AI',
          home.settings.defaultMetaDescription ||
            'SheikhGo NEXUS — live GPS tracking, geofencing, playback, offline sync, fleet health alerts, Nexus operations, and honest AI assistants for transport teams.',
        );
      }
    });
  }

  openDemoModal(): void {
    this.demoModalOpen.set(true);
  }

  closeDemoModal(): void {
    this.demoModalOpen.set(false);
    if (this.route.snapshot.fragment === 'watch-demo') {
      void this.router.navigate([], { fragment: undefined, replaceUrl: true });
    }
  }

  ngAfterViewInit(): void {
    this.fragmentSub = this.route.fragment.subscribe(fragment => {
      if (fragment === 'ai-solutions' || fragment === 'home-ai') {
        void this.router.navigate(['/ai-solutions'], { replaceUrl: true });
        return;
      }
      if (fragment === 'watch-demo') {
        this.demoModalOpen.set(true);
      }
    });
  }

  ngOnDestroy(): void {
    this.fragmentSub?.unsubscribe();
  }
}
