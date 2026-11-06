using CodeLogic.Application.Flowcharts;
using CodeLogic.Web.Playground;

namespace CodeLogic.IntegrationTests.Playground;

public sealed class SourceMappedSourceEditorTests
{
    [Fact]
    public void ReplacesOnlyMappedSourceRange()
    {
        const string source = "int score = 5;\nConsole.WriteLine(score);";
        var span = Span(start: 0, length: 14);

        var changed = SourceMappedSourceEditor.TryReplace(
            source,
            span,
            "int score = 6;",
            maximumCharacters: 100,
            out var candidate,
            out var error);

        Assert.True(changed);
        Assert.Empty(error);
        Assert.Equal("int score = 6;\nConsole.WriteLine(score);", candidate);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RejectsBlankReplacement(string? replacement)
    {
        var changed = SourceMappedSourceEditor.TryReplace(
            "int score = 5;",
            Span(0, 14),
            replacement,
            maximumCharacters: 100,
            out _,
            out var error);

        Assert.False(changed);
        Assert.Contains("cannot be empty", error, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsStaleSpanWithoutChangingSource()
    {
        const string source = "int score = 5;";

        var changed = SourceMappedSourceEditor.TryReplace(
            source,
            Span(12, 20),
            "int score = 6;",
            maximumCharacters: 100,
            out var candidate,
            out var error);

        Assert.False(changed);
        Assert.Equal(source, candidate);
        Assert.Contains("stale", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsCandidateOverEditorLimit()
    {
        var changed = SourceMappedSourceEditor.TryReplace(
            "x",
            Span(0, 1),
            "replacement",
            maximumCharacters: 5,
            out _,
            out var error);

        Assert.False(changed);
        Assert.Contains("limit", error, StringComparison.OrdinalIgnoreCase);
    }

    private static SourceSpan Span(int start, int length) => new(
        start,
        length,
        startLine: 1,
        startColumn: start + 1,
        endLine: 1,
        endColumn: start + length + 1);
}
