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

export type StatVariant = 'default' | 'success' | 'warning' | 'danger';

export interface StatTile {
  key: string;
  label: string;
  value: string | number;
  hint?: string;
  icon: string;
  color: AccentColor;
  prefix?: string;
  suffix?: string;
  /** e.g. "+12.4%" */
  trend?: string;
  trendUp?: boolean;
  /** e.g. "vs last week" */
  trendDetail?: string;
  /** Mini sparkline values (normalized 0–100 internally). */
  sparkline?: number[];
  variant?: StatVariant;
}

export type TaskPriority = 'high' | 'medium' | 'low';

export interface TaskItem {
  id: string | number;
  title: string;
  subtitle?: string;
  done?: boolean;
  meta?: string;         // right-aligned meta (e.g. driver name)
  avatarUrl?: string;
  avatarInitials?: string;
  priority?: TaskPriority;
  /** Booking status for row accent (Pending, Completed, Cancelled, etc.). */
  status?: string;
}

export interface DataTableColumn<T = any> {
  key: string;
  header: string;
  width?: string;
  align?: 'left' | 'right' | 'center';
  cell?: (row: T) => string;
  /** Renders value as a colored status badge (for booking status, etc.). */
  badge?: boolean;
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
