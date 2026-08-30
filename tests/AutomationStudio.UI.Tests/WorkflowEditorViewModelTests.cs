using AutomationStudio.Abstractions;
using AutomationStudio.Core.Execution;
using AutomationStudio.Storage;
using AutomationStudio.UI.ViewModels;
using AutomationStudio.UI.ViewModels.ActionEditors;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutomationStudio.UI.Tests;

public class WorkflowEditorViewModelTests
{
    private static (WorkflowEditorViewModel Editor, FakeDesktopController Desktop, FakeWorkflowRepository Workflows, FakeExecutionRepository Executions, FakeElementFinder ElementFinder) Build(
        WorkflowRecord? existing = null)
    {
        var desktop = new FakeDesktopController();
        var elementFinder = new FakeElementFinder();
        var engine = new WorkflowExecutionEngine(
            desktop, new FakeScreenshotService(), new FakeWindowManager(), elementFinder, new FakeOcrEngine(), new FakeTemplateMatcher(), NullLogger<WorkflowExecutionEngine>.Instance);
        var workflows = new FakeWorkflowRepository();
        var executions = new FakeExecutionRepository();

        var editor = new WorkflowEditorViewModel(engine, workflows, executions, onSaved: () => { }, existing: existing);
        return (editor, desktop, workflows, executions, elementFinder);
    }

    [Fact]
    public void NewEditor_HasAFreshIdAndDefaultName()
    {
        var (editor, _, _, _, _) = Build();

        Assert.False(string.IsNullOrWhiteSpace(editor.Id));
        Assert.Equal("New Workflow", editor.Name);
        Assert.Empty(editor.Actions);
    }

    [Fact]
    public void AddActionCommand_AddsAndSelectsANewEditor()
    {
        var (editor, _, _, _, _) = Build();
        var entry = ActionPaletteEntry.All.First(e => e.Label == "Wait");

        editor.AddActionCommand.Execute(entry);

        var added = Assert.Single(editor.Actions);
        Assert.Same(added, editor.SelectedAction);
        Assert.IsType<WaitEditorViewModel>(added);
    }

    [Fact]
    public void RemoveSelectedActionCommand_RemovesOnlyTheSelectedAction()
    {
        var (editor, _, _, _, _) = Build();
        var waitEntry = ActionPaletteEntry.All.First(e => e.Label == "Wait");
        editor.AddActionCommand.Execute(waitEntry);
        editor.AddActionCommand.Execute(waitEntry);
        var toRemove = editor.Actions[0];
        editor.SelectedAction = toRemove;

        editor.RemoveSelectedActionCommand.Execute(null);

        Assert.Single(editor.Actions);
        Assert.DoesNotContain(toRemove, editor.Actions);
        Assert.Null(editor.SelectedAction);
    }

    [Fact]
    public void MoveSelectedActionUpAndDown_ReorderTheList()
    {
        var (editor, _, _, _, _) = Build();
        var waitEntry = ActionPaletteEntry.All.First(e => e.Label == "Wait");
        editor.AddActionCommand.Execute(waitEntry); // index 0
        var second = editor.SelectedAction;
        editor.AddActionCommand.Execute(waitEntry); // index 1, now selected

        editor.MoveSelectedActionUpCommand.Execute(null);
        Assert.Same(second, editor.Actions[1]);
        Assert.NotSame(second, editor.Actions[0]);

        editor.MoveSelectedActionDownCommand.Execute(null);
        Assert.Same(second, editor.Actions[0]);
    }

    [Fact]
    public void TryBuildWorkflow_WithInvalidActionField_ReturnsFalseWithError()
    {
        var (editor, _, _, _, _) = Build();
        var comboEntry = ActionPaletteEntry.All.First(e => e.Label == "Key Combination");
        editor.AddActionCommand.Execute(comboEntry);
        ((KeyCombinationEditorViewModel)editor.SelectedAction!).KeysText = "NotARealKey";

        var success = editor.TryBuildWorkflow(out _, out var error);

        Assert.False(success);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryBuildWorkflow_AssignsSequentialActionIds()
    {
        var (editor, _, _, _, _) = Build();
        var waitEntry = ActionPaletteEntry.All.First(e => e.Label == "Wait");
        editor.AddActionCommand.Execute(waitEntry);
        editor.AddActionCommand.Execute(waitEntry);

        var success = editor.TryBuildWorkflow(out var workflow, out var error);

        Assert.True(success, error);
        Assert.Equal(["action-000", "action-001"], workflow.Actions.Select(a => a.Id));
    }

    [Fact]
    public async Task SaveAsync_PersistsToTheRepositoryAndSetsStatusMessage()
    {
        var (editor, _, workflows, _, _) = Build();
        editor.Name = "My Workflow";

        await editor.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(workflows.Saved.Values);
        Assert.Equal("My Workflow", saved.Workflow.Name);
        Assert.NotNull(editor.StatusMessage);
    }

    [Fact]
    public async Task RunAsync_ExecutesEveryActionAndRecordsExecutionInRepository()
    {
        var (editor, desktop, _, executions, _) = Build();
        var waitEntry = ActionPaletteEntry.All.First(e => e.Label == "Wait");
        ((WaitEditorViewModel)ActionEditorFactoryAdd(editor, waitEntry)).Milliseconds = 1;
        var clickEntry = ActionPaletteEntry.All.First(e => e.Label == "Click");
        editor.AddActionCommand.Execute(clickEntry);

        await editor.RunCommand.ExecuteAsync(null);

        // Progress<T> has no captured SynchronizationContext in a plain test (unlike the real
        // Avalonia UI thread), so it marshals callbacks via the thread pool; give the last one a
        // moment to land before asserting on LogLines.
        await Task.Delay(50);

        Assert.Contains("Click", desktop.Calls);
        Assert.Single(executions.StartedExecutions);
        Assert.Single(executions.CompletedExecutions);
        Assert.False(editor.IsRunning);
        Assert.Equal(2, editor.LogLines.Count);
    }

    [Fact]
    public async Task RunAsync_WithInvalidAction_DoesNotStartAnExecution()
    {
        var (editor, _, _, executions, _) = Build();
        var comboEntry = ActionPaletteEntry.All.First(e => e.Label == "Key Combination");
        editor.AddActionCommand.Execute(comboEntry);
        ((KeyCombinationEditorViewModel)editor.SelectedAction!).KeysText = "NotARealKey";

        await editor.RunCommand.ExecuteAsync(null);

        Assert.Empty(executions.StartedExecutions);
        Assert.NotNull(editor.StatusMessage);
    }

    [Fact]
    public void LoadingFromExistingRecord_PopulatesNameDescriptionAndActions()
    {
        var existing = new WorkflowRecord
        {
            Workflow = new AutomationStudio.Core.Workflows.Workflow
            {
                Id = "existing-id",
                Name = "Existing",
                Description = "desc",
                Actions = [new AutomationStudio.Core.Actions.WaitAction { Milliseconds = 42 }]
            },
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        var (editor, _, _, _, _) = Build(existing);

        Assert.Equal("existing-id", editor.Id);
        Assert.Equal("Existing", editor.Name);
        Assert.Equal("desc", editor.Description);
        var waitEditor = Assert.IsType<WaitEditorViewModel>(Assert.Single(editor.Actions));
        Assert.Equal(42, waitEditor.Milliseconds);
    }

    private static ActionEditorViewModel ActionEditorFactoryAdd(WorkflowEditorViewModel editor, ActionPaletteEntry entry)
    {
        editor.AddActionCommand.Execute(entry);
        return editor.SelectedAction!;
    }

    /// <summary>
    /// Design doc section 26's self-healing signal, surfaced as a real UI element for the first time
    /// (previously a log line only — see WorkflowHealer/TargetActions' own history). ElementFinder is
    /// deliberately left unconfigured for "Export" so resolution falls through to fallback
    /// coordinates, the one case with real data (see HealingProposalViewModel.CanApply) to apply.
    /// </summary>
    [Fact]
    public async Task RunAsync_TargetResolvesViaFallbackCoordinates_PopulatesAnApplicableHealingProposal()
    {
        var (editor, _, _, _, _) = Build();
        var clickTargetEntry = ActionPaletteEntry.All.First(e => e.Label == "Click Target");
        var target = (ClickTargetEditorViewModel)ActionEditorFactoryAdd(editor, clickTargetEntry);
        target.TargetText = "Export"; // configured, but the fake ElementFinder never finds it.
        target.UseFallbackCoordinates = true;
        target.FallbackX = 400;
        target.FallbackY = 300;
        target.TimeoutMilliseconds = 500; // fail fast if this regresses, rather than the 30s default.

        await editor.RunCommand.ExecuteAsync(null);
        await Task.Delay(50); // Progress<T> marshals via the thread pool in a plain test — see the note above.

        var proposal = Assert.Single(editor.HealingProposals);
        Assert.True(proposal.CanApply);
        Assert.True(editor.HasHealingProposals);
    }

    [Fact]
    public async Task ApplyHealingProposalCommand_ReaffirmsTheFallbackCoordinatesOnTheEditor_AndRemovesTheProposal()
    {
        // UseFallbackCoordinates must already be true for TargetResolver to ever try (and succeed
        // via) the Coordinates tier at all — a Coordinates-succeeded proposal can only occur when the
        // fallback was already configured, so "Apply" here is a defensive, idempotent re-write of the
        // same values (see WorkflowHealer's own Coordinates-specific branch), not a false→true flip.
        // What's under test: Apply must not corrupt the editor and must remove the resolved proposal.
        var (editor, _, _, _, _) = Build();
        var clickTargetEntry = ActionPaletteEntry.All.First(e => e.Label == "Click Target");
        var target = (ClickTargetEditorViewModel)ActionEditorFactoryAdd(editor, clickTargetEntry);
        target.TargetText = "Export";
        target.UseFallbackCoordinates = true;
        target.FallbackX = 400;
        target.FallbackY = 300;
        target.TimeoutMilliseconds = 500;
        await editor.RunCommand.ExecuteAsync(null);
        await Task.Delay(50);
        var proposal = Assert.Single(editor.HealingProposals);

        editor.ApplyHealingProposalCommand.Execute(proposal);

        Assert.Empty(editor.HealingProposals);
        Assert.False(editor.HasHealingProposals);
        Assert.True(target.UseFallbackCoordinates);
        Assert.Equal(400, target.FallbackX);
        Assert.Equal(300, target.FallbackY);
        Assert.NotNull(editor.StatusMessage);
    }

    [Fact]
    public async Task DismissHealingProposalCommand_RemovesTheProposal_WithoutTouchingTheEditor()
    {
        var (editor, _, _, _, _) = Build();
        var clickTargetEntry = ActionPaletteEntry.All.First(e => e.Label == "Click Target");
        var target = (ClickTargetEditorViewModel)ActionEditorFactoryAdd(editor, clickTargetEntry);
        target.TargetText = "Export";
        target.UseFallbackCoordinates = true;
        target.FallbackX = 400;
        target.FallbackY = 300;
        target.TimeoutMilliseconds = 500;
        await editor.RunCommand.ExecuteAsync(null);
        await Task.Delay(50);
        var proposal = Assert.Single(editor.HealingProposals);

        editor.DismissHealingProposalCommand.Execute(proposal);

        Assert.Empty(editor.HealingProposals);
        // Dismiss must never call StatusMessage/apply logic — the editor's target fields are exactly
        // what they already were, and DismissHealingProposalCommand's own body proves it never
        // touches _editorsByActionId or the editor at all (unlike ApplyHealingProposalCommand).
        Assert.Equal(400, target.FallbackX);
        Assert.Equal(300, target.FallbackY);
    }

    [Fact]
    public async Task RunAsync_TargetResolvesViaItsPrimaryConfiguredTier_ProducesNoHealingProposal()
    {
        var (editor, _, _, _, elementFinder) = Build();
        elementFinder.ElementsByText["Export"] = new DetectedElement { Bounds = new ScreenRect(100, 200, 50, 20), Type = ElementType.Button };
        var clickTargetEntry = ActionPaletteEntry.All.First(e => e.Label == "Click Target");
        var target = (ClickTargetEditorViewModel)ActionEditorFactoryAdd(editor, clickTargetEntry);
        target.TargetText = "Export"; // resolves via ElementFinder — exactly the tier it's configured for.

        await editor.RunCommand.ExecuteAsync(null);
        await Task.Delay(50);

        Assert.Empty(editor.HealingProposals);
        Assert.False(editor.HasHealingProposals);
    }
}
