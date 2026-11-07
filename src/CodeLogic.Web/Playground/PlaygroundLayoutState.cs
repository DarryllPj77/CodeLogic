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

/// <summary>What the drawer at the right edge of the canvas shows.</summary>
public enum DrawerTab
{
    Properties,
    Legend,
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

    public DrawerTab DrawerTab { get; private set; } = DrawerTab.Properties;

    public bool IsHelpOpen { get; private set; }

    public bool IsSettingsOpen { get; private set; }

    public bool IsWhatsSupportedOpen { get; private set; }

    /// <summary>The registry entry the "What's supported" view should scroll to, if any.</summary>
    public string? WhatsSupportedTopicId { get; private set; }

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

    public void OpenProperties()
    {
        IsPropertiesOpen = true;
        DrawerTab = DrawerTab.Properties;
    }

    /// <summary>The Legend toolbar button: opens the drawer on the Legend tab, or closes it if already there.</summary>
    public void ToggleLegend()
    {
        if (IsPropertiesOpen && DrawerTab == DrawerTab.Legend)
        {
            IsPropertiesOpen = false;
            return;
        }

        IsPropertiesOpen = true;
        DrawerTab = DrawerTab.Legend;
    }

    public void SelectDrawerTab(DrawerTab tab) => DrawerTab = tab;

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
        IsWhatsSupportedOpen = false;
    }

    public void ToggleSettings()
    {
        IsSettingsOpen = !IsSettingsOpen;
        IsHelpOpen = false;
        IsWhatsSupportedOpen = false;
    }

    /// <summary>Opens "What's supported", optionally at one feature (for example from a diagnostic).</summary>
    public void OpenWhatsSupported(string? topicId = null)
    {
        IsWhatsSupportedOpen = true;
        WhatsSupportedTopicId = topicId;
        IsHelpOpen = false;
        IsSettingsOpen = false;
    }

    public void CloseWhatsSupported()
    {
        IsWhatsSupportedOpen = false;
        WhatsSupportedTopicId = null;
    }

    /// <summary>
    /// Esc closes the most temporary layer first: an open dialog, then a maximized panel, then the
    /// properties drawer. Returns false when there was nothing to close.
    /// </summary>
    public bool HandleEscape()
    {
        if (IsHelpOpen || IsSettingsOpen || IsWhatsSupportedOpen)
        {
            IsHelpOpen = false;
            IsSettingsOpen = false;
            CloseWhatsSupported();
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
