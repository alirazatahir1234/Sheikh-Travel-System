import { AfterViewInit, Component, OnDestroy, OnInit, computed, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { Subscription } from 'rxjs';
import { CtaBandComponent } from '../../shared/cta-band.component';
import { WebsiteSeoService } from '../../core/seo.service';
import { WEBSITE_BRAND } from '../../core/brand';
import { WebsiteI18nService } from '../../core/website-i18n.service';
import { WebsiteTranslatePipe } from '../../shared/website-translate.pipe';

interface SolutionItem {
  title: string;
  icon: string;
  what: string;
  why: string;
}

interface SolutionGroup {
  id: string;
  title: string;
  kicker: string;
  lead: string;
  icon: string;
  accent: 'chat' | 'engine' | 'whatsapp' | 'surfaces';
  count: number;
  sceneImage: string;
  sceneToast: {
    tone: 'ok' | 'warn' | 'info';
    icon: string;
    title: string;
    detail: string;
  };
  items: SolutionItem[];
}

interface PlatformHighlight {
  icon: string;
  title: string;
  text: string;
  link?: string;
  linkLabel?: string;
}

@Component({
  selector: 'app-ai-solutions-page',
  standalone: true,
  imports: [RouterLink, MatIconModule, CtaBandComponent, WebsiteTranslatePipe],
  templateUrl: './ai-solutions.page.html',
  styleUrl: './ai-solutions.page.scss',
})
export class AiSolutionsPage implements OnInit, AfterViewInit, OnDestroy {
  private readonly seo = inject(WebsiteSeoService);
  private readonly route = inject(ActivatedRoute);
  private fragmentSub?: Subscription;

  readonly brand = WEBSITE_BRAND;
  private readonly i18n = inject(WebsiteI18nService);
  activeSection = 'assistant-chat';

  readonly heroPerks = computed(() => {
    this.i18n.lang();
    return [
      { icon: 'verified_user', text: this.i18n.t('pages.aiSolutions.perk.grounded') },
      { icon: 'psychology', text: this.i18n.t('pages.aiSolutions.perk.tools') },
      { icon: 'shield', text: this.i18n.t('pages.aiSolutions.perk.control') },
    ] as const;
  });

  readonly pillars = [
    {
      id: 'assistant-chat',
      icon: 'forum',
      count: '5 capabilities',
      title: 'Assistant & chat',
      lead: 'Ask questions in plain English. Answers come from live fleet tools — with optional language-model polish when you configure a provider.',
    },
    {
      id: 'fleet-intelligence',
      icon: 'insights',
      count: '6 capabilities',
      title: 'Fleet intelligence',
      lead: 'Scores, digests, and recommendations built from rules and heuristics on your GPS and fleet data — transparent, not black-box ML.',
    },
    {
      id: 'whatsapp',
      icon: 'chat',
      count: '1 capability',
      title: 'WhatsApp assist',
      lead: 'Draft replies and summaries for your team. Nothing is sent automatically — your staff review and tap send.',
    },
    {
      id: 'surfaces',
      icon: 'devices',
      count: '2 surfaces',
      title: 'Where you use it',
      lead: 'Nexus AI Management Center, in-app chat and FAB, plus Driver & Fleet mobile copilot — same intelligence, right context.',
    },
  ] as const;

  readonly jumpLinks = [
    { id: 'assistant-chat', title: 'Assistant & chat', icon: 'forum' },
    { id: 'fleet-intelligence', title: 'Fleet intelligence', icon: 'insights' },
    { id: 'whatsapp', title: 'WhatsApp', icon: 'chat' },
    { id: 'surfaces', title: 'Surfaces', icon: 'devices' },
    { id: 'how-it-works', title: 'How it works', icon: 'info' },
  ] as const;

  readonly inventoryStats = [
    { value: '14', label: 'Shipped AI capabilities' },
    { value: '4', label: 'Product pillars' },
    { value: '0', label: 'Auto-sent WhatsApp messages' },
  ] as const;

  readonly benefitRows = [
    {
      icon: 'build_circle',
      title: 'Tool-first answers',
      text: 'Assistants call live fleet tools — not generic chat guesses.',
    },
    {
      icon: 'science',
      title: 'Honest intelligence labels',
      text: 'Scores and “predictions” are rule-based heuristics, not hidden ML.',
    },
    {
      icon: 'edit_note',
      title: 'Human-approved messaging',
      text: 'WhatsApp AI Assist drafts only; your team sends every message.',
    },
    {
      icon: 'devices',
      title: 'Nexus + mobile surfaces',
      text: 'Management Center, in-app chat, FAB, and Driver & Fleet copilot.',
    },
  ] as const;

  readonly workflowSteps = [
    {
      step: '1',
      title: 'Connect your fleet data',
      text: 'GPS, trips, alerts, fuel, and operations records already live in SheikhGo Nexus — AI reads the same sources your maps and reports use.',
    },
    {
      step: '2',
      title: 'Configure optional LLM providers',
      text: 'Add Ollama, DeepInfra, or a compatible endpoint when you want natural-language polish. Tool paths and digests work without one.',
    },
    {
      step: '3',
      title: 'Ask, review, act',
      text: 'Chat in Nexus or mobile copilot, scan morning digests, follow recommendations, and approve WhatsApp drafts before anything goes out.',
    },
  ] as const;

  readonly platformHighlights: PlatformHighlight[] = [
    {
      icon: 'build_circle',
      title: 'Tool-first answers',
      text: 'The AI Assistant calls structured fleet tools — trips, fuel, alerts, vehicles — so responses reflect your account, not generic chat text.',
    },
    {
      icon: 'memory',
      title: 'Conversation context',
      text: 'Sessions remember recent turns so follow-up questions stay coherent across the Nexus chat, gateway, and copilot experiences.',
    },
    {
      icon: 'tune',
      title: 'Configurable providers',
      text: 'Connect Ollama or DeepInfra (or your chosen endpoint) when you want natural-language generation; deterministic paths work without an LLM.',
    },
    {
      icon: 'notifications_active',
      title: 'Smarter notifications',
      text: 'A decision engine and escalation rules help prioritize what reaches owners and managers — paired with GPS Alerts on the map.',
      link: '/gps-alerts',
      linkLabel: 'GPS Alerts',
    },
  ];

  readonly groups: SolutionGroup[] = [
    {
      id: 'assistant-chat',
      title: 'Assistant & chat',
      kicker: 'Talk to your fleet',
      lead:
        'SheikhGo AI is built for fleet operators: structured Q&A, session-based chat, and quick copilot prompts — all wired to the same tools and data your team already trusts.',
      icon: 'forum',
      accent: 'chat',
      count: 5,
      sceneImage: '/website/product/dashboard.png',
      sceneToast: {
        tone: 'info',
        icon: 'support_agent',
        title: 'AI Assistant',
        detail: 'Tool-backed answer · trips & alerts',
      },
      items: [
        {
          title: 'AI Assistant',
          icon: 'support_agent',
          what:
            'A structured fleet assistant that answers operational questions by calling tools — vehicles, trips, fuel usage, alerts, and related reporting — instead of guessing.',
          why:
            'Managers get fast, account-specific answers without exporting spreadsheets or clicking through every screen.',
        },
        {
          title: 'AI Chat Gateway',
          icon: 'hub',
          what:
            'Session-based chat with tool access and an optional LLM layer. Keeps conversation state, routes requests safely, and supports the same fleet capabilities across clients.',
          why:
            'One consistent chat backbone for Nexus, integrations, and future channels — with guardrails around what the assistant can do.',
        },
        {
          title: 'AI Copilot',
          icon: 'auto_awesome',
          what:
            'Lightweight quick Q&A for everyday questions — status checks, summaries, and “what should I look at?” prompts without opening a full analytics view.',
          why:
            'Reduces friction for busy dispatchers and owners who need a straight answer in seconds.',
        },
        {
          title: 'Configurable AI providers',
          icon: 'cloud_sync',
          what:
            'Power natural-language responses with providers you configure — including Ollama for on-prem style setups and DeepInfra for hosted models. No provider configured? Tool-based and heuristic flows still run.',
          why:
            'You choose cost, privacy, and model fit; SheikhGo does not lock you into a single vendor narrative.',
        },
        {
          title: 'Conversation memory & context',
          icon: 'history',
          what:
            'Recent messages and session context carry forward so “What about yesterday?” or “Same vehicle” makes sense in ongoing chats.',
          why:
            'Feels like a real working conversation — not a string of one-off queries.',
        },
      ],
    },
    {
      id: 'fleet-intelligence',
      title: 'Deterministic fleet intelligence',
      kicker: 'Clear logic, honest labels',
      lead:
        'Scores, digests, and recommendations are computed from your fleet signals using transparent rules and heuristics — not undisclosed “AI models” trained on your data.',
      icon: 'insights',
      accent: 'engine',
      count: 6,
      sceneImage: '/website/nexus/fleet-intelligence-dashboard.jpg',
      sceneToast: {
        tone: 'ok',
        icon: 'favorite',
        title: 'Fleet health',
        detail: 'Rule-based score · 82 / 100',
      },
      items: [
        {
          title: 'Fleet health scoring',
          icon: 'favorite',
          what:
            'Rolls up connectivity, alert patterns, idle time, and related signals into an at-a-glance health view for vehicles and the fleet as a whole.',
          why:
            'Spot weak links before they become breakdowns, theft events, or compliance headaches.',
        },
        {
          title: 'AI recommendations',
          icon: 'lightbulb',
          what:
            'Actionable suggestions — review this driver, check this vehicle, adjust this geofence — generated from thresholds and fleet rules you can reason about.',
          why:
            'Turns raw GPS and alert noise into a short list of what to do next.',
        },
        {
          title: 'Heuristic predictions',
          icon: 'timeline',
          what:
            'Maintenance hints, driver-risk flags, and fuel anomaly signals based on patterns in your data. These are rule-based estimates, not trained machine-learning forecasts.',
          why:
            'Honest early warnings that help planning without over-promising predictive accuracy.',
        },
        {
          title: 'Morning fleet digest',
          icon: 'wb_sunny',
          what:
            'A concise start-of-day summary — overnight alerts, offline units, trips worth reviewing — delivered in a format your team can scan in minutes.',
          why:
            'Owners and managers start aligned instead of digging through logs first thing.',
        },
        {
          title: 'Notification decision engine',
          icon: 'rule',
          what:
            'Decides which events become push, email, SMS, or in-app notifications based on severity, quiet hours, and your escalation policy.',
          why:
            'Fewer alert floods; more attention on what actually needs a human.',
        },
        {
          title: 'Alert escalation',
          icon: 'priority_high',
          what:
            'When conditions persist or worsen — repeated overspeed, prolonged offline, unresolved panic — escalation paths notify the right roles.',
          why:
            'Critical situations do not stall because the first notification was missed.',
        },
      ],
    },
    {
      id: 'whatsapp',
      title: 'WhatsApp Agent AI Assist',
      kicker: 'Suggest, never send alone',
      lead:
        'Help your front-line staff respond faster on WhatsApp with AI-generated draft replies and thread summaries — always reviewed by a person before anything goes to a customer.',
      icon: 'chat',
      accent: 'whatsapp',
      count: 1,
      sceneImage: '/website/gps-alerts/scene-health.jpg',
      sceneToast: {
        tone: 'warn',
        icon: 'edit_note',
        title: 'Suggested reply',
        detail: 'Draft only — agent sends',
      },
      items: [
        {
          title: 'WhatsApp Agent AI Assist',
          icon: 'edit_note',
          what:
            'Suggests replies and summarizes long WhatsApp threads for agents handling bookings, support, or fleet coordination. The system never auto-sends messages on your behalf.',
          why:
            'Speeds up consistent, professional responses while keeping brand and compliance control with your team.',
        },
      ],
    },
    {
      id: 'surfaces',
      title: 'Surfaces & access',
      kicker: 'Same brain, right screen',
      lead:
        'Fleet AI is not a separate product bolted on — it lives where your team already works: Nexus management, floating chat, and mobile apps for drivers and fleet managers.',
      icon: 'devices',
      accent: 'surfaces',
      count: 2,
      sceneImage: '/website/product/live-map.png',
      sceneToast: {
        tone: 'info',
        icon: 'dashboard',
        title: 'Nexus · /ai',
        detail: 'Management Center + mobile copilot',
      },
      items: [
        {
          title: 'Nexus AI Management Center',
          icon: 'dashboard',
          what:
            'The `/ai` area in SheikhGo Nexus centralizes AI settings, provider configuration, session oversight, and management tools — plus in-app chat and a floating action button for quick questions.',
          why:
            'Administrators configure and monitor AI in one place; operators chat without leaving Nexus.',
        },
        {
          title: 'Driver & Fleet mobile copilot',
          icon: 'phone_iphone',
          what:
            'Copilot-style Q&A in the Driver and Fleet mobile apps — vehicle status, trip context, and guided prompts sized for small screens and field use.',
          why:
            'Drivers and supervisors get answers on the road without calling the office for every detail.',
        },
      ],
    },
  ];

  readonly honestyPoints = [
    {
      icon: 'science',
      title: 'Heuristics, not hidden ML',
      text: 'Health scores, recommendations, and “predictions” use explicit rules on your fleet data — we do not claim proprietary trained models where none exist.',
    },
    {
      icon: 'send',
      title: 'WhatsApp stays human-sent',
      text: 'AI Assist proposes text; your agent approves and sends. No silent auto-replies to customers or drivers.',
    },
    {
      icon: 'settings',
      title: 'LLM is optional',
      text: 'Natural-language polish requires a configured provider (e.g. Ollama or DeepInfra). Core tools, digests, and notification logic work without it.',
    },
    {
      icon: 'block',
      title: 'Not sold as live today',
      text: 'Document retrieval (RAG), MCP console execution, and always-on MCP bridge are platform directions — not customer-facing products on this marketing page.',
    },
  ] as const;

  setActive(id: string): void {
    this.activeSection = id;
  }

  ngOnInit(): void {
    this.seo.set(
      'AI Solutions',
      'SheikhGo AI for fleets — assistant & chat with fleet tools, heuristic intelligence, WhatsApp AI Assist (human-approved), Nexus AI Management Center, and mobile copilot. Honest, configurable, tool-first.',
      '/ai-solutions',
    );
  }

  ngAfterViewInit(): void {
    this.fragmentSub = this.route.fragment.subscribe(fragment => {
      if (!fragment) return;
      this.activeSection = fragment;
      setTimeout(() =>
        document.getElementById(fragment)?.scrollIntoView({ behavior: 'smooth', block: 'start' }),
      );
    });
  }

  ngOnDestroy(): void {
    this.fragmentSub?.unsubscribe();
  }
}
