/**
 * Shared UI models used by the generic dashboard / page widgets.
 * Keeping them in one place lets every module compose widgets
 * without cross-importing component-specific types.
 */

export type AccentColor =
  | 'blue' | 'green' | 'teal' | 'purple'
  | 'orange' | 'rose' | 'amber' | 'sky';

export interface QuickLaunchApp {
  id: string;
  label: string;
  icon: string;          // material icon
  route?: string;
  color: AccentColor;
}

export interface StatTile {
  key: string;
  label: string;
  value: string | number;
  hint?: string;
  icon: string;
  color: AccentColor;
  prefix?: string;
  suffix?: string;
}

export interface TaskItem {
  id: string | number;
  title: string;
  subtitle?: string;
  done?: boolean;
  meta?: string;         // right-aligned meta (e.g. "Lahore HR Director")
  avatarUrl?: string;
  avatarInitials?: string;
}

export interface DataTableColumn<T = any> {
  key: string;
  header: string;
  width?: string;
  align?: 'left' | 'right' | 'center';
  cell?: (row: T) => string;
}

export interface ArticleLink {
  id: string | number;
  title: string;
  url?: string;
  icon?: string;
}

export interface PrayerTime {
  name: string;
  time: string;          // "05:12 AM"
}

export interface WeatherInfo {
  city: string;
  temperatureC: number;
  condition: string;     // "Sunny"
  icon?: string;         // optional material icon override
  dateLabel?: string;    // "Tue 22 Sep"
}
