export type TripStatus =
  | 'Draft'
  | 'Scheduled'
  | 'DriverAssigned'
  | 'VehicleAssigned'
  | 'Started'
  | 'AtPickup'
  | 'Enroute'
  | 'Delayed'
  | 'Completed'
  | 'Cancelled'
  | 'Failed';

export type TripType =
  | 'Rental'
  | 'Transfer'
  | 'Tour'
  | 'Shuttle'
  | 'EmployeeTransport'
  | 'SchoolTransport';

export type TripPriority = 'Low' | 'Normal' | 'High' | 'Urgent';

export interface TripListItem {
  id: number;
  tripNumber: string;
  bookingId?: number | null;
  bookingNumber?: string | null;
  customerId: number;
  customerName?: string | null;
  driverId?: number | null;
  driverName?: string | null;
  vehicleId?: number | null;
  vehicleName?: string | null;
  routeId?: number | null;
  routeName?: string | null;
  pickupAddress?: string | null;
  destinationAddress?: string | null;
  tripDate: string;
  plannedStart: string;
  plannedEnd?: string | null;
  status: TripStatus;
  gpsOnline: boolean;
  tripType: TripType;
  priority: TripPriority;
}

export interface TripStop {
  id: number;
  sequence: number;
  location: string;
  latitude?: number | null;
  longitude?: number | null;
  eta?: string | null;
  arrivalTime?: string | null;
  departureTime?: string | null;
}

export interface TripStatusHistory {
  id: number;
  fromStatus?: TripStatus | null;
  toStatus: TripStatus;
  changedAtUtc: string;
  changedBy?: string | null;
  note?: string | null;
}

export interface TripDetail extends TripListItem {
  tripName: string;
  pickupLatitude?: number | null;
  pickupLongitude?: number | null;
  destinationLatitude?: number | null;
  destinationLongitude?: number | null;
  estimatedDurationMinutes?: number | null;
  assistantDriverId?: number | null;
  assistantDriverName?: string | null;
  passengerCount: number;
  driverNotes?: string | null;
  plannedDistanceKm?: number | null;
  actualDistanceKm?: number | null;
  actualStart?: string | null;
  actualEnd?: string | null;
  cancellationReason?: string | null;
  createdAt: string;
  stops: TripStop[];
  timeline: TripStatusHistory[];
  expenses: TripExpense[];
  documents: TripDocument[];
  passengers: TripPassenger[];
  openAlertCount: number;
}

export interface TripExpense {
  id: number;
  expenseType: string;
  amount: number;
  description?: string | null;
  expenseDate: string;
  createdAt: string;
}

export interface TripDocument {
  id: number;
  documentType: string;
  fileName: string;
  fileUrl: string;
  uploadedBy?: string | null;
  createdAt: string;
}

export interface TripPassenger {
  id: number;
  fullName: string;
  phone?: string | null;
  boardingStatus: string;
  dropStatus: string;
  notes?: string | null;
}

export const TRIP_EXPENSE_TYPES = ['Fuel', 'Toll', 'Parking', 'Food', 'Hotel', 'Other'] as const;
export const TRIP_DOCUMENT_TYPES = ['TripSheet', 'Invoice', 'DeliveryNote', 'CustomerSignature', 'VehiclePhoto', 'Other'] as const;
export const BOARDING_STATUSES = ['Pending', 'Boarded', 'NoShow'] as const;
export const DROP_STATUSES = ['Pending', 'Dropped', 'Skipped'] as const;

export interface TripDashboard {
  totalTrips: number;
  scheduledTrips: number;
  ongoingTrips: number;
  completedTrips: number;
  cancelledTrips: number;
  delayedTrips: number;
  todaysTrips: number;
  upcomingTrips: number;
}

export interface TripStopInput {
  sequence: number;
  location: string;
  latitude?: number | null;
  longitude?: number | null;
  eta?: string | null;
}

export interface CreateTripDto {
  tripName: string;
  tripType: TripType;
  bookingId?: number | null;
  customerId: number;
  routeId?: number | null;
  passengerCount: number;
  priority: TripPriority;
  pickupAddress?: string | null;
  pickupLatitude?: number | null;
  pickupLongitude?: number | null;
  destinationAddress?: string | null;
  destinationLatitude?: number | null;
  destinationLongitude?: number | null;
  tripDate: string;
  plannedStart: string;
  plannedEnd?: string | null;
  estimatedDurationMinutes?: number | null;
  plannedDistanceKm?: number | null;
  driverNotes?: string | null;
  driverId?: number | null;
  assistantDriverId?: number | null;
  vehicleId?: number | null;
  stops?: TripStopInput[] | null;
}

export interface UpdateTripDto extends Omit<CreateTripDto, 'bookingId' | 'driverId' | 'assistantDriverId' | 'vehicleId'> {}

export interface TripFilter {
  status?: TripStatus | '';
  driverId?: number | null;
  vehicleId?: number | null;
  routeId?: number | null;
  customerId?: number | null;
  dateFrom?: string | null;
  dateTo?: string | null;
  search?: string;
  todayOnly?: boolean;
  tomorrowOnly?: boolean;
  upcomingOnly?: boolean;
}

export interface TripCalendarItem {
  id: number;
  tripNumber: string;
  tripName: string;
  tripDate: string;
  plannedStart: string;
  plannedEnd?: string | null;
  status: TripStatus;
  customerName?: string | null;
  driverName?: string | null;
  vehicleName?: string | null;
  priority: TripPriority;
}

export interface TripNamedCount {
  name: string;
  count: number;
}

export interface TripAnalytics {
  from: string;
  to: string;
  totalTrips: number;
  completedTrips: number;
  cancelledTrips: number;
  delayedTrips: number;
  ongoingTrips: number;
  completionRate: number;
  totalPlannedDistanceKm?: number | null;
  totalActualDistanceKm?: number | null;
  totalExpenses: number;
  byStatus: TripNamedCount[];
  byType: TripNamedCount[];
  byDriver: TripNamedCount[];
  byVehicle: TripNamedCount[];
}

export interface TripRouteSummary {
  tripId: number;
  tripNumber: string;
  routeId?: number | null;
  routeName?: string | null;
  plannedDistanceKm?: number | null;
  estimatedDurationMinutes?: number | null;
  actualDistanceKm?: number | null;
  remainingDistanceKm?: number | null;
  distanceCoveredKm?: number | null;
  etaMinutes?: number | null;
  liveLatitude?: number | null;
  liveLongitude?: number | null;
  liveSpeedKmh?: number | null;
  ignition?: boolean | null;
  lastGpsAt?: string | null;
  googleMapsUrl?: string | null;
  googleDirectionsUrl?: string | null;
  hasCoordinates: boolean;
  canOptimize: boolean;
}

export const TRIP_STATUSES: TripStatus[] = [
  'Draft', 'Scheduled', 'DriverAssigned', 'VehicleAssigned', 'Started',
  'AtPickup', 'Enroute', 'Delayed', 'Completed', 'Cancelled', 'Failed'
];

export const TRIP_TYPES: TripType[] = [
  'Rental', 'Transfer', 'Tour', 'Shuttle', 'EmployeeTransport', 'SchoolTransport'
];

export const TRIP_PRIORITIES: TripPriority[] = ['Low', 'Normal', 'High', 'Urgent'];
