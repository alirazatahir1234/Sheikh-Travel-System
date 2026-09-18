import { Component, OnInit, inject } from '@angular/core';
import { NgClass } from '@angular/common';
import { RouterLink } from '@angular/router';
import { WebsiteSeoService } from '../../core/seo.service';
import { WEBSITE_BRAND } from '../../core/brand';
import { WebsitePublicContentService } from '../../core/website-public-content.service';

@Component({
  selector: 'app-home-page',
  standalone: true,
  imports: [RouterLink, NgClass],
  templateUrl: './home.page.html',
  styleUrl: './home.page.scss',
})
export class HomePage implements OnInit {
  readonly brand = WEBSITE_BRAND;
  private readonly seo = inject(WebsiteSeoService);
  private readonly content = inject(WebsitePublicContentService);

  heroEyebrow: string = WEBSITE_BRAND.companyName;
  heroTitlePrimary = 'Build Smarter with';
  heroTitleAccent = 'AI-Powered Solutions';
  heroLead: string = WEBSITE_BRAND.mission;
  heroPrimaryCta = { label: 'Get a Free Consultation →', link: '/contact' };
  heroSecondaryCta = { label: 'Our Services', link: '/', fragment: 'services' as string | undefined };
  cmsFeatures: { title: string; text: string; link: string }[] = [];

  readonly softStats = [
    { label: 'End-to-End Delivery' },
    { label: 'Modern Technology' },
    { label: 'AI-Ready Solutions' },
    { label: 'Business-Focused' },
  ] as const;

  readonly capabilityChips = [
    'Web Development',
    'Mobile Apps',
    'Cloud Solutions',
    'AI Solutions',
    'UI/UX Design',
    'IT Consulting',
  ] as const;

  readonly trustedTech = [
    'Microsoft',
    'AWS',
    'Google Cloud',
    'Flutter',
    'Shopify',
    'Figma',
  ] as const;

  readonly services = [
    {
      title: 'Web Development',
      text: 'Modern, scalable and high-performing web applications.',
      icon: 'WEB',
    },
    {
      title: 'Mobile App Development',
      text: 'Cross-platform and native mobile applications for modern businesses.',
      icon: 'APP',
    },
    {
      title: 'UI/UX Design',
      text: 'User-centered interfaces that create meaningful experiences.',
      icon: 'UX',
    },
    {
      title: 'Cloud Solutions',
      text: 'Scalable cloud infrastructure, migration and deployment solutions.',
      icon: 'CLD',
    },
    {
      title: 'IT Consulting',
      text: 'Strategic technology guidance to help achieve your business goals.',
      icon: 'IT',
    },
    {
      title: 'Custom Software Solutions',
      text: 'Tailored software built around your unique business needs.',
      icon: 'SW',
    },
  ] as const;

  readonly aiFeatures = [
    'AI Automation',
    'Intelligent Analytics',
    'AI Chatbots & Assistants',
    'Predictive Insights',
    'Computer Vision',
    'Generative AI',
    'LLM Integration',
    'AI Agents',
  ] as const;

  readonly industries = [
    { title: 'Logistics & Fleet', link: WEBSITE_BRAND.loginPath, icon: 'LG' },
    { title: 'Retail & E-Commerce', link: '/contact', icon: 'RT' },
    { title: 'Healthcare', link: '/contact', icon: 'HC' },
    { title: 'Education', link: '/contact', icon: 'ED' },
    { title: 'Finance', link: '/contact', icon: 'FN' },
    { title: 'Real Estate', link: '/contact', icon: 'RE' },
    { title: 'Travel & Tourism', link: '/contact', icon: 'TR' },
    { title: 'Manufacturing', link: '/contact', icon: 'MF' },
    { title: 'More Industries', link: '/contact', icon: '+' },
  ] as const;

  readonly whyItems = [
    {
      title: 'Client-Centric Approach',
      text: 'We start with your goals and design solutions around real business outcomes.',
      icon: '01',
    },
    {
      title: 'Modern Technologies',
      text: 'We build with current platforms and practices that stay maintainable over time.',
      icon: '02',
    },
    {
      title: 'AI-Driven Innovation',
      text: 'We apply AI where it creates leverage — automation, insight, and better decisions.',
      icon: '03',
    },
    {
      title: 'Scalable Solutions',
      text: 'Architecture designed to grow with your users, data, and product roadmap.',
      icon: '04',
    },
    {
      title: 'On-Time Delivery',
      text: 'Clear milestones, transparent progress, and disciplined delivery practices.',
      icon: '05',
    },
    {
      title: 'Long-Term Partnership',
      text: 'Support and iteration after launch so your technology stays sharp.',
      icon: '06',
    },
  ] as const;

  readonly processSteps = [
    { step: '01', title: 'Discover', text: 'Understand your needs and goals.' },
    { step: '02', title: 'Plan', text: 'Create a clear strategy and roadmap.' },
    { step: '03', title: 'Design', text: 'Design user-centered solutions.' },
    { step: '04', title: 'Develop', text: 'Build, test and iterate with best practices.' },
    { step: '05', title: 'Deploy', text: 'Launch and provide ongoing support.' },
  ] as const;

  readonly fleetCapabilities = [
    'Live GPS Tracking',
    'Vehicle Management',
    'Driver Management',
    'Trip Management',
    'Fleet Analytics',
    'GPS Route History',
    'AI-ready architecture',
  ] as const;

  readonly testimonials = [
    {
      quote:
        'Sample client testimonial — replace with an approved customer quote before production.',
      name: 'Client Name',
      role: 'Role',
      company: 'Company',
    },
    {
      quote:
        'Sample client testimonial — replace with an approved customer quote before production.',
      name: 'Client Name',
      role: 'Role',
      company: 'Company',
    },
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
}
