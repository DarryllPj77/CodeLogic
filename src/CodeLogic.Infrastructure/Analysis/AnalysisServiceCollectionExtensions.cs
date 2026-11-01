using CodeLogic.Application.Analysis;

using Microsoft.Extensions.DependencyInjection;

namespace CodeLogic.Infrastructure.Analysis;

public static class AnalysisServiceCollectionExtensions
{
    public static IServiceCollection AddCodeLogicAnalysis(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<ICodeAnalysisService, RoslynCodeAnalysisService>();
        return services;
    }
}
