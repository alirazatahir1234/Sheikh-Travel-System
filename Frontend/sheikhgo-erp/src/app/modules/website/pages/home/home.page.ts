import { AfterViewInit, Component, OnDestroy, OnInit, inject } from '@angular/core';
import { NgClass } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { MatIconModule } from '@angular/material/icon';
import { WebsiteSeoService } from '../../core/seo.service';
import { WEBSITE_BRAND } from '../../core/brand';
import { WebsitePublicContentService } from '../../core/website-public-content.service';

@Component({
  selector: 'app-home-page',
  standalone: true,
  imports: [RouterLink, NgClass, MatIconModule],
  templateUrl: './home.page.html',
  styleUrl: './home.page.scss',
})
export class HomePage implements OnInit, AfterViewInit, OnDestroy {
  readonly brand = WEBSITE_BRAND;
  private readonly seo = inject(WebsiteSeoService);
  private readonly content = inject(WebsitePublicContentService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private fragmentSub?: Subscription;

  nexusImageError = false;

  readonly heroPerks = [
    { icon: 'my_location', text: 'Real-time GPS tracking' },
    { icon: 'auto_awesome', text: 'Tool-first AI insights' },
    { icon: 'shield', text: 'Secure & scalable platform' },
    { icon: 'hub', text: 'All-in-one NEXUS dashboard' },
  ] as const;

  readonly pillLinks: {
    label: string;
    icon: string;
    fragment?: string;
    link?: string;
  }[] = [
    { label: 'NEXUS Dashboard', fragment: 'see-the-platform', icon: 'dashboard' },
    { label: 'Platform modules', fragment: 'platform-modules', icon: 'view_module' },
    { label: 'Capabilities', fragment: 'platform-features', icon: 'grid_view' },
    { label: 'AI Solutions', link: '/ai-solutions', icon: 'auto_awesome' },
    { label: 'Services', link: '/services', icon: 'code' },
    { label: 'Request Demo', fragment: 'cta', icon: 'rocket_launch' },
  ];

  readonly nexusCallouts = [
    { title: 'Live GPS Tracking', text: 'Map, speed, trip state', icon: 'gps_fixed' },
    { title: 'AI Assistant', text: 'Plain-language fleet Q&A', icon: 'smart_toy' },
    { title: 'Geofencing', text: 'Zone entry & exit alerts', icon: 'fence' },
    { title: 'GPS Alerts', text: 'Speed, health & device events', icon: 'notifications_active' },
  ] as const;

  readonly platformCapabilities = [
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

  /** Product UI captures from SheikhGo Nexus ERP (local demo tenant). */
  readonly nexusModuleShots = [
    { caption: 'Dashboard', src: '/website/nexus/modules/dashboard.png', alt: 'SheikhGo Nexus operations dashboard' },
    { caption: 'Live Tracking', src: '/website/nexus/modules/live-tracking.png', alt: 'Live GPS fleet map in SheikhGo Nexus' },
    { caption: 'GPS Alerts', src: '/website/nexus/modules/gps-alerts.png', alt: 'GPS alerts inbox in SheikhGo Nexus' },
    { caption: 'Geofencing', src: '/website/nexus/modules/geofencing.png', alt: 'Geofence editor in SheikhGo Nexus' },
    { caption: 'Trip History', src: '/website/nexus/modules/trip-history.png', alt: 'GPS trip history and playback in SheikhGo Nexus' },
    { caption: 'Vehicles', src: '/website/nexus/modules/vehicles.png', alt: 'Vehicle registry in SheikhGo Nexus' },
    { caption: 'Drivers', src: '/website/nexus/modules/drivers.png', alt: 'Driver management in SheikhGo Nexus' },
    { caption: 'Trips', src: '/website/nexus/modules/trips.png', alt: 'Trip operations in SheikhGo Nexus' },
    { caption: 'Fuel', src: '/website/nexus/modules/fuel.png', alt: 'Fuel logs in SheikhGo Nexus' },
    { caption: 'Maintenance', src: '/website/nexus/modules/maintenance.png', alt: 'Fleet maintenance in SheikhGo Nexus' },
    { caption: 'Reports', src: '/website/nexus/modules/reports.png', alt: 'Operational reports in SheikhGo Nexus' },
    { caption: 'AI Assistant', src: '/website/nexus/modules/ai-assistant.png', alt: 'SheikhGo AI assistant in Nexus' },
    { caption: 'Customers', src: '/website/nexus/modules/customers.png', alt: 'Customer records in SheikhGo Nexus' },
    { caption: 'Notifications', src: '/website/nexus/modules/notifications.png', alt: 'Notification center in SheikhGo Nexus' },
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

  ngAfterViewInit(): void {
    this.fragmentSub = this.route.fragment.subscribe(fragment => {
      if (fragment === 'ai-solutions' || fragment === 'home-ai') {
        void this.router.navigate(['/ai-solutions'], { replaceUrl: true });
      }
    });
  }

  ngOnDestroy(): void {
    this.fragmentSub?.unsubscribe();
  }
}
