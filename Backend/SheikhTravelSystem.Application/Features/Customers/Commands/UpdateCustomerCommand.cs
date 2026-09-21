using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Customers.DTOs;

namespace SheikhTravelSystem.Application.Features.Customers.Commands;

public record UpdateCustomerCommand(int Id, UpdateCustomerDto Customer) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Update";
    public string AuditEntityName => "Customer";
    public int? AuditEntityId => Id;
}

public class UpdateCustomerCommandValidator : AbstractValidator<UpdateCustomerCommand>
{
    public UpdateCustomerCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Customer.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Customer.Phone).NotEmpty();
        RuleFor(x => x.Customer.FatherOrHusbandName).MaximumLength(200).When(x => !string.IsNullOrEmpty(x.Customer.FatherOrHusbandName));
        RuleFor(x => x.Customer.Gender).MaximumLength(20).When(x => !string.IsNullOrEmpty(x.Customer.Gender));
        RuleFor(x => x.Customer.Nationality).MaximumLength(120).When(x => !string.IsNullOrEmpty(x.Customer.Nationality));
    }
}

public class UpdateCustomerCommandHandler(ICustomerRepository customerRepository)
    : IRequestHandler<UpdateCustomerCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateCustomerCommand request, CancellationToken cancellationToken)
    {
        var result = await customerRepository.UpdateAsync(request.Id, request.Customer, cancellationToken);
        if (!result.Success)
            return ApiResponse<bool>.FailResponse(result.ErrorMessage!);

        return ApiResponse<bool>.SuccessResponse(true, "Customer updated successfully.");
    }
}
