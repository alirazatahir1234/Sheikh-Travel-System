import { AfterViewInit, Component, OnDestroy, OnInit, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { Subscription } from 'rxjs';
import { CtaBandComponent } from '../../shared/cta-band.component';
import { WebsiteSeoService } from '../../core/seo.service';
import { WebsiteTranslatePipe } from '../../shared/website-translate.pipe';

interface AlertItem {
  title: string;
  icon: string;
  what: string;
  why: string;
}

interface AlertGroup {
  id: string;
  title: string;
  kicker: string;
  lead: string;
  icon: string;
  accent: 'safety' | 'tracking' | 'health';
  count: number;
  sceneImage: string;
  sceneToast: {
    tone: 'danger' | 'warn' | 'ok';
    icon: string;
    title: string;
    detail: string;
  };
  items: AlertItem[];
}

@Component({
  selector: 'app-gps-alerts-page',
  standalone: true,
  imports: [RouterLink, MatIconModule, CtaBandComponent, WebsiteTranslatePipe],
  templateUrl: './gps-alerts.page.html',
  styleUrl: './gps-alerts.page.scss',
})
export class GpsAlertsPage implements OnInit, AfterViewInit, OnDestroy {
  private readonly seo = inject(WebsiteSeoService);
  private readonly route = inject(ActivatedRoute);
  private fragmentSub?: Subscription;

  activeSection = 'safety';

  readonly platformCapabilities: {
    icon: string;
    title: string;
    text: string;
    link?: string;
    linkLabel?: string;
  }[] = [
    {
      icon: 'pin_drop',
      title: 'Geofencing',
      text: 'Draw enter and exit zones on the map. Get alerts when vehicles arrive at depots, job sites or leave restricted areas.',
      link: '/live-tracking',
      linkLabel: 'Live map & zones',
    },
    {
      icon: 'history',
      title: 'Playback history',
      text: 'Review routes after the fact with GPS history playback — see speed, stops and context alongside alert timelines.',
      link: '/live-tracking',
      linkLabel: 'History playback',
    },
    {
      icon: 'my_location',
      title: 'Live tracking',
      text: 'Every alert ties back to a live fleet map with speed, ignition and connectivity — so you know which vehicle needs action.',
      link: '/live-tracking',
      linkLabel: 'Explore live map',
    },
    {
      icon: 'cloud_sync',
      title: 'Offline log sync',
      text: 'When a data package expires or the vehicle goes offline, the device buffers logs locally. Once internet returns, stored positions sync automatically to live.',
    },
  ];

  readonly benefitRows = [
    { icon: 'notifications', title: '13 alert types', text: 'Safety, tracking and vehicle health — turn on what you need.' },
    { icon: 'directions_car', title: 'Know which vehicle', text: 'What happened and when — without calling the driver first.' },
    { icon: 'verified_user', title: 'Fewer surprises', text: 'Rental, transport, logistics and staff transport fleets stay ahead.' },
    { icon: 'tune', title: 'Turn on what matters', text: 'Enable the alerts that fit your operation; leave the rest off.' },
  ] as const;

  readonly jumpLinks = [
    { id: 'safety', title: 'Safety Alerts', icon: 'health_and_safety' },
    { id: 'tracking', title: 'Tracking Alerts', icon: 'my_location' },
    { id: 'vehicle-health', title: 'Vehicle Health Alerts', icon: 'directions_car' },
    { id: 'road-speed', title: 'Road Speed Alert', icon: 'add_road' },
  ] as const;

  readonly groups: AlertGroup[] = [
    {
      id: 'safety',
      title: 'Safety Alerts',
      kicker: 'Protect people',
      lead:
        'Protect drivers, passengers and your reputation. These alerts surface the moments that need a quick response.',
      icon: 'health_and_safety',
      accent: 'safety',
      count: 5,
      sceneImage: '/website/gps-alerts/scene-safety.jpg',
      sceneToast: {
        tone: 'danger',
        icon: 'sos',
        title: 'SOS Panic Alert',
        detail: 'Driver pressed panic · just now',
      },
      items: [
        {
          title: 'SOS Panic',
          icon: 'sos',
          what: 'Sends an urgent alert when the driver presses the panic button in the vehicle or on the app.',
          why: 'Gives your team a chance to respond immediately to threats, accidents or medical emergencies.',
        },
        {
          title: 'Fixed Speed Limit',
          icon: 'speed',
          what: 'Alerts when a vehicle goes faster than a speed you set for the fleet or that vehicle (for example 100 km/h).',
          why: 'Simple company rule — one clear ceiling for drivers who ignore your policy.',
        },
        {
          title: 'Road Speed Alert',
          icon: 'add_road',
          what:
            'Compares the vehicle’s current speed with the legal speed limit of the road it is on, plus a small buffer you can set.',
          why:
            'City roads and highways have different limits. You catch real overspeeding on that street — not just against one blanket number.',
        },
        {
          title: 'Geofence Enter',
          icon: 'login',
          what: 'Notifies when a vehicle enters an area you defined (depot, customer site, restricted area, or city boundary).',
          why: 'Confirm arrivals and site visits without waiting for a driver phone call.',
        },
        {
          title: 'Geofence Exit',
          icon: 'logout',
          what: 'Notifies when a vehicle leaves an allowed or restricted area.',
          why: 'Spot unauthorized movement, early departures or vehicles leaving a job site before the trip is done.',
        },
      ],
    },
    {
      id: 'tracking',
      title: 'Tracking Alerts',
      kicker: 'Stay in control',
      lead: 'Stay in control of where each vehicle is — and whether it is still visible on the map.',
      icon: 'my_location',
      accent: 'tracking',
      count: 5,
      sceneImage: '/website/gps-alerts/scene-tracking.jpg',
      sceneToast: {
        tone: 'warn',
        icon: 'wifi_off',
        title: 'Vehicle Offline',
        detail: 'No location for 12 min',
      },
      items: [
        {
          title: 'Vehicle Online',
          icon: 'wifi',
          what: 'Tells you when a vehicle reconnects and starts sharing live location again.',
          why: 'You know it is back on the map after night parking, a tunnel, or a temporary signal gap.',
        },
        {
          title: 'Vehicle Offline',
          icon: 'wifi_off',
          what: 'Alerts when a vehicle stops sharing location for longer than you allow.',
          why: 'Catch dead batteries, covered devices, network issues or suspicious disconnects early.',
        },
        {
          title: 'Ignition On',
          icon: 'power_settings_new',
          what: 'Fires when the vehicle’s engine is switched on.',
          why: 'See when a trip actually starts — useful for duty hours, rental handovers and night movements.',
        },
        {
          title: 'Ignition Off',
          icon: 'power_off',
          what: 'Fires when the engine is switched off.',
          why: 'Confirm parking, end of trip, or unexpected stops during an active job.',
        },
        {
          title: 'GPS Lost',
          icon: 'gps_off',
          what: 'Warns when the device cannot get a clear location from satellites (signal blocked or weak).',
          why: 'You will not trust an old pin on the map when the vehicle is no longer reporting a real position.',
        },
      ],
    },
    {
      id: 'vehicle-health',
      title: 'Vehicle Health Alerts',
      kicker: 'Protect the asset',
      lead: 'Protect the vehicle itself — power, battery and fuel — before a roadside failure or theft attempt.',
      icon: 'directions_car',
      accent: 'health',
      count: 3,
      sceneImage: '/website/gps-alerts/scene-health.jpg',
      sceneToast: {
        tone: 'warn',
        icon: 'battery_alert',
        title: 'Low Battery',
        detail: 'Backup below safe level',
      },
      items: [
        {
          title: 'Low Battery',
          icon: 'battery_alert',
          what: 'Alerts when the backup battery or vehicle battery drops below a safe level.',
          why: 'Charge or replace it before you lose visibility mid-route.',
        },
        {
          title: 'Power Cut',
          icon: 'bolt',
          what: 'Notifies when main power to the tracking device is disconnected.',
          why: 'A common theft or tampering warning — act while there is still time to respond.',
        },
        {
          title: 'Fuel',
          icon: 'local_gas_station',
          what: 'Flags unusual fuel drops or refill patterns when your setup supports fuel monitoring.',
          why: 'Help reduce fuel theft and unexplained consumption across commercial fleets.',
        },
      ],
    },
  ];

  setActive(id: string): void {
    this.activeSection = id;
  }

  ngOnInit(): void {
    this.seo.set(
      'GPS Alerts',
      'SheikhGo GPS Alerts for fleet owners — safety, tracking and vehicle health notifications including road speed, geofence, SOS, ignition, fuel and more.',
      '/gps-alerts',
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
