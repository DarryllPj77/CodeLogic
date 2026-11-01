using System.Diagnostics;

namespace CodeLogic.Web.Observability;

public sealed class CorrelationIdMiddleware(
    RequestDelegate next,
    ILogger<CorrelationIdMiddleware> logger)
{
    private static readonly Func<ILogger, string, IDisposable?> BeginCorrelationScope =
        LoggerMessage.DefineScope<string>("CorrelationId:{CorrelationId}");

    public const string ResponseHeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = Guid.NewGuid().ToString("N");

        context.TraceIdentifier = correlationId;
        context.Response.Headers[ResponseHeaderName] = correlationId;
        Activity.Current?.SetTag("code_logic.correlation_id", correlationId);

        using (BeginCorrelationScope(logger, correlationId))
        {
            await next(context);
        }
    }
}

public static class CorrelationIdApplicationBuilderExtensions
{
    public static IApplicationBuilder UseCodeLogicCorrelationId(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<CorrelationIdMiddleware>();
    }
}
