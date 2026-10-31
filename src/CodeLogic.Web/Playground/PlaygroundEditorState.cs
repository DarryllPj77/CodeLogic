using System.Collections.ObjectModel;

namespace CodeLogic.Web.Playground;

public sealed record PlaygroundSnippet(
    string Id,
    string Title,
    string Description,
    string Code);

public sealed class PlaygroundEditorState
{
    private readonly ReadOnlyCollection<PlaygroundSnippet> snippets;

    public PlaygroundEditorState(
        IEnumerable<PlaygroundSnippet> snippets,
        int maximumCharacters)
    {
        ArgumentNullException.ThrowIfNull(snippets);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumCharacters);

        var materializedSnippets = snippets.ToArray();
        if (materializedSnippets.Length == 0)
        {
            throw new ArgumentException("At least one playground snippet is required.", nameof(snippets));
        }

        if (materializedSnippets.Any(snippet =>
            string.IsNullOrWhiteSpace(snippet.Id)
            || string.IsNullOrWhiteSpace(snippet.Title)
            || string.IsNullOrWhiteSpace(snippet.Description)
            || string.IsNullOrWhiteSpace(snippet.Code)))
        {
            throw new ArgumentException("Playground snippets require an ID, title, description, and code.", nameof(snippets));
        }

        if (materializedSnippets.Select(snippet => snippet.Id).Distinct(StringComparer.Ordinal).Count()
            != materializedSnippets.Length)
        {
            throw new ArgumentException("Playground snippet IDs must be unique.", nameof(snippets));
        }

        if (materializedSnippets.Any(snippet => snippet.Code.Length > maximumCharacters))
        {
            throw new ArgumentException("A playground snippet exceeds the editor size limit.", nameof(snippets));
        }

        this.snippets = Array.AsReadOnly(materializedSnippets);
        MaximumCharacters = maximumCharacters;
        ActiveSnippet = materializedSnippets[0];
        CurrentCode = ActiveSnippet.Code;
    }

    public IReadOnlyList<PlaygroundSnippet> Snippets => snippets;

    public PlaygroundSnippet ActiveSnippet { get; private set; }

    public string CurrentCode { get; private set; }

    public int MaximumCharacters { get; }

    public int RemainingCharacters => MaximumCharacters - CurrentCode.Length;

    public bool HasUnsavedChanges { get; private set; }

    public bool TryUpdateCode(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        if (code.Length > MaximumCharacters)
        {
            return false;
        }

        CurrentCode = code;
        HasUnsavedChanges = true;
        return true;
    }

    public void LoadSnippet(string snippetId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(snippetId);
        ActiveSnippet = snippets.SingleOrDefault(snippet => snippet.Id == snippetId)
            ?? throw new ArgumentException("The requested playground snippet does not exist.", nameof(snippetId));
        CurrentCode = ActiveSnippet.Code;
        HasUnsavedChanges = true;
    }

    public void MarkSaved() => HasUnsavedChanges = false;

    public void Reset()
    {
        CurrentCode = ActiveSnippet.Code;
        HasUnsavedChanges = false;
    }
}
