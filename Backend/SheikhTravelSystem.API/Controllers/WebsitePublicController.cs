using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.TripFeedback;
using SheikhTravelSystem.Application.Features.Website.Queries;

namespace SheikhTravelSystem.API.Controllers;

/// <summary>Anonymous public website CMS read APIs for sheikhgo.com.</summary>
[Route("api/website/public")]
[EnableRateLimiting("public")]
[AllowAnonymous]
public class WebsitePublicController : BaseApiController
{
    /// <summary>Public Nexus list prices (USD) + FX for Pakistan PKR display.</summary>
    [HttpGet("pricing")]
    public IActionResult GetPricing()
        => Ok(new
        {
            currencyPrimary = SubscriptionPlanPricing.CurrencyUsd,
            usdToPkr = SubscriptionPlanPricing.UsdToPkrRate,
            annualDiscountPercent = SubscriptionPlanPricing.AnnualDiscountPercent,
            proPlatformFeeMonthlyUsd = SubscriptionPlanPricing.ProPlatformFeeMonthlyUsd,
            tiers = new[]
            {
                new
                {
                    code = "STARTER",
                    name = "Starter",
                    monthlyPerVehicleUsd = SubscriptionPlanPricing.StarterMonthlyPerVehicleUsd,
                    yearlyPerVehicleUsd = SubscriptionPlanPricing.ResolveYearlyPerDevice("STARTER"),
                    platformFeeMonthlyUsd = 0m
                },
                new
                {
                    code = "PRO",
                    name = "Pro",
                    monthlyPerVehicleUsd = SubscriptionPlanPricing.ProMonthlyPerVehicleUsd,
                    yearlyPerVehicleUsd = SubscriptionPlanPricing.ResolveYearlyPerDevice("PRO"),
                    platformFeeMonthlyUsd = SubscriptionPlanPricing.ProPlatformFeeMonthlyUsd
                },
                new
                {
                    code = "ENTERPRISE",
                    name = "Enterprise",
                    monthlyPerVehicleUsd = SubscriptionPlanPricing.EnterpriseMonthlyPerVehicleUsd,
                    yearlyPerVehicleUsd = SubscriptionPlanPricing.ResolveYearlyPerDevice("ENTERPRISE"),
                    platformFeeMonthlyUsd = 0m
                }
            },
            addOns = new object[]
            {
                new { code = "AI_OCR", monthlyPerVehicleUsd = 4m, note = "AI Document OCR & Smart Fleet Assistant" },
                new { code = "SMS_WHATSAPP_PACK", usdPerPack = 5m, messagesPerPack = 250, note = "SMS / WhatsApp Alert Pack" }
            }
        });

    [HttpGet("home")]
    public async Task<IActionResult> GetHome()
        => Ok(await Mediator.Send(new GetPublicHomeQuery()));

    [HttpGet("pages/{slug}")]
    public async Task<IActionResult> GetPage(string slug)
        => Ok(await Mediator.Send(new GetPublicPageQuery(slug)));

    [HttpGet("features")]
    public async Task<IActionResult> GetFeatures()
        => Ok(await Mediator.Send(new GetPublicFeaturesQuery()));

    [HttpGet("legal/{docType}")]
    public async Task<IActionResult> GetLegal(string docType)
        => Ok(await Mediator.Send(new GetPublicLegalQuery(docType)));

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings()
        => Ok(await Mediator.Send(new GetPublicSettingsQuery()));

    [HttpGet("driver-ratings")]
    public async Task<IActionResult> GetDriverRatings([FromQuery] int take = 12)
        => Ok(await Mediator.Send(new GetPublicDriverRatingsQuery(take)));
}
