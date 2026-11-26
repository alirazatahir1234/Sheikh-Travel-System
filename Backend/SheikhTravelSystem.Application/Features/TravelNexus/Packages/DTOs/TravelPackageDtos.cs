namespace SheikhTravelSystem.Application.Features.TravelNexus.Packages.DTOs;

public class TravelPackageListItemDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>Umrah | Hajj.</summary>
    public string PackageType { get; set; } = string.Empty;
    public int DurationNights { get; set; }
    public int NightsMakkah { get; set; }
    public int NightsMadinah { get; set; }
    public int? VisaTypeId { get; set; }
    public string? VisaTypeName { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    /// <summary>Draft | Published | Archived.</summary>
    public string Status { get; set; } = string.Empty;
    /// <summary>Lowest adult price across room types (null when no prices).</summary>
    public decimal? FromPrice { get; set; }
    public int GroupCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TravelPackageDto : TravelPackageListItemDto
{
    public string? Inclusions { get; set; }
    public string? Exclusions { get; set; }
    public IReadOnlyList<TravelPackageStayDto> Stays { get; set; } = [];
    public IReadOnlyList<TravelPackagePriceDto> Prices { get; set; } = [];
}

public class TravelPackageStayDto
{
    public int HotelId { get; set; }
    public string HotelName { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public int Nights { get; set; }
    public int Sequence { get; set; }
}

public class TravelPackagePriceDto
{
    /// <summary>Quad | Triple | Double | Single.</summary>
    public string RoomType { get; set; } = string.Empty;
    public decimal AdultPrice { get; set; }
    public decimal ChildPrice { get; set; }
    public decimal InfantPrice { get; set; }
}

public record SaveTravelPackageStayDto(int HotelId, int Nights);
public record SaveTravelPackagePriceDto(string RoomType, decimal AdultPrice, decimal ChildPrice, decimal InfantPrice);

public record SaveTravelPackageDto(
    string Code,
    string Name,
    string PackageType,
    int DurationNights,
    int NightsMakkah,
    int NightsMadinah,
    int? VisaTypeId,
    string? Inclusions,
    string? Exclusions,
    string CurrencyCode,
    IReadOnlyList<SaveTravelPackageStayDto> Stays,
    IReadOnlyList<SaveTravelPackagePriceDto> Prices);
