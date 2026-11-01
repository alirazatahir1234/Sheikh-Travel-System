import { isDriverPrimaryAccount, rolesInclude } from '../auth/role-priority';

export enum TenantType {
  TravelAgency = 'travelAgency',
  FleetOperator = 'fleetOperator',
  CorporateCustomer = 'corporateCustomer',
  Driver = 'driver'
}

export function resolveTenantType(
  roles: string[],
  override?: TenantType
): TenantType {
  if (override) return override;
  if (isDriverPrimaryAccount(roles)) return TenantType.Driver;
  if (rolesInclude(roles, 'FleetManager', 'FLEET_MANAGER')) return TenantType.FleetOperator;
  if (rolesInclude(roles, 'CorporateUser', 'CORPORATE_USER')) return TenantType.CorporateCustomer;
  return TenantType.TravelAgency;
}

export function tenantProductSubtitle(type: TenantType): string {
  switch (type) {
    case TenantType.FleetOperator:
      return 'FLEET CONTROL';
    case TenantType.CorporateCustomer:
      return 'CORPORATE PORTAL';
    case TenantType.Driver:
      return 'DRIVER APP';
    default:
      return 'ERP';
  }
}
