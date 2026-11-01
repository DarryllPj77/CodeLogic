using CodeLogic.Application.Analysis;
using CodeLogic.Application.Flowcharts;
using CodeLogic.Infrastructure.Flowcharts;

using Microsoft.Extensions.DependencyInjection;

namespace CodeLogic.Infrastructure.Analysis;

public static class AnalysisServiceCollectionExtensions
{
    public static IServiceCollection AddCodeLogicAnalysis(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<ICodeAnalysisService, RoslynCodeAnalysisService>();
        services.AddSingleton<IFlowchartTranslator, RoslynFlowchartTranslator>();
        services.AddSingleton<IMermaidFlowchartSerializer, MermaidFlowchartSerializer>();
        return services;
    }
}
