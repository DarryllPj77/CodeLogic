using System.Reflection;
using System.Security.Claims;

using CodeLogic.Infrastructure.Identity;
using CodeLogic.Web.Components.Pages;

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace CodeLogic.IntegrationTests.Authoring;

public sealed class LessonAuthoringAuthorizationTests
{
    [Fact]
    public void AuthoringPageRequiresAdministratorRole()
    {
        var authorize = typeof(LessonAuthoring)
            .GetCustomAttributes<AuthorizeAttribute>()
            .Single();

        Assert.Equal(CodeLogicRoles.Administrator, authorize.Roles);
    }

    [Fact]
    public async Task AdministratorPolicyAllowsAdminAndDeniesRegularAuthenticatedUser()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization();
        await using var provider = services.BuildServiceProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();
        var policy = new AuthorizationPolicyBuilder()
            .RequireRole(CodeLogicRoles.Administrator)
            .Build();
        var administrator = Principal("admin-user", CodeLogicRoles.Administrator);
        var learner = Principal("learner-user");

        var adminResult = await authorization.AuthorizeAsync(administrator, resource: null, policy);
        var learnerResult = await authorization.AuthorizeAsync(learner, resource: null, policy);

        Assert.True(adminResult.Succeeded);
        Assert.False(learnerResult.Succeeded);
    }

    [Fact]
    public void PublishedLessonPageDoesNotRequireAuthentication()
    {
        var authorizeAttributes = typeof(AuthoredLessonDetail)
            .GetCustomAttributes<AuthorizeAttribute>();

        Assert.Empty(authorizeAttributes);
    }

    private static ClaimsPrincipal Principal(string userId, params string[] roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, userId),
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }
}
