import { CreateTripDto, TripDocument } from './trip.model';

export interface TripInsights {
  invoicePaymentStatus?: string | null;
  invoiceAmount?: number | null;
  feedbackStatus: string;
  profitability: TripProfitability;
  routeDeviation: TripRouteDeviation;
  liveEvents: TripLiveEvent[];
  driverRejects: TripDriverReject[];
  assignmentConflict?: TripAssignmentConflict | null;
  proofOfDeliveryDocuments: TripDocument[];
}

export interface TripProfitability {
  estimatedRevenue?: number | null;
  totalExpenses: number;
  margin?: number | null;
  marginPercent?: number | null;
}

export interface TripRouteDeviation {
  plannedDistanceKm?: number | null;
  actualDistanceKm?: number | null;
  deviationKm?: number | null;
  deviationPercent?: number | null;
  summary: string;
}

export interface TripLiveEvent {
  eventType: string;
  title: string;
  detail?: string | null;
  occurredAtUtc: string;
  source: string;
}

export interface TripDriverReject {
  driverId: number;
  driverName?: string | null;
  rejectionReason?: string | null;
  rejectedAtUtc: string;
}

export interface TripAssignmentConflict {
  hasConflict: boolean;
  message?: string | null;
  conflictingTripId?: number | null;
  conflictingTripNumber?: string | null;
}

export interface TripCostEstimate {
  distanceKm?: number | null;
  fuelCost?: number | null;
  driverAllowance: number;
  tollCharges: number;
  otherCharges: number;
  totalEstimatedCost: number;
  source: string;
  note?: string | null;
}

export interface TripCostEstimateRequest {
  routeId?: number | null;
  vehicleId?: number | null;
  plannedDistanceKm?: number | null;
  isRoundTrip?: boolean;
  fuelPricePerLiter?: number;
  driverAllowance?: number;
  tollCharges?: number;
  otherCharges?: number;
}

export interface TripTemplate {
  id: number;
  name: string;
  recurrenceKind: string;
  recurrenceInterval: number;
  recurrenceDaysOfWeek?: string | null;
  nextOccurrenceDate?: string | null;
  isActive: boolean;
  createdAt: string;
}

export interface UpsertTripTemplate {
  id?: number | null;
  name: string;
  payload: CreateTripDto;
  recurrenceKind: string;
  recurrenceInterval: number;
  recurrenceDaysOfWeek?: string | null;
  nextOccurrenceDate?: string | null;
  isActive: boolean;
}

export interface BulkTripImportResult {
  created: number;
  failed: number;
  errors: string[];
}

export const TRIP_CARGO_FLAG_OPTIONS = [
  'Hazmat',
  'ColdChain',
  'Oversized',
  'Fragile',
  'HighValue',
  'LiveAnimals'
] as const;

export type TripCargoFlag = (typeof TRIP_CARGO_FLAG_OPTIONS)[number];

export const TRIP_SAVED_FILTERS_KEY = 'sheikhgo_trips_saved_filters';

export interface TripSavedFilter {
  name: string;
  query: Record<string, string | number | boolean | null>;
}
