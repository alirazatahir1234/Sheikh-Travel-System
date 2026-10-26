import { AfterViewInit, Component, OnDestroy, OnInit, inject } from '@angular/core';
import { NgClass } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { MatIconModule } from '@angular/material/icon';
import { WebsiteSeoService } from '../../core/seo.service';
import { WEBSITE_BRAND } from '../../core/brand';
import { WebsitePublicContentService } from '../../core/website-public-content.service';
import { INDUSTRY_ITEMS, SERVICE_ITEMS } from '../../core/site-catalog';

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

  heroEyebrow: string = WEBSITE_BRAND.companyName;
  heroTitlePrimary = 'Build Smarter with';
  heroTitleAccent = 'AI-Powered Solutions';
  heroLead: string = WEBSITE_BRAND.mission;
  heroPrimaryCta = { label: 'Get a Free Consultation →', link: '/contact' };
  heroSecondaryCta = { label: 'Our Services', link: '/', fragment: 'services' as string | undefined };
  cmsFeatures: { title: string; text: string; link: string }[] = [];
  nexusImageError = false;

  readonly nexusCallouts = [
    {
      title: 'Live GPS Tracking',
      text: 'Real-time map, speed, and status',
      icon: 'gps_fixed',
    },
    {
      title: 'AI Assistant',
      text: 'Ask fleet questions in plain English',
      icon: 'smart_toy',
    },
    {
      title: 'Geofencing',
      text: 'Zones with instant exit alerts',
      icon: 'fence',
    },
    {
      title: 'Smart Notifications',
      text: 'App, dashboard, email, and SMS',
      icon: 'notifications_active',
    },
  ] as const;

  readonly nexusFeatures = [
    {
      title: 'Live GPS Tracking',
      text: 'See vehicles on the map with speed and trip state as conditions change.',
      icon: 'my_location',
      link: '/live-tracking',
    },
    {
      title: 'Geofencing',
      text: 'Draw office, depot, and customer zones — get notified when vehicles cross boundaries.',
      icon: 'fence',
      link: '/gps-tracking',
    },
    {
      title: 'Trip Playback & History',
      text: 'Replay routes with stops and timestamps for disputes, audits, and coaching.',
      icon: 'route',
      link: '/gps-tracking',
    },
    {
      title: 'Offline to Online Sync',
      text: 'Recover trips and positions when devices reconnect — no silent data gaps.',
      icon: 'sync',
      link: '/fleet-management',
    },
    {
      title: 'Fleet Health Monitoring',
      text: 'Track device connectivity, idle time, and operational signals in one view.',
      icon: 'health_and_safety',
      link: '/fleet-management',
    },
    {
      title: 'AI Assistant',
      text: 'Structured answers from fleet tools — offline vehicles, fuel, alerts, and more.',
      icon: 'support_agent',
      link: '/ai-solutions',
    },
    {
      title: 'GPS Alerts',
      text: 'Speeding, geofence exits, panic, fuel, and device events when you need to act.',
      icon: 'warning',
      link: '/gps-alerts',
    },
    {
      title: 'WhatsApp Assist',
      text: 'Draft owner-ready messages from fleet context — human-approved before send.',
      icon: 'chat',
      link: '/ai-solutions',
    },
  ] as const;

  readonly softStats = [
    { label: 'End-to-End Delivery' },
    { label: 'Modern Technology' },
    { label: 'AI-Ready Solutions' },
    { label: 'Business-Focused' },
  ] as const;

  readonly capabilityChips = [
    { label: 'Web Development', icon: 'language' },
    { label: 'Mobile Apps', icon: 'smartphone' },
    { label: 'Cloud Solutions', icon: 'cloud' },
    { label: 'AI Solutions', icon: 'auto_awesome' },
    { label: 'UI/UX Design', icon: 'palette' },
    { label: 'IT Consulting', icon: 'support_agent' },
  ] as const;

  readonly trustedTech = [
    'Microsoft',
    'AWS',
    'Google Cloud',
    'Flutter',
    'Shopify',
    'Figma',
  ] as const;

  readonly services = SERVICE_ITEMS;

  readonly aiFeatures = [
    'AI Assistant & Chat Gateway',
    'ERP AI Management Center',
    'Fleet health & morning digest',
    'Heuristic recommendations',
    'WhatsApp AI Assist (human-approved)',
    'Driver & Fleet mobile copilot',
  ] as const;

  readonly industries = [
    ...INDUSTRY_ITEMS.map(i => ({ title: i.title, link: i.fragment ? '/contact' : i.link, icon: i.icon })),
    { title: 'More Industries', link: '/contact', icon: 'add_circle' },
  ];

  readonly whyItems = [
    {
      title: 'Client-Centric Approach',
      text: 'We start with your goals and design solutions around real business outcomes.',
      icon: 'handshake',
    },
    {
      title: 'Modern Technologies',
      text: 'We build with current platforms and practices that stay maintainable over time.',
      icon: 'memory',
    },
    {
      title: 'AI-Driven Innovation',
      text: 'We apply AI where it creates leverage — automation, insight, and better decisions.',
      icon: 'auto_awesome',
    },
    {
      title: 'Scalable Solutions',
      text: 'Architecture designed to grow with your users, data, and product roadmap.',
      icon: 'trending_up',
    },
    {
      title: 'On-Time Delivery',
      text: 'Clear milestones, transparent progress, and disciplined delivery practices.',
      icon: 'schedule',
    },
    {
      title: 'Long-Term Partnership',
      text: 'Support and iteration after launch so your technology stays sharp.',
      icon: 'groups',
    },
  ] as const;

  readonly processSteps = [
    { step: '01', title: 'Discover', text: 'Understand your needs and goals.', icon: 'search' },
    { step: '02', title: 'Plan', text: 'Create a clear strategy and roadmap.', icon: 'assignment' },
    { step: '03', title: 'Design', text: 'Design user-centered solutions.', icon: 'brush' },
    { step: '04', title: 'Develop', text: 'Build, test and iterate with best practices.', icon: 'code' },
    { step: '05', title: 'Deploy', text: 'Launch and provide ongoing support.', icon: 'cloud_upload' },
  ] as const;

  /** Fallback icons for CMS-managed "Featured capabilities" cards (no icon field on that model). */
  private readonly cmsFeatureIcons = ['bolt', 'hub', 'insights', 'auto_awesome'] as const;

  cmsFeatureIcon(index: number): string {
    return this.cmsFeatureIcons[index % this.cmsFeatureIcons.length];
  }

  readonly fleetCapabilities = [
    'Live GPS Tracking',
    'Vehicle Management',
    'Driver Management',
    'Trip Management',
    'Fleet Analytics',
    'GPS Route History',
    'AI-ready architecture',
  ] as const;

  ngOnInit(): void {
    this.seo.set(
      'SheikhGo Technologies | IT, Software & AI Solutions',
      'SheikhGo Technologies delivers software development, AI solutions, mobile applications, cloud services and custom technology solutions for modern businesses.',
      '/',
    );

    this.content.getHome().subscribe(home => {
      if (!home) return;

      if (home.settings?.defaultMetaTitle || home.settings?.defaultMetaDescription) {
        this.seo.set(
          home.settings.defaultMetaTitle ||
            'SheikhGo Technologies | IT, Software & AI Solutions',
          home.settings.defaultMetaDescription ||
            'SheikhGo Technologies delivers software development, AI solutions, mobile applications, cloud services and custom technology solutions for modern businesses.',
        );
      }

      const hero = this.content.sectionByType(home.sections, 'Hero');
      if (hero) {
        if (hero.subtitle?.trim()) this.heroEyebrow = hero.subtitle.trim();
        if (hero.title?.trim()) {
          const parts = hero.title.trim().split(/\s+/);
          if (parts.length > 2) {
            this.heroTitlePrimary = parts.slice(0, Math.ceil(parts.length / 2)).join(' ');
            this.heroTitleAccent = parts.slice(Math.ceil(parts.length / 2)).join(' ');
          } else {
            this.heroTitlePrimary = hero.title.trim();
            this.heroTitleAccent = '';
          }
        }
        if (hero.content?.trim()) this.heroLead = hero.content.trim();
        if (hero.buttonText?.trim() && hero.buttonUrl?.trim()) {
          this.heroPrimaryCta = {
            label: hero.buttonText.trim(),
            link: hero.buttonUrl.trim(),
          };
        }
        if (hero.secondaryButtonText?.trim() && hero.secondaryButtonUrl?.trim()) {
          const url = hero.secondaryButtonUrl.trim();
          const hash = url.includes('#') ? url.split('#')[1] : undefined;
          this.heroSecondaryCta = {
            label: hero.secondaryButtonText.trim(),
            link: url.startsWith('#') ? '/' : url.split('#')[0] || '/',
            fragment: hash,
          };
        }
      }

      if (home.features?.length) {
        this.cmsFeatures = home.features.map(f => ({
          title: f.title,
          text: f.description || '',
          link: f.linkUrl || '/features',
        }));
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
