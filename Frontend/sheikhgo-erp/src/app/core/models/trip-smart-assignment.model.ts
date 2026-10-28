export interface TripAssignmentScoreFactor {
  key: string;
  label: string;
  weight: number;
  rawScore: number;
  weightedScore: number;
  detail: string;
}

export interface TripAssignmentCandidate {
  driverId: number;
  driverName: string;
  vehicleId?: number | null;
  vehicleName?: string | null;
  totalScore: number;
  rank: number;
  distanceKm?: number | null;
  etaMinutes?: number | null;
  trafficSource: string;
  factors: TripAssignmentScoreFactor[];
}

export interface TripAssignmentExclusion {
  driverId: number;
  driverName: string;
  vehicleId?: number | null;
  vehicleName?: string | null;
  reason: string;
  detail: string;
}

export interface TripSmartAssignmentSuggestions {
  tripId: number;
  tripNumber: string;
  candidates: TripAssignmentCandidate[];
  routingNote?: string | null;
  mode?: string;
  excluded?: TripAssignmentExclusion[] | null;
  exclusionCounts?: Record<string, number> | null;
}

export interface TripSmartAssignmentApplyResult {
  tripId: number;
  driverId: number;
  driverName: string;
  vehicleId?: number | null;
  vehicleName?: string | null;
  totalScore: number;
  factors: TripAssignmentScoreFactor[];
  attemptNumber: number;
}

export interface TripSmartAssignmentDecision {
  id: number;
  tripId: number;
  tripNumber?: string | null;
  driverId: number;
  driverName?: string | null;
  vehicleId?: number | null;
  vehicleName?: string | null;
  rank: number;
  totalScore: number;
  factors: TripAssignmentScoreFactor[];
  distanceKm?: number | null;
  etaMinutes?: number | null;
  trafficSource: string;
  outcome: string;
  rejectionReason?: string | null;
  createdBy?: string | null;
  createdAtUtc: string;
}

export interface OverrideTripSmartAssignmentRequest {
  driverId: number;
  vehicleId?: number | null;
  reason?: string | null;
}

export interface ConfirmTripSmartAssignmentRequest {
  driverId: number;
  vehicleId?: number | null;
}
