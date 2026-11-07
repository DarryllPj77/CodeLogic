using CodeLogic.Application.Analysis;
using CodeLogic.Application.Execution;
using CodeLogic.Infrastructure.Analysis;
using CodeLogic.Infrastructure.Execution;
using CodeLogic.Infrastructure.Flowcharts;
using CodeLogic.Web.Playground;

namespace CodeLogic.IntegrationTests.Analysis;

/// <summary>
/// Guards the "What's supported" view against drifting from real behavior: every supported example
/// must analyze, translate, and trace, and every unsupported example must produce exactly the
/// diagnostic the registry describes.
/// </summary>
public sealed class SupportedFeatureRegistryTests
{
    private static readonly RoslynCodeAnalysisService Analysis = new();
    private static readonly RoslynPedagogicalInterpreter Interpreter = new(new RoslynFlowchartTranslator(Analysis));

    public static TheoryData<string> SupportedIds() =>
        [.. SupportedFeatureRegistry.All.Where(feature => feature.IsSupported).Select(feature => feature.Id)];

    public static TheoryData<string> UnsupportedIds() =>
        [.. SupportedFeatureRegistry.All.Where(feature => !feature.IsSupported).Select(feature => feature.Id)];

    [Theory]
    [MemberData(nameof(SupportedIds))]
    public async Task SupportedExamplesAnalyzeTranslateAndTrace(string id)
    {
        var feature = SupportedFeatureRegistry.Get(id);

        var result = await Analysis.AnalyzeAsync(feature.Example);
        var trace = await Interpreter.TraceAsync(feature.Example);

        Assert.True(result.IsValid, string.Join(" | ", result.Diagnostics.Select(item => $"{item.Code} {item.Message}")));
        Assert.True(trace.Status == ExecutionTraceStatus.Completed, string.Join(" | ", trace.Diagnostics.Select(item => item.Message)));
    }

    [Theory]
    [MemberData(nameof(UnsupportedIds))]
    public async Task UnsupportedExamplesProduceTheRegistryDiagnostic(string id)
    {
        var feature = SupportedFeatureRegistry.Get(id);

        var result = await Analysis.AnalyzeAsync(feature.Example);

        Assert.Equal(CodeAnalysisStatus.Unsupported, result.Status);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == feature.DiagnosticCode
            && diagnostic.Message == feature.Summary
            && diagnostic.Explanation == feature.Why
            && diagnostic.Suggestion == feature.Instead
            && diagnostic.HelpTopicId == feature.Id);
    }

    [Fact]
    public void UnsupportedEntriesExplainWhatWhyAndInstead()
    {
        foreach (var feature in SupportedFeatureRegistry.All.Where(feature => !feature.IsSupported))
        {
            Assert.False(string.IsNullOrWhiteSpace(feature.DiagnosticCode), feature.Id);
            Assert.False(string.IsNullOrWhiteSpace(feature.Why), feature.Id);
            Assert.False(string.IsNullOrWhiteSpace(feature.Instead), feature.Id);
        }
    }

    [Fact]
    public void RegistryCoversEveryItemTheOwnerListed()
    {
        string[] expected =
        [
            SupportedFeatureIds.UsingDirectives, SupportedFeatureIds.Namespace, SupportedFeatureIds.Class,
            SupportedFeatureIds.Main, SupportedFeatureIds.TopLevel,
            SupportedFeatureIds.Declarations, SupportedFeatureIds.Assignments, SupportedFeatureIds.ConsoleOutput,
            SupportedFeatureIds.IfElse, SupportedFeatureIds.While, SupportedFeatureIds.For, SupportedFeatureIds.DoWhile,
            SupportedFeatureIds.Nesting, SupportedFeatureIds.StaticMethods,
            SupportedFeatureIds.Switch, SupportedFeatureIds.Break, SupportedFeatureIds.Continue,
            SupportedFeatureIds.ReturnInLoop, SupportedFeatureIds.LocalFunctions, SupportedFeatureIds.Records,
            SupportedFeatureIds.Properties, SupportedFeatureIds.Generics, SupportedFeatureIds.Async,
            SupportedFeatureIds.Linq, SupportedFeatureIds.Lambdas, SupportedFeatureIds.DotNetExecution,
        ];

        Assert.Equal(expected.Order(), SupportedFeatureRegistry.All.Select(feature => feature.Id).Order());
        Assert.Equal(
            SupportedFeatureRegistry.All.Count,
            SupportedFeatureRegistry.All.Select(feature => feature.Id).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void WhatsSupportedViewListsExactlyTheRegistryInSectionOrder()
    {
        var shown = WhatsSupportedView.Sections.SelectMany(section => section.Features).ToArray();

        Assert.Equal(SupportedFeatureRegistry.All, shown);
        Assert.Equal(
            ["Program structure", "Statements", "Not supported yet"],
            WhatsSupportedView.Sections.Select(section => section.Title));
    }

    [Fact]
    public void EveryRegistryHelpTopicIdUsedByDiagnosticsExists()
    {
        foreach (var id in typeof(SupportedFeatureIds).GetFields().Select(field => (string)field.GetValue(null)!))
        {
            Assert.True(SupportedFeatureRegistry.TryGet(id, out _), id);
        }
    }
}
