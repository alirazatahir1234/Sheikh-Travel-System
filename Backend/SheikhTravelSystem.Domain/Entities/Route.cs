using SheikhTravelSystem.Domain.Common;

namespace SheikhTravelSystem.Domain.Entities;

public class Route : BaseEntity
{
    public string Source { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public decimal Distance { get; set; }
    public decimal BasePrice { get; set; }
    public bool IsActive { get; set; } = true;
}
