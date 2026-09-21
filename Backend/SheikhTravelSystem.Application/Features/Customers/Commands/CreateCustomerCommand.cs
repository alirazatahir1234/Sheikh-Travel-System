using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Customers.DTOs;

namespace SheikhTravelSystem.Application.Features.Customers.Commands;

public record CreateCustomerCommand(CreateCustomerDto Customer) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "Create";
    public string AuditEntityName => "Customer";
    public int? AuditEntityId => null;
}

public class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator()
    {
        RuleFor(x => x.Customer.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Customer.Phone).NotEmpty();
        RuleFor(x => x.Customer.FatherOrHusbandName).MaximumLength(200).When(x => !string.IsNullOrEmpty(x.Customer.FatherOrHusbandName));
        RuleFor(x => x.Customer.Gender).MaximumLength(20).When(x => !string.IsNullOrEmpty(x.Customer.Gender));
        RuleFor(x => x.Customer.Nationality).MaximumLength(120).When(x => !string.IsNullOrEmpty(x.Customer.Nationality));
    }
}

public class CreateCustomerCommandHandler(ICustomerRepository customerRepository)
    : IRequestHandler<CreateCustomerCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        var result = await customerRepository.CreateAsync(request.Customer, cancellationToken);
        if (!result.Success)
            return ApiResponse<int>.FailResponse(result.ErrorMessage!);

        return ApiResponse<int>.SuccessResponse(result.Data, "Customer created successfully.");
    }
}
