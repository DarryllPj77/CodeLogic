using CodeLogic.Application.Analysis;
using CodeLogic.Web.Playground;

namespace CodeLogic.IntegrationTests.Playground;

public sealed class PlaygroundEditorStateTests
{
    [Fact]
    public void StartsWithFirstCuratedSnippet()
    {
        var state = CreateState();

        Assert.Equal("variables", state.ActiveSnippet.Id);
        Assert.Equal("int score = 5;", state.CurrentCode);
        Assert.Equal(4, state.RemainingCharacters);
        Assert.False(state.HasUnsavedChanges);
    }

    [Fact]
    public void ExactSizeLimitIsAcceptedAndOversizedUpdateIsRejected()
    {
        var state = CreateState();

        Assert.True(state.TryUpdateCode(new string('a', state.MaximumCharacters)));
        Assert.False(state.TryUpdateCode(new string('b', state.MaximumCharacters + 1)));
        Assert.Equal(new string('a', state.MaximumCharacters), state.CurrentCode);
        Assert.Equal(0, state.RemainingCharacters);
    }

    [Fact]
    public void SharedSourceLimitAcceptsTenThousandAndRejectsTenThousandAndOne()
    {
        var state = new PlaygroundEditorState(
            [new PlaygroundSnippet("variables", "Variables", "Variable example", "int score = 5;")],
            CodeAnalysisDefaults.MaximumCharacters);

        Assert.True(state.TryUpdateCode(new string('a', 10_000)));
        Assert.False(state.TryUpdateCode(new string('b', 10_001)));
        Assert.Equal(10_000, state.CurrentCode.Length);
        Assert.Equal(0, state.RemainingCharacters);
    }

    [Fact]
    public void ResetRestoresCurrentlyLoadedSnippet()
    {
        var state = CreateState();
        state.LoadSnippet("operators");
        state.TryUpdateCode("bool changed = false;");

        state.Reset();

        Assert.Equal("bool ready = true;", state.CurrentCode);
        Assert.Equal("operators", state.ActiveSnippet.Id);
        Assert.False(state.HasUnsavedChanges);
    }

    [Fact]
    public void UnknownSnippetIsRejectedWithoutLosingCurrentText()
    {
        var state = CreateState();
        state.TryUpdateCode("int value = 1;");

        Assert.Throws<ArgumentException>(() => state.LoadSnippet("missing"));
        Assert.Equal("int value = 1;", state.CurrentCode);
    }

    [Fact]
    public void DuplicateSnippetIdentifiersAreRejected()
    {
        var snippets = new[]
        {
            new PlaygroundSnippet("same", "First", "First example", "int first = 1;"),
            new PlaygroundSnippet("same", "Second", "Second example", "int second = 2;"),
        };

        Assert.Throws<ArgumentException>(() => new PlaygroundEditorState(snippets, 100));
    }

    private static PlaygroundEditorState CreateState() => new(
        [
            new PlaygroundSnippet("variables", "Variables", "Variable example", "int score = 5;"),
            new PlaygroundSnippet("operators", "Operators", "Operator example", "bool ready = true;"),
        ],
        maximumCharacters: 18);
}
