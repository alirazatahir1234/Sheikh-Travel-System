using FluentAssertions;
using Moq;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Customers.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Customers.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Customers.Queries;

namespace SheikhTravelSystem.Tests.TravelNexus;

public class TravelCustomerHandlerTests
{
    private const int TenantA = 3;
    private const int TenantB = 7;

    private readonly Mock<ITravelCustomerRepository> _repo = new();
    private readonly Mock<ITravelBranchScopeResolver> _scope = new();
    private readonly Mock<ITenantContext> _tenant = new();
    private readonly Mock<ICurrentUserService> _user = new();

    public TravelCustomerHandlerTests()
    {
        _tenant.Setup(t => t.GetRequiredTenantId()).Returns(TenantA);
        _user.Setup(u => u.UserId).Returns(11);
        _scope.Setup(s => s.ResolveAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TravelBranchScope.CompanyWide);
    }

    private static CreateTravelCustomerDto NewCustomer(int? existingId = null, int? branchId = null, string? cnic = null) =>
        new(existingId, "Individual", "Ahmed Khan", "+92 300 1234567", null, null, "Lahore", cnic,
            null, null, null, null, branchId, null, null);

    private CreateTravelCustomerCommandHandler CreateHandler() => new(_repo.Object, _scope.Object, _tenant.Object, _user.Object);

    [Fact]
    public async Task Create_uses_tenant_from_context_and_returns_customer_id()
    {
        _repo.Setup(r => r.CreateAsync(TenantA, It.IsAny<CreateTravelCustomerDto>(), 11, It.IsAny<CancellationToken>()))
            .ReturnsAsync(501);

        var result = await CreateHandler().Handle(new CreateTravelCustomerCommand(NewCustomer()), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data.Should().Be(501);
        _repo.Verify(r => r.CreateAsync(TenantA, It.IsAny<CreateTravelCustomerDto>(), 11, It.IsAny<CancellationToken>()), Times.Once);
        _repo.Verify(r => r.CreateAsync(It.Is<int>(t => t != TenantA), It.IsAny<CreateTravelCustomerDto>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_rejects_branch_outside_users_scope()
    {
        _scope.Setup(s => s.ResolveAsync(TenantA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelBranchScope(false, [1, 2]));

        var act = () => CreateHandler().Handle(new CreateTravelCustomerCommand(NewCustomer(branchId: 9)), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        _repo.Verify(r => r.CreateAsync(It.IsAny<int>(), It.IsAny<CreateTravelCustomerDto>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_rejects_duplicate_cnic_in_same_tenant()
    {
        _repo.Setup(r => r.FindCustomerIdByCnicAsync(TenantA, "3520212345671", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(77);

        var result = await CreateHandler().Handle(new CreateTravelCustomerCommand(NewCustomer(cnic: "3520212345671")), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("#77");
    }

    [Fact]
    public async Task Create_linking_existing_customer_of_other_tenant_is_not_found()
    {
        // Repository is tenant-scoped: customer 900 belongs to tenant B, so tenant A sees NotFound.
        _repo.Setup(r => r.GetExistingCustomerStateAsync(900, TenantA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExistingCustomerState.NotFound);

        var act = () => CreateHandler().Handle(new CreateTravelCustomerCommand(NewCustomer(existingId: 900)), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Create_linking_customer_that_is_already_travel_customer_fails()
    {
        _repo.Setup(r => r.GetExistingCustomerStateAsync(12, TenantA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExistingCustomerState.AlreadyTravelCustomer);

        var result = await CreateHandler().Handle(new CreateTravelCustomerCommand(NewCustomer(existingId: 12)), CancellationToken.None);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task GetById_for_another_tenants_customer_is_not_found()
    {
        _tenant.Setup(t => t.GetRequiredTenantId()).Returns(TenantB);
        _repo.Setup(r => r.GetByIdAsync(42, TenantA, It.IsAny<TravelBranchScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelCustomerDto { CustomerId = 42 });

        var handler = new GetTravelCustomerByIdQueryHandler(_repo.Object, _scope.Object, _tenant.Object, _user.Object);
        var act = () => handler.Handle(new GetTravelCustomerByIdQuery(42), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        _repo.Verify(r => r.GetByIdAsync(42, TenantB, It.IsAny<TravelBranchScope>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(false, "••••••••••671", true)]
    [InlineData(true, "3520212345671", false)]
    public async Task GetById_masks_cnic_without_pii_permission(bool canViewPii, string expectedCnic, bool expectedMasked)
    {
        _user.Setup(u => u.HasPermission(TravelPermissions.TravelerViewPii)).Returns(canViewPii);
        _repo.Setup(r => r.GetByIdAsync(42, TenantA, It.IsAny<TravelBranchScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelCustomerDto { CustomerId = 42, Cnic = "3520212345671" });

        var handler = new GetTravelCustomerByIdQueryHandler(_repo.Object, _scope.Object, _tenant.Object, _user.Object);
        var result = await handler.Handle(new GetTravelCustomerByIdQuery(42), CancellationToken.None);

        result.Data!.Cnic.Should().Be(expectedCnic);
        result.Data.PiiMasked.Should().Be(expectedMasked);
    }

    [Fact]
    public async Task Update_with_masked_cnic_keeps_stored_value()
    {
        _repo.Setup(r => r.GetByIdAsync(42, TenantA, It.IsAny<TravelBranchScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelCustomerDto { CustomerId = 42 });
        _repo.Setup(r => r.GetCnicAsync(42, TenantA, It.IsAny<CancellationToken>())).ReturnsAsync("3520212345671");
        _repo.Setup(r => r.UpdateAsync(42, TenantA, It.IsAny<UpdateTravelCustomerDto>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var dto = new UpdateTravelCustomerDto("Individual", "Ahmed Khan", "03001234567", null, null, null,
            TravelPii.Mask("3520212345671"), null, null, null, null, null, null, null);
        var handler = new UpdateTravelCustomerCommandHandler(_repo.Object, _scope.Object, _tenant.Object);
        var result = await handler.Handle(new UpdateTravelCustomerCommand(42, dto), CancellationToken.None);

        result.Success.Should().BeTrue();
        _repo.Verify(r => r.UpdateAsync(42, TenantA, It.IsAny<UpdateTravelCustomerDto>(), "3520212345671", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Validator_rejects_unknown_kind_and_bad_phone()
    {
        var v = new CreateTravelCustomerCommandValidator();
        var bad = NewCustomer() with { CustomerKind = "Family", Phone = "abc" };
        var result = v.Validate(new CreateTravelCustomerCommand(bad));
        result.Errors.Select(e => e.PropertyName).Should().Contain(["Customer.CustomerKind", "Customer.Phone"]);
    }
}
