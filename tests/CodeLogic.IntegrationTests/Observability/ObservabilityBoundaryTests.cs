using System.Diagnostics;

using CodeLogic.Infrastructure.Persistence;
using CodeLogic.Web.Observability;

using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeLogic.IntegrationTests.Observability;

public sealed class ObservabilityBoundaryTests
{
    [Fact]
    public async Task CorrelationMiddlewareReturnsServerControlledIdentifier()
    {
        using var activity = new Activity("request").Start();
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.ResponseHeaderName] =
            "untrusted-student-value";
        var middleware = new CorrelationIdMiddleware(
            async httpContext => await httpContext.Response.StartAsync(),
            NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        var correlationId = context.Response.Headers[CorrelationIdMiddleware.ResponseHeaderName].ToString();
        Assert.Equal(32, correlationId.Length);
        Assert.True(Guid.TryParseExact(correlationId, "N", out _));
        Assert.Equal(correlationId, context.TraceIdentifier);
        Assert.NotEqual("untrusted-student-value", correlationId);
        Assert.NotEqual(activity.TraceId.ToString(), correlationId);
        Assert.Equal(correlationId, activity.GetTagItem("code_logic.correlation_id"));
    }

    [Fact]
    public async Task ReadinessCheckReportsHealthyForReachableDatabase()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<CodeLogicDbContext>()
            .UseSqlite(connection)
            .Options;
        var services = new ServiceCollection();
        services.AddScoped(_ => new CodeLogicDbContext(options));
        await using var provider = services.BuildServiceProvider();
        var check = new DatabaseReadinessHealthCheck(
            provider.GetRequiredService<IServiceScopeFactory>());

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task ReadinessCheckDoesNotExposeExceptionDetails()
    {
        var services = new ServiceCollection();
        await using var provider = services.BuildServiceProvider();
        var check = new DatabaseReadinessHealthCheck(
            provider.GetRequiredService<IServiceScopeFactory>());

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("The curriculum database is unavailable.", result.Description);
        Assert.Null(result.Exception);
    }
}
