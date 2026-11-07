namespace CodeLogic.Web.Playground;

public enum WorkspacePanel
{
    Editor,
    Analysis,
    Flowchart,
}

public enum WorkspaceTab
{
    Diagnostics,
    Output,
    Trace,
    Logic,
    Terminal,
}

public enum WorkspaceMode
{
    Analyze,
    FlowchartFocus,
}

public enum CompactView
{
    Editor,
    Flowchart,
}

/// <summary>
/// Which workspace panels are open, collapsed, or maximized. Keeping these rules in one plain
/// class lets the Razor page stay declarative and lets tests cover collapse, FULL, and Esc behavior.
/// </summary>
public sealed class PlaygroundLayoutState
{
    public WorkspaceMode Mode { get; private set; } = WorkspaceMode.Analyze;

    public bool IsEditorCollapsed { get; private set; }

    public bool IsAnalysisOpen { get; private set; }

    public bool IsExplorerOpen { get; private set; }

    public bool IsPropertiesOpen { get; private set; }

    public bool IsHelpOpen { get; private set; }

    public bool IsSettingsOpen { get; private set; }

    public WorkspacePanel? MaximizedPanel { get; private set; }

    public WorkspaceTab ActiveTab { get; private set; } = WorkspaceTab.Diagnostics;

    /// <summary>The panel shown below the responsive breakpoint, where only one column fits.</summary>
    public CompactView CompactView { get; private set; } = CompactView.Editor;

    public bool IsMaximized(WorkspacePanel panel) => MaximizedPanel == panel;

    public void SetMode(WorkspaceMode mode)
    {
        Mode = mode;
        if (mode == WorkspaceMode.FlowchartFocus)
        {
            CompactView = CompactView.Flowchart;
            if (MaximizedPanel is WorkspacePanel.Editor or WorkspacePanel.Analysis)
            {
                MaximizedPanel = null;
            }
        }
    }

    public void ToggleEditor()
    {
        IsEditorCollapsed = !IsEditorCollapsed;
        if (IsEditorCollapsed && MaximizedPanel == WorkspacePanel.Editor)
        {
            MaximizedPanel = null;
        }
    }

    public void ToggleAnalysis()
    {
        IsAnalysisOpen = !IsAnalysisOpen;
        if (!IsAnalysisOpen && MaximizedPanel == WorkspacePanel.Analysis)
        {
            MaximizedPanel = null;
        }
    }

    /// <summary>Opens the Analysis panel on a tab; used by tab clicks and by actions that produce results there.</summary>
    public void ShowTab(WorkspaceTab tab)
    {
        ActiveTab = tab;
        IsAnalysisOpen = true;
    }

    public void SelectTab(WorkspaceTab tab) => ActiveTab = tab;

    public void ToggleExplorer() => IsExplorerOpen = !IsExplorerOpen;

    public void SetExplorerOpen(bool isOpen) => IsExplorerOpen = isOpen;

    public void OpenProperties() => IsPropertiesOpen = true;

    public void CloseProperties() => IsPropertiesOpen = false;

    /// <summary>Flowchart focus hides the editor column, so asking to see the editor leaves focus mode.</summary>
    public void SetCompactView(CompactView view)
    {
        CompactView = view;
        if (view == CompactView.Editor && Mode == WorkspaceMode.FlowchartFocus)
        {
            Mode = WorkspaceMode.Analyze;
        }
    }

    /// <summary>FULL toggles a panel over the workspace; maximizing also expands it and shows its compact view.</summary>
    public void ToggleMaximized(WorkspacePanel panel)
    {
        if (MaximizedPanel == panel)
        {
            MaximizedPanel = null;
            return;
        }

        MaximizedPanel = panel;
        switch (panel)
        {
            case WorkspacePanel.Editor:
                IsEditorCollapsed = false;
                CompactView = CompactView.Editor;
                break;
            case WorkspacePanel.Analysis:
                IsAnalysisOpen = true;
                CompactView = CompactView.Editor;
                break;
            case WorkspacePanel.Flowchart:
                CompactView = CompactView.Flowchart;
                break;
        }
    }

    public void ToggleHelp()
    {
        IsHelpOpen = !IsHelpOpen;
        IsSettingsOpen = false;
    }

    public void ToggleSettings()
    {
        IsSettingsOpen = !IsSettingsOpen;
        IsHelpOpen = false;
    }

    /// <summary>
    /// Esc closes the most temporary layer first: an open dialog, then a maximized panel, then the
    /// properties drawer. Returns false when there was nothing to close.
    /// </summary>
    public bool HandleEscape()
    {
        if (IsHelpOpen || IsSettingsOpen)
        {
            IsHelpOpen = false;
            IsSettingsOpen = false;
            return true;
        }

        if (MaximizedPanel is not null)
        {
            MaximizedPanel = null;
            return true;
        }

        if (IsPropertiesOpen)
        {
            IsPropertiesOpen = false;
            return true;
        }

        return false;
    }
}
