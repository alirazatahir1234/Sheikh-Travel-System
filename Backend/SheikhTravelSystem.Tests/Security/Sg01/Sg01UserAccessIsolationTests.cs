using System.Text.RegularExpressions;
using FluentAssertions;
using Moq;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Users.Commands;

namespace SheikhTravelSystem.Tests.Security.Sg01;

/// <summary>
/// SG-01 Pass 2, step 4 — user administration and role assignment.
/// Admin writes must carry the tenant that the handler authorised, so the UPDATE itself cannot
/// touch another tenant's user even if a future caller skips the check. Role reads/writes must
/// keep the user and the role in one tenant.
/// </summary>
public class Sg01UserAccessIsolationTests
{
    private static readonly string Backend = FindBackendRoot();

    // ---- SQL text -------------------------------------------------------------------------

    [Theory]
    [InlineData("SoftDeleteAsync")]
    [InlineData("UpdateStatusAsync")]
    [InlineData("AdminResetPasswordAsync")]
    public void Admin_user_writes_are_scoped_to_the_authorised_tenant(string method)
    {
        var body = MethodBody("UserRepository.cs", method);
        foreach (System.Text.RegularExpressions.Match update in Regex.Matches(body, @"UPDATE Users[^""]*?WHERE[^""]*", RegexOptions.Singleline))
            Normalize(update.Value).Should().Contain("WHERE Id = @Id AND TenantId = @TenantId",
                because: $"UserRepository::{method} must not update a user outside the authorised tenant");

        body.Should().Contain("throw new NotFoundException(\"User\"",
            because: "a write that matched no row in the tenant must fail loudly, not silently succeed");
    }

    [Theory]
    [InlineData("LoadAssignedRolesMapAsync", 1)]
    [InlineData("LoadAssignedAsync", 2)]
    public void Assigned_role_reads_only_join_roles_of_the_user_tenant(string method, int statements)
    {
        var sql = Normalize(MethodBody("UserRepository.cs", method));
        Regex.Matches(sql, Regex.Escape("INNER JOIN Roles r ON r.Id = ur.RoleId AND r.TenantId = u.TenantId"))
            .Count.Should().Be(statements);
        Regex.Matches(sql, Regex.Escape("INNER JOIN Users u ON u.Id = ur.UserId"))
            .Count.Should().Be(statements);
    }

    [Fact]
    public void Role_assignment_insert_requires_user_and_role_in_the_same_tenant()
    {
        var body = MethodBody("UserRepository.cs", "InsertAssignmentAsync", lastOverload: true);
        var sql = Normalize(body);

        Regex.Matches(sql, Regex.Escape("INNER JOIN Roles r ON r.Id = @RoleId AND r.TenantId = u.TenantId"))
            .Count.Should().Be(2, because: "both the rich and the fallback INSERT must be guarded");
        sql.Should().NotContain("VALUES (@UserId, @RoleId",
            because: "an unconditional VALUES insert can pair a user with another tenant's role");
        body.Should().Contain("throw new ConflictException");
    }

    [Fact]
    public void Replacing_role_assignments_only_clears_a_user_of_the_tenant()
    {
        var sql = Normalize(MethodBody("UserRepository.cs", "ReplaceRoleAssignmentsAsync", lastOverload: true));
        sql.Should().Contain("DELETE FROM UserRoles WHERE UserId = @UserId AND EXISTS (SELECT 1 FROM Users u WHERE u.Id = @UserId AND u.TenantId = @TenantId)");
    }

    [Fact]
    public void Role_user_count_is_anchored_to_the_tenant_role()
    {
        var sql = Normalize(MethodBody("PlatformRepository.Access.cs", "CountUsersWithRoleAsync"));
        sql.Should().Contain("INNER JOIN Roles r ON r.Id = ur.RoleId AND r.TenantId = @TenantId");
    }

    // ---- Handlers: the tenant that was authorised is the tenant that is written -------------

    private const int TargetUser = 77;
    private const int OtherTenant = 9;

    [Fact]
    public async Task Delete_user_scopes_the_write_to_the_target_user_tenant()
    {
        var (repo, scope) = Arrange();
        await new DeleteUserCommandHandler(repo.Object, scope.Object)
            .Handle(new DeleteUserCommand(TargetUser), CancellationToken.None);

        scope.Verify(s => s.EnsureTenantAccess(OtherTenant), Times.Once);
        repo.Verify(r => r.SoftDeleteAsync(TargetUser, OtherTenant, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_user_status_scopes_the_write_to_the_target_user_tenant()
    {
        var (repo, scope) = Arrange();
        await new UpdateUserStatusCommandHandler(repo.Object, scope.Object)
            .Handle(new UpdateUserStatusCommand(TargetUser, false), CancellationToken.None);

        repo.Verify(r => r.UpdateStatusAsync(TargetUser, OtherTenant, false, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Admin_password_reset_scopes_the_write_to_the_target_user_tenant()
    {
        var (repo, scope) = Arrange();
        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hash");

        await new ResetUserPasswordCommandHandler(repo.Object, hasher.Object, scope.Object)
            .Handle(new ResetUserPasswordCommand(TargetUser), CancellationToken.None);

        repo.Verify(r => r.AdminResetPasswordAsync(TargetUser, OtherTenant, "hash", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_caller_without_access_to_the_user_tenant_writes_nothing()
    {
        var (repo, scope) = Arrange();
        scope.Setup(s => s.EnsureTenantAccess(OtherTenant)).Throws(new ForbiddenException());

        var act = () => new DeleteUserCommandHandler(repo.Object, scope.Object)
            .Handle(new DeleteUserCommand(TargetUser), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        repo.Verify(r => r.SoftDeleteAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static (Mock<IUserRepository> Repo, Mock<IPlatformScope> Scope) Arrange()
    {
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetTenantIdAsync(TargetUser, It.IsAny<CancellationToken>())).ReturnsAsync(OtherTenant);
        return (repo, new Mock<IPlatformScope>());
    }

    // ---- helpers ---------------------------------------------------------------------------

    private static string MethodBody(string fileName, string methodName, bool lastOverload = false)
    {
        var path = Directory.EnumerateFiles(Backend, fileName, SearchOption.AllDirectories)
            .First(p => !p.Contains("/obj/") && !p.Contains("/bin/"));
        var text = File.ReadAllText(path);

        var matches = Regex.Matches(text, $@"\bTask\b[^;=]*?\s{Regex.Escape(methodName)}\s*\(");
        matches.Count.Should().BeGreaterThan(0, because: $"{fileName} must still define {methodName}");
        var declaration = lastOverload ? matches[^1] : matches[0];

        var open = text.IndexOf('{', declaration.Index);
        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}' && --depth == 0)
                return text[open..(i + 1)];
        }
        throw new InvalidOperationException($"Unbalanced braces in {fileName}::{methodName}");
    }

    private static string Normalize(string s) => Regex.Replace(s, @"\s+", " ");

    private static string FindBackendRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var backend = Path.Combine(dir.FullName, "Backend");
            if (Directory.Exists(Path.Combine(backend, "SheikhTravelSystem.Infrastructure")))
                return backend;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Could not locate Backend/ root.");
    }
}
