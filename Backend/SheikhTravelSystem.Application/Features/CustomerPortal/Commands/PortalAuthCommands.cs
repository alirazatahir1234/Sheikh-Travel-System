using System.Security.Cryptography;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.CustomerPortal.DTOs;

namespace SheikhTravelSystem.Application.Features.CustomerPortal.Commands;

public record SendPortalOtpCommand(string Phone) : IRequest<ApiResponse<PortalOtpSentDto>>;

public class SendPortalOtpCommandValidator : AbstractValidator<SendPortalOtpCommand>
{
    public SendPortalOtpCommandValidator()
    {
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(20);
    }
}

public class SendPortalOtpCommandHandler(
    IPortalOtpService otpService,
    ISmsOtpService smsOtpService,
    IConfiguration configuration,
    IHostEnvironment hostEnvironment,
    ILogger<SendPortalOtpCommandHandler> logger)
    : IRequestHandler<SendPortalOtpCommand, ApiResponse<PortalOtpSentDto>>
{
    public async Task<ApiResponse<PortalOtpSentDto>> Handle(SendPortalOtpCommand request, CancellationToken cancellationToken)
    {
        var phone = request.Phone.Trim();
        // OTP bypass is Development-only. Staging/Production ignore PortalAuth:DevMode / DevOtpCode.
        var configDevMode = configuration.GetValue("PortalAuth:DevMode", false);
        var allowDevBypass = hostEnvironment.IsDevelopment() && configDevMode;
        var devCode = configuration["PortalAuth:DevOtpCode"];
        var code = allowDevBypass && !string.IsNullOrWhiteSpace(devCode)
            ? devCode.Trim()
            : RandomNumberGenerator.GetInt32(100_000, 999_999).ToString();

        otpService.Store(phone, code);

        if (allowDevBypass)
        {
            logger.LogInformation("Portal OTP issued in Development DevMode for {Phone} (code not logged).", phone);
        }
        else
        {
            if (configDevMode && !hostEnvironment.IsDevelopment())
            {
                logger.LogWarning(
                    "PortalAuth:DevMode is configured but ignored because environment is {Environment}.",
                    hostEnvironment.EnvironmentName);
            }

            await smsOtpService.SendOtpAsync(phone, code, cancellationToken);
        }

        var dto = new PortalOtpSentDto(
            phone,
            allowDevBypass,
            allowDevBypass
                ? "Use the Development DevOtpCode from local secrets / appsettings.Development.json."
                : "OTP sent via SMS.");

        return ApiResponse<PortalOtpSentDto>.SuccessResponse(dto, "OTP sent.");
    }
}

public record VerifyPortalOtpCommand(string Phone, string Code, string FullName) : IRequest<ApiResponse<PortalAuthResultDto>>;

public class VerifyPortalOtpCommandValidator : AbstractValidator<VerifyPortalOtpCommand>
{
    public VerifyPortalOtpCommandValidator()
    {
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Code).NotEmpty().Length(6);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
    }
}

public class VerifyPortalOtpCommandHandler(
    IPortalOtpService otpService,
    IJwtTokenService jwtTokenService,
    ITenantContext tenantContext,
    ICustomerPortalRepository portalRepository)
    : IRequestHandler<VerifyPortalOtpCommand, ApiResponse<PortalAuthResultDto>>
{
    public async Task<ApiResponse<PortalAuthResultDto>> Handle(VerifyPortalOtpCommand request, CancellationToken cancellationToken)
    {
        var phone = PortalPhoneHelper.Normalize(request.Phone);
        if (!otpService.TryValidate(request.Phone.Trim(), request.Code.Trim(), out var error))
        {
            return ApiResponse<PortalAuthResultDto>.FailResponse(error ?? "Invalid OTP.");
        }

        var tenantId = tenantContext.GetRequiredTenantId();
        var customerId = await portalRepository.EnsureCustomerAsync(
            phone, request.FullName.Trim(), tenantId, cancellationToken);
        var token = jwtTokenService.GeneratePortalAccessToken(
            phone, request.FullName.Trim(), tenantId, customerId);
        var dto = new PortalAuthResultDto(phone, request.FullName.Trim(), token);
        return ApiResponse<PortalAuthResultDto>.SuccessResponse(dto, "Signed in.");
    }
}
