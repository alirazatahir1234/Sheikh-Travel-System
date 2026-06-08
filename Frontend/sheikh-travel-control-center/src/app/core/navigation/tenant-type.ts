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
  if (roles.includes('Driver')) return TenantType.Driver;
  if (roles.includes('FleetManager')) return TenantType.FleetOperator;
  if (roles.includes('CorporateUser')) return TenantType.CorporateCustomer;
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
      return 'CONTROL CENTER';
  }
}
