namespace SheikhTravelSystem.Domain.Enums;

public enum TenantStatus
{
    Trial = 1,
    Active = 2,
    Suspended = 3,
    Expired = 4,
    Cancelled = 5
}

public enum TenantStorageModel
{
    SharedDatabase = 1,
    DedicatedSchema = 2,
    DedicatedDatabase = 3
}
