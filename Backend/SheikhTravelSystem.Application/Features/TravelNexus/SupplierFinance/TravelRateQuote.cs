using SheikhTravelSystem.Application.Features.TravelNexus.SupplierFinance.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.SupplierFinance;

/// <summary>Hotel cost from rate cards: per night, per room type × quantity, using the card valid on that night.</summary>
public static class TravelRateQuote
{
    public static TravelRateQuoteDto Quote(IReadOnlyList<TravelSupplierRateDto> rates, int hotelId, DateTime checkIn, DateTime checkOut,
        IReadOnlyList<TravelRateQuoteRoomDto> rooms)
    {
        var applicable = rates.Where(r => r.IsActive && r.RateType == "HotelRoomNight" && r.HotelId == hotelId).ToList();
        var nights = Math.Max(0, (int)(checkOut.Date - checkIn.Date).TotalDays);
        var missing = new List<TravelRateQuoteGapDto>();
        var currencies = new HashSet<string>(StringComparer.Ordinal);
        decimal total = 0;
        for (var n = 0; n < nights; n++)
        {
            var night = checkIn.Date.AddDays(n);
            foreach (var room in rooms.Where(r => r.Quantity > 0))
            {
                // The most recently started card wins when cards overlap.
                var card = applicable.Where(r => r.RoomType == room.RoomType && r.ValidFrom.Date <= night && r.ValidTo.Date >= night)
                    .OrderByDescending(r => r.ValidFrom).ThenByDescending(r => r.Id).FirstOrDefault();
                if (card is null) { missing.Add(new TravelRateQuoteGapDto(room.RoomType, night)); continue; }
                currencies.Add(card.CurrencyCode);
                total += card.Amount * room.Quantity;
            }
        }
        return currencies.Count > 1
            ? new TravelRateQuoteDto { MixedCurrencies = true, Nights = nights, Missing = missing }
            : new TravelRateQuoteDto { CurrencyCode = currencies.FirstOrDefault(), Total = Math.Round(total, 2, MidpointRounding.AwayFromZero), Nights = nights, Missing = missing };
    }
}
