using System.Collections.ObjectModel;

namespace CodeLogic.Application.Analysis;

public sealed record EditorFoldRegion(
    int StartLine,
    int EndLine,
    string Kind)
{
    public int HiddenLineCount => EndLine - StartLine;
}

public interface IEditorStructureService
{
    Task<IReadOnlyList<EditorFoldRegion>> GetFoldRegionsAsync(
        string? source,
        CancellationToken cancellationToken = default);
}

public static class EditorFoldRegions
{
    public static IReadOnlyList<EditorFoldRegion> ReadOnly(IEnumerable<EditorFoldRegion> regions) =>
        new ReadOnlyCollection<EditorFoldRegion>(regions.ToArray());
}
