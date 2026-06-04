using System.Security.Cryptography;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
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
    IConfiguration configuration,
    ILogger<SendPortalOtpCommandHandler> logger)
    : IRequestHandler<SendPortalOtpCommand, ApiResponse<PortalOtpSentDto>>
{
    public Task<ApiResponse<PortalOtpSentDto>> Handle(SendPortalOtpCommand request, CancellationToken cancellationToken)
    {
        var phone = request.Phone.Trim();
        var devMode = !bool.TryParse(configuration["PortalAuth:DevMode"], out var devFlag) || devFlag;
        var devCode = configuration["PortalAuth:DevOtpCode"] ?? "123456";
        var code = devMode
            ? devCode
            : RandomNumberGenerator.GetInt32(100_000, 999_999).ToString();

        otpService.Store(phone, code);

        if (devMode)
        {
            logger.LogInformation("Portal OTP for {Phone} (dev mode): {Code}", phone, code);
        }
        else
        {
            logger.LogInformation("Portal OTP sent for {Phone}", phone);
        }

        var dto = new PortalOtpSentDto(
            phone,
            devMode,
            devMode ? "Use the dev OTP from appsettings PortalAuth:DevOtpCode." : "OTP sent via SMS.");

        return Task.FromResult(ApiResponse<PortalOtpSentDto>.SuccessResponse(dto, "OTP sent."));
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
    IJwtTokenService jwtTokenService)
    : IRequestHandler<VerifyPortalOtpCommand, ApiResponse<PortalAuthResultDto>>
{
    public Task<ApiResponse<PortalAuthResultDto>> Handle(VerifyPortalOtpCommand request, CancellationToken cancellationToken)
    {
        if (!otpService.TryValidate(request.Phone.Trim(), request.Code.Trim(), out var error))
        {
            return Task.FromResult(ApiResponse<PortalAuthResultDto>.FailResponse(error ?? "Invalid OTP."));
        }

        var token = jwtTokenService.GeneratePortalAccessToken(request.Phone.Trim(), request.FullName.Trim());
        var dto = new PortalAuthResultDto(request.Phone.Trim(), request.FullName.Trim(), token);
        return Task.FromResult(ApiResponse<PortalAuthResultDto>.SuccessResponse(dto, "Signed in."));
    }
}
