using CodeLogic.Application.Analysis;

namespace CodeLogic.Web.Playground;

/// <summary>
/// What the "What's supported" dialog shows: the analyzer's own feature registry grouped by section,
/// with nothing added or removed, so the dialog cannot disagree with the analyzer.
/// </summary>
public static class WhatsSupportedView
{
    public static IReadOnlyList<WhatsSupportedSection> Sections { get; } =
    [
        .. Enum.GetValues<SupportedFeatureSection>()
            .Select(section => new WhatsSupportedSection(
                section,
                SupportedFeatureRegistry.SectionTitle(section),
                [.. SupportedFeatureRegistry.InSection(section)])),
    ];
}

public sealed record WhatsSupportedSection(
    SupportedFeatureSection Section,
    string Title,
    IReadOnlyList<SupportedFeature> Features);
