using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.Customers.Commands;

/// <summary>
/// Soft-deletes a customer by identifier (sets IsDeleted = 1).
/// </summary>
public record DeleteCustomerCommand(int Id) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Delete";
    public string AuditEntityName => "Customer";
    public int? AuditEntityId => Id;
}

public class DeleteCustomerCommandHandler(ICustomerRepository customerRepository)
    : IRequestHandler<DeleteCustomerCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteCustomerCommand request, CancellationToken cancellationToken)
    {
        await customerRepository.SoftDeleteAsync(request.Id, cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true, "Customer deleted successfully.");
    }
}
