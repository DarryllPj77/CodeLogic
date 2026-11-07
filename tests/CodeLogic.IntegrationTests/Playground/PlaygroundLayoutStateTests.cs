using CodeLogic.Web.Playground;

namespace CodeLogic.IntegrationTests.Playground;

public sealed class PlaygroundLayoutStateTests
{
    [Fact]
    public void DefaultsShowEditorAndGraphWithAnalysisCollapsedAndNoDrawers()
    {
        var layout = new PlaygroundLayoutState();

        Assert.Equal(WorkspaceMode.Analyze, layout.Mode);
        Assert.False(layout.IsEditorCollapsed);
        Assert.False(layout.IsAnalysisOpen);
        Assert.False(layout.IsPropertiesOpen);
        Assert.False(layout.IsExplorerOpen);
        Assert.Null(layout.MaximizedPanel);
        Assert.Equal(CompactView.Editor, layout.CompactView);
    }

    [Fact]
    public void CollapseTogglesFlipEachPanelIndependently()
    {
        var layout = new PlaygroundLayoutState();

        layout.ToggleEditor();
        layout.ToggleAnalysis();
        Assert.True(layout.IsEditorCollapsed);
        Assert.True(layout.IsAnalysisOpen);

        layout.ToggleEditor();
        layout.ToggleAnalysis();
        Assert.False(layout.IsEditorCollapsed);
        Assert.False(layout.IsAnalysisOpen);
    }

    [Fact]
    public void ShowTabOpensTheAnalysisPanelOnThatTab()
    {
        var layout = new PlaygroundLayoutState();

        layout.ShowTab(WorkspaceTab.Logic);

        Assert.True(layout.IsAnalysisOpen);
        Assert.Equal(WorkspaceTab.Logic, layout.ActiveTab);
    }

    [Fact]
    public void SelectTabChangesTheTabWithoutOpeningACollapsedPanel()
    {
        var layout = new PlaygroundLayoutState();

        layout.SelectTab(WorkspaceTab.Trace);

        Assert.False(layout.IsAnalysisOpen);
        Assert.Equal(WorkspaceTab.Trace, layout.ActiveTab);
    }

    [Theory]
    [InlineData(WorkspacePanel.Editor)]
    [InlineData(WorkspacePanel.Analysis)]
    [InlineData(WorkspacePanel.Flowchart)]
    public void FullTogglesOnAndOffForEachPanel(WorkspacePanel panel)
    {
        var layout = new PlaygroundLayoutState();

        layout.ToggleMaximized(panel);
        Assert.True(layout.IsMaximized(panel));

        layout.ToggleMaximized(panel);
        Assert.Null(layout.MaximizedPanel);
    }

    [Fact]
    public void FullExpandsACollapsedPanelSoItsContentIsVisible()
    {
        var layout = new PlaygroundLayoutState();
        layout.ToggleEditor();

        layout.ToggleMaximized(WorkspacePanel.Editor);
        Assert.False(layout.IsEditorCollapsed);

        layout.ToggleMaximized(WorkspacePanel.Analysis);
        Assert.True(layout.IsAnalysisOpen);
        Assert.Equal(WorkspacePanel.Analysis, layout.MaximizedPanel);
    }

    [Fact]
    public void FullOnTheFlowchartSwitchesTheCompactViewToTheFlowchart()
    {
        var layout = new PlaygroundLayoutState();

        layout.ToggleMaximized(WorkspacePanel.Flowchart);

        Assert.Equal(CompactView.Flowchart, layout.CompactView);
    }

    [Fact]
    public void CollapsingAMaximizedPanelAlsoRestoresIt()
    {
        var layout = new PlaygroundLayoutState();
        layout.ToggleMaximized(WorkspacePanel.Analysis);

        layout.ToggleAnalysis();

        Assert.False(layout.IsAnalysisOpen);
        Assert.Null(layout.MaximizedPanel);
    }

    [Fact]
    public void EscapeClosesTheMostTemporaryLayerFirst()
    {
        var layout = new PlaygroundLayoutState();
        layout.OpenProperties();
        layout.ToggleMaximized(WorkspacePanel.Flowchart);
        layout.ToggleHelp();

        Assert.True(layout.HandleEscape());
        Assert.False(layout.IsHelpOpen);
        Assert.NotNull(layout.MaximizedPanel);

        Assert.True(layout.HandleEscape());
        Assert.Null(layout.MaximizedPanel);
        Assert.True(layout.IsPropertiesOpen);

        Assert.True(layout.HandleEscape());
        Assert.False(layout.IsPropertiesOpen);

        Assert.False(layout.HandleEscape());
    }

    [Fact]
    public void HelpAndSettingsAreMutuallyExclusive()
    {
        var layout = new PlaygroundLayoutState();

        layout.ToggleSettings();
        layout.ToggleHelp();

        Assert.True(layout.IsHelpOpen);
        Assert.False(layout.IsSettingsOpen);
    }

    [Fact]
    public void FlowchartFocusRestoresAMaximizedEditorAndShowsTheFlowchart()
    {
        var layout = new PlaygroundLayoutState();
        layout.ToggleMaximized(WorkspacePanel.Editor);

        layout.SetMode(WorkspaceMode.FlowchartFocus);

        Assert.Null(layout.MaximizedPanel);
        Assert.Equal(CompactView.Flowchart, layout.CompactView);
    }

    [Fact]
    public void ChoosingTheCompactEditorViewLeavesFlowchartFocus()
    {
        var layout = new PlaygroundLayoutState();
        layout.SetMode(WorkspaceMode.FlowchartFocus);

        layout.SetCompactView(CompactView.Editor);

        Assert.Equal(WorkspaceMode.Analyze, layout.Mode);
        Assert.Equal(CompactView.Editor, layout.CompactView);
    }
}
