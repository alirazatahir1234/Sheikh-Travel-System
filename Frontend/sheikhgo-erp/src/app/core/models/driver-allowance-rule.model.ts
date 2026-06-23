/**
 * Mirrors SheikhTravelSystem.Application.Features.DriverAllowance.DTOs.
 * Keep enum ids in sync with Domain.Enums.AllowanceCalculationType.
 */

export enum AllowanceCalculationType {
  FixedAmount    = 1,
  PerKm          = 2,
  PerDay         = 3,
  ProfitPercent  = 4
}

export const AllowanceCalculationTypeLabels: Record<AllowanceCalculationType, string> = {
  [AllowanceCalculationType.FixedAmount]:   'Fixed amount (PKR)',
  [AllowanceCalculationType.PerKm]:         'Per kilometer (PKR/km)',
  [AllowanceCalculationType.PerDay]:        'Per day (PKR/day)',
  [AllowanceCalculationType.ProfitPercent]: 'Profit share (%)'
};

/** Short unit label rendered next to the `value` field in the UI. */
export const AllowanceCalculationUnit: Record<AllowanceCalculationType, string> = {
  [AllowanceCalculationType.FixedAmount]:   'PKR',
  [AllowanceCalculationType.PerKm]:         'PKR/km',
  [AllowanceCalculationType.PerDay]:        'PKR/day',
  [AllowanceCalculationType.ProfitPercent]: '%'
};

export interface DriverAllowanceRule {
  id: number;
  name: string;
  calculationType: AllowanceCalculationType;
  value: number;
  priority: number;
  minDistanceKm?: number | null;
  maxDistanceKm?: number | null;
  vehicleFuelType?: number | null;
  routeFilter?: string | null;
  isActive: boolean;
  notes?: string | null;
  createdAt: string;
}

export interface CreateDriverAllowanceRuleDto {
  name: string;
  calculationType: AllowanceCalculationType;
  value: number;
  priority: number;
  minDistanceKm?: number | null;
  maxDistanceKm?: number | null;
  vehicleFuelType?: number | null;
  routeFilter?: string | null;
  notes?: string | null;
}

export interface UpdateDriverAllowanceRuleDto extends CreateDriverAllowanceRuleDto {
  isActive: boolean;
}

export interface CreateDriverAllowanceRuleRequest {
  rule: CreateDriverAllowanceRuleDto;
}

export interface UpdateDriverAllowanceRuleRequest {
  id: number;
  rule: UpdateDriverAllowanceRuleDto;
}

/** Request sent to POST /api/DriverAllowanceRules/calculate. */
export interface CalculateDriverAllowanceRequest {
  routeId: number;
  vehicleId: number;
  tripDays?: number | null;
  profit?: number | null;
}

/** Response returned by the evaluator — auditable metadata. */
export interface CalculateDriverAllowanceResponse {
  amount: number;
  appliedRuleId?: number | null;
  appliedRuleName?: string | null;
  appliedRuleType?: AllowanceCalculationType | null;
  appliedRuleValue?: number | null;
  formulaExplanation: string;
  matchedAnyRule: boolean;
}
