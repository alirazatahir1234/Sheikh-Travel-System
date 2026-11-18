using FluentAssertions;
using Moq;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Users.DTOs;
using SheikhTravelSystem.Application.Features.Users.Queries;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Tests.Security.Sg01;

/// <summary>
/// SG-01 — GetUserById used to skip the tenant check when the user had no company, returning it to
/// anyone with Users.View. A company-less user now requires the platform-level
/// <see cref="PlatformPermissions.TenantsDataRead"/> permission.
/// </summary>
public class Sg01GetUserByIdAccessTests
{
    private const int UserId = 55;

    private static UserDto User(int? companyId) =>
        new(UserId, "Someone", "someone@test.local", "", UserRole.Admin, true, DateTime.UtcNow, CompanyId: companyId);

    private static (GetUserByIdQueryHandler Handler, Mock<IPlatformScope> Scope) Arrange(int? companyId, bool platformReader)
    {
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByIdAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(User(companyId));

        var scope = new Mock<IPlatformScope>();
        scope.Setup(s => s.HasPermission(PlatformPermissions.TenantsDataRead)).Returns(platformReader);
        return (new GetUserByIdQueryHandler(repo.Object, scope.Object), scope);
    }

    [Fact]
    public async Task Company_user_without_platform_permission_cannot_read_a_company_less_user()
    {
        var (handler, scope) = Arrange(companyId: null, platformReader: false);

        var act = () => handler.Handle(new GetUserByIdQuery(UserId), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        scope.Verify(s => s.HasPermission(PlatformPermissions.TenantsDataRead), Times.Once);
    }

    [Fact]
    public async Task Platform_reader_can_read_a_company_less_user()
    {
        var (handler, _) = Arrange(companyId: null, platformReader: true);

        var result = await handler.Handle(new GetUserByIdQuery(UserId), CancellationToken.None);

        result.Data!.Id.Should().Be(UserId);
    }

    [Fact]
    public async Task Company_id_zero_is_treated_as_company_less()
    {
        var (handler, _) = Arrange(companyId: 0, platformReader: false);

        var act = () => handler.Handle(new GetUserByIdQuery(UserId), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task User_of_another_company_still_goes_through_tenant_access_check()
    {
        var (handler, scope) = Arrange(companyId: 9, platformReader: false);
        scope.Setup(s => s.EnsureTenantAccess(9)).Throws(new ForbiddenException());

        var act = () => handler.Handle(new GetUserByIdQuery(UserId), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        scope.Verify(s => s.HasPermission(It.IsAny<string>()), Times.Never,
            "a company user is authorised by tenant access, not by the company-less branch");
    }

    [Fact]
    public async Task User_of_the_callers_own_company_is_returned()
    {
        var (handler, scope) = Arrange(companyId: 3, platformReader: false);

        var result = await handler.Handle(new GetUserByIdQuery(UserId), CancellationToken.None);

        result.Data!.Id.Should().Be(UserId);
        scope.Verify(s => s.EnsureTenantAccess(3), Times.Once);
    }
}
