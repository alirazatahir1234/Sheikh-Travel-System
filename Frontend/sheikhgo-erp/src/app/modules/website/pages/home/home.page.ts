import { Component, HostListener, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CtaBandComponent } from '../../shared/cta-band.component';
import { WebsiteSeoService } from '../../core/seo.service';
import { WEBSITE_BRAND } from '../../core/brand';
import {
  PublicWebsiteFeature,
  PublicWebsiteSection,
  WebsitePublicContentService,
} from '../../core/website-public-content.service';

@Component({
  selector: 'app-home-page',
  standalone: true,
  imports: [RouterLink, CtaBandComponent],
  templateUrl: './home.page.html',
  styleUrl: './home.page.scss',
})
export class HomePage implements OnInit, OnDestroy {
  readonly brand = WEBSITE_BRAND;
  private readonly seo = inject(WebsiteSeoService);
  private readonly content = inject(WebsitePublicContentService);

  hero: PublicWebsiteSection | null = null;
  featuresSection: PublicWebsiteSection | null = null;
  trackingSection: PublicWebsiteSection | null = null;
  dashboardSection: PublicWebsiteSection | null = null;
  playbackSection: PublicWebsiteSection | null = null;
  alertsSection: PublicWebsiteSection | null = null;
  reportsSection: PublicWebsiteSection | null = null;
  aiSection: PublicWebsiteSection | null = null;
  integrationsSection: PublicWebsiteSection | null = null;
  securitySection: PublicWebsiteSection | null = null;
  ctaSection: PublicWebsiteSection | null = null;

  shots: { fleetTracking: string | null; dashboard: string | null; tripPlayback: string | null } = {
    fleetTracking: null,
    dashboard: null,
    tripPlayback: null,
  };

  videos: { hero: string | null; product: string | null } = {
    hero: null,
    product: null,
  };

  readonly demoModalOpen = signal(false);
  readonly dashTab = signal('Overview');

  readonly trustItems = [
    { value: 'Real-Time', label: 'Live tracking & updates', icon: '◎' },
    { value: '24/7', label: 'Fleet monitoring', icon: '◉' },
    { value: 'One', label: 'Unified platform', icon: '⬡' },
    { value: 'Multi-Module', label: 'All operations in one', icon: '▦' },
  ];

  pillars: { title: string; text: string; link: string; icon: string }[] = [
    { title: 'Fleet Management', text: 'Manage vehicles, assignments and operational status.', link: '/fleet-management', icon: 'FL' },
    { title: 'GPS Tracking', text: 'Monitor vehicles in real time and review historical routes.', link: '/gps-tracking', icon: 'GPS' },
    { title: 'Driver Management', text: 'Manage drivers, assignments, availability and performance.', link: '/features', icon: 'DR' },
    { title: 'Trip Management', text: 'Create, monitor and complete trips with full journey visibility.', link: '/features', icon: 'TR' },
    { title: 'Maintenance', text: 'Track service schedules, maintenance history and vehicle health.', link: '/features', icon: 'MT' },
    { title: 'Fuel Management', text: 'Monitor fuel usage, cost and vehicle efficiency.', link: '/features', icon: 'FU' },
  ];

  readonly trackingPoints = [
    'Live vehicle locations',
    'Speed & ignition status',
    'Exact address resolution',
    'Geofencing & zone events',
    'GPS history & playback',
    'Stops, parking & alerts',
  ];

  readonly dashTabs = [
    'Overview',
    'Vehicles',
    'Drivers',
    'Trips',
    'Maintenance',
    'Fuel',
    'Alerts',
    'Reports',
  ];

  readonly dashKpis = [
    { label: 'Total Vehicles', value: '42' },
    { label: 'Online', value: '36' },
    { label: 'Total Drivers', value: '56' },
    { label: 'Active Trips', value: '18' },
    { label: 'Open Alerts', value: '8' },
    { label: 'Maintenance Due', value: '5' },
  ];

  readonly tripCaps = [
    { title: 'Route Playback', icon: '▶' },
    { title: 'Stops', icon: '●' },
    { title: 'Parking', icon: 'P' },
    { title: 'Speed', icon: '⚡' },
    { title: 'Distance', icon: '↔' },
    { title: 'Duration', icon: '◷' },
  ];

  readonly alertPills = [
    { label: 'Vehicle Offline', tone: 'crit' },
    { label: 'Overspeed', tone: 'warn' },
    { label: 'Long Stop', tone: 'info' },
    { label: 'Maintenance Due', tone: 'maint' },
    { label: 'Geofence Entry', tone: 'ok' },
    { label: 'GPS Signal Lost', tone: 'crit' },
  ];

  readonly perfMetrics = [
    { label: 'Total Distance', value: '542.9 km' },
    { label: 'Avg Speed', value: '11.7 km/h' },
    { label: 'Max Speed', value: '93 km/h' },
    { label: 'Utilization', value: '82%' },
  ];

  readonly integrations = [
    'GPS Devices',
    'Traccar',
    'Google Maps',
    'SMS / Email',
    'AI Analytics',
    'REST API',
  ];

  readonly securityItems = [
    'Role-Based Access',
    'Audit Logs',
    'API Security',
    'Tenant Isolation',
    'User Management',
    'Backup & Recovery',
  ];

  ngOnInit(): void {
    this.seo.set(
      'Intelligent Fleet & Travel Operations Platform',
      'Manage your fleet, track every journey, and run transportation operations from one intelligent platform.',
      '/',
    );

    this.probeShot('fleet-tracking', 'fleetTracking');
    this.probeShot('dashboard', 'dashboard');
    this.probeShot('trip-playback', 'tripPlayback');
    this.probeVideo('hero-demo.mp4', 'hero');
    this.probeVideo('product-demo.mp4', 'product');

    this.content.getHome().subscribe(home => {
      if (!home) return;
      const s = home.sections ?? [];
      this.hero = this.content.sectionByType(s, 'Hero');
      this.featuresSection = this.content.sectionByType(s, 'Features');
      this.trackingSection = this.content.sectionByType(s, 'FleetTracking');
      this.dashboardSection = this.content.sectionByType(s, 'Dashboard');
      this.playbackSection = this.content.sectionByType(s, 'TripPlayback');
      this.alertsSection = this.content.sectionByType(s, 'Alerts');
      this.reportsSection = this.content.sectionByType(s, 'Reports');
      this.aiSection = this.content.sectionByType(s, 'AI');
      this.integrationsSection = this.content.sectionByType(s, 'Integrations');
      this.securitySection = this.content.sectionByType(s, 'Security');
      this.ctaSection = this.content.sectionByType(s, 'CTA');

      if (home.features?.length) {
        this.pillars = home.features.slice(0, 6).map((f: PublicWebsiteFeature, i: number) => ({
          title: f.title,
          text: f.description || '',
          link: f.linkUrl || '/features',
          icon: this.pillars[i]?.icon || f.iconKey?.slice(0, 2).toUpperCase() || 'SG',
        }));
      }

      if (home.settings?.defaultMetaTitle || home.settings?.defaultMetaDescription) {
        this.seo.set(
          home.settings.defaultMetaTitle || 'Intelligent Fleet & Travel Operations Platform',
          home.settings.defaultMetaDescription ||
            'Manage your fleet, track every journey, and run transportation operations from one intelligent platform.',
        );
      }
    });
  }

  ngOnDestroy(): void {
    document.body.classList.remove('sg-modal-open');
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.demoModalOpen()) this.closeDemoModal();
  }

  get modalVideoSrc(): string | null {
    return this.videos.product || this.videos.hero;
  }

  setDashTab(tab: string): void {
    this.dashTab.set(tab);
  }

  openDemoModal(): void {
    this.demoModalOpen.set(true);
    document.body.classList.add('sg-modal-open');
  }

  closeDemoModal(): void {
    this.demoModalOpen.set(false);
    document.body.classList.remove('sg-modal-open');
  }

  private probeShot(
    fileBase: string,
    key: 'fleetTracking' | 'dashboard' | 'tripPlayback',
  ): void {
    const url = `/website/${fileBase}.png`;
    const img = new Image();
    img.onload = () => {
      this.shots = { ...this.shots, [key]: url };
    };
    img.src = url;
  }

  private probeVideo(fileName: string, key: 'hero' | 'product'): void {
    const url = `/website/${fileName}`;
    const video = document.createElement('video');
    video.preload = 'metadata';
    video.onloadedmetadata = () => {
      this.videos = { ...this.videos, [key]: url };
    };
    video.src = url;
  }
}
