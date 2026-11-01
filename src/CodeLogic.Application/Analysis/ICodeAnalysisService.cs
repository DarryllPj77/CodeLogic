namespace CodeLogic.Application.Analysis;

public interface ICodeAnalysisService
{
    CodeAnalysisLimits Limits { get; }

    Task<CodeAnalysisResult> AnalyzeAsync(
        string? source,
        CancellationToken cancellationToken = default);
}
