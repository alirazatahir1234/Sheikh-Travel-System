import { AfterViewInit, Component, OnDestroy, OnInit, inject } from '@angular/core';
import { NgClass } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { MatIconModule } from '@angular/material/icon';
import { WebsiteSeoService } from '../../core/seo.service';
import { WEBSITE_BRAND } from '../../core/brand';
import { WebsitePublicContentService } from '../../core/website-public-content.service';
import { SERVICE_ITEMS } from '../../core/site-catalog';

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
    { icon: 'shield', text: 'Secure & scalable ERP' },
    { icon: 'hub', text: 'All-in-one NEXUS dashboard' },
  ] as const;

  readonly pillLinks = [
    { label: 'NEXUS Dashboard', fragment: 'see-the-platform', icon: 'dashboard' },
    { label: 'GPS Tracking', fragment: 'platform-features', icon: 'gps_fixed' },
    { label: 'Live Map', fragment: 'platform-features', icon: 'map' },
    { label: 'Geofencing', fragment: 'platform-features', icon: 'fence' },
    { label: 'AI Solutions', fragment: 'home-ai', icon: 'auto_awesome' },
    { label: 'ERP', fragment: 'erp-ai', icon: 'business' },
    { label: 'Services', fragment: 'services', icon: 'code' },
    { label: 'Request Demo', fragment: 'cta', icon: 'rocket_launch' },
  ] as const;

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
      text: 'Assistants and copilots that query live GPS and ERP tools — optional LLM when configured.',
      icon: 'auto_awesome',
      link: '/ai-solutions',
      linkLabel: 'AI Solutions',
    },
    {
      title: 'SheikhGo ERP',
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

  readonly erpPillars = [
    {
      title: 'SheikhGo ERP operations',
      text: 'Multi-tenant fleet and travel operations with the modules transport teams use every day.',
      icon: 'domain',
      link: WEBSITE_BRAND.loginPath,
      linkLabel: 'Open ERP',
      items: [
        'Vehicles, drivers & trips',
        'Fuel & maintenance workflows',
        'Bookings & customer records',
        'Reports & audit trails',
      ],
    },
    {
      title: 'Fleet intelligence layer',
      text: 'Transparent scores and digests on top of GPS and ERP — no black-box “AI magic.”',
      icon: 'monitoring',
      link: '/ai-solutions',
      linkLabel: 'AI Solutions',
      items: [
        'Morning digests & health views',
        'AI assistant via fleet tools',
        'WhatsApp drafts (human-approved)',
        'Mobile copilot experiences',
      ],
    },
  ] as const;

  readonly aiFeatures = [
    'AI Assistant & chat gateway',
    'ERP AI Management Center',
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
      text: 'Vehicles, drivers, and ERP records in one explainable view.',
      icon: 'hub',
    },
  ] as const;

  readonly trustedTech = [
    'Microsoft',
    'Azure',
    'Google Cloud',
    'Flutter',
    'Shopify',
    'Figma',
  ] as const;

  readonly services = SERVICE_ITEMS;

  ngOnInit(): void {
    this.seo.set(
      'SheikhGo | Fleet Intelligence, GPS, ERP & AI',
      'SheikhGo NEXUS — live GPS tracking, geofencing, playback, offline sync, fleet health alerts, ERP operations, and honest AI assistants for transport teams.',
      '/',
    );

    this.content.getHome().subscribe(home => {
      if (!home?.settings) return;
      if (home.settings.defaultMetaTitle || home.settings.defaultMetaDescription) {
        this.seo.set(
          home.settings.defaultMetaTitle ||
            'SheikhGo | Fleet Intelligence, GPS, ERP & AI',
          home.settings.defaultMetaDescription ||
            'SheikhGo NEXUS — live GPS tracking, geofencing, playback, offline sync, fleet health alerts, ERP operations, and honest AI assistants for transport teams.',
        );
      }
    });
  }

  ngAfterViewInit(): void {
    this.fragmentSub = this.route.fragment.subscribe(fragment => {
      if (fragment === 'ai-solutions') {
        void this.router.navigate(['/ai-solutions'], { replaceUrl: true });
      }
    });
  }

  ngOnDestroy(): void {
    this.fragmentSub?.unsubscribe();
  }
}
