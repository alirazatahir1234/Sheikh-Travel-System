import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

type Destination = {
  name: string;
  region: string;
  tag: string;
  image: string;
  accent: string;
  from: string;
};

type Testimonial = {
  quote: string;
  name: string;
  trip: string;
  initials: string;
};

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss'
})
export class HomeComponent {
  readonly year = new Date().getFullYear();

  readonly destinations: Destination[] = [
    {
      name: 'Santorini',
      region: 'Cyclades, Greece',
      tag: 'Island glow',
      image:
        'https://images.unsplash.com/photo-1613395877344-13d4c79e4284?auto=format&fit=crop&w=900&q=80',
      accent: '#38bdf8',
      from: 'From $1,249'
    },
    {
      name: 'Marrakech',
      region: 'Morocco',
      tag: 'Desert pulse',
      image:
        'https://images.unsplash.com/photo-1597212618440-806262de4f6b?auto=format&fit=crop&w=900&q=80',
      accent: '#f59e0b',
      from: 'From $989'
    },
    {
      name: 'Kyoto',
      region: 'Japan',
      tag: 'Temple trails',
      image:
        'https://images.unsplash.com/photo-1493976040374-85c8e12f0c0e?auto=format&fit=crop&w=900&q=80',
      accent: '#f472b6',
      from: 'From $1,620'
    },
    {
      name: 'Amalfi Coast',
      region: 'Italy',
      tag: 'Coastal drive',
      image:
        'https://images.unsplash.com/photo-1533105079780-92b9be482077?auto=format&fit=crop&w=900&q=80',
      accent: '#34d399',
      from: 'From $1,180'
    }
  ];

  readonly testimonials: Testimonial[] = [
    {
      quote:
        'Sheikh Travel turned our anniversary into a seamless story — private transfers, sunset stops, zero stress.',
      name: 'Amira & Yusuf',
      trip: 'Santorini · 9 nights',
      initials: 'A'
    },
    {
      quote:
        'The team matched us with routes we would never have found. Felt bespoke, not like a template package.',
      name: 'Daniel Okoye',
      trip: 'Kyoto & Osaka',
      initials: 'D'
    },
    {
      quote:
        'Clear pricing, fast replies, and drivers who actually knew the city. Already booking our next leg.',
      name: 'Elena Rossi',
      trip: 'Amalfi road trip',
      initials: 'E'
    }
  ];
}
