using AutomationStudio.Abstractions;
using AutomationStudio.Core.Execution;
using AutomationStudio.Storage;
using AutomationStudio.UI.ViewModels;
using AutomationStudio.UI.ViewModels.ActionEditors;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutomationStudio.UI.Tests;

public class WorkflowEditorViewModelTests
{
    private static (WorkflowEditorViewModel Editor, FakeDesktopController Desktop, FakeWorkflowRepository Workflows, FakeExecutionRepository Executions) Build(
        WorkflowRecord? existing = null)
    {
        var desktop = new FakeDesktopController();
        var engine = new WorkflowExecutionEngine(
            desktop, new FakeScreenshotService(), new FakeWindowManager(), new FakeElementFinder(), NullLogger<WorkflowExecutionEngine>.Instance);
        var workflows = new FakeWorkflowRepository();
        var executions = new FakeExecutionRepository();

        var editor = new WorkflowEditorViewModel(engine, workflows, executions, onSaved: () => { }, existing: existing);
        return (editor, desktop, workflows, executions);
    }

    [Fact]
    public void NewEditor_HasAFreshIdAndDefaultName()
    {
        var (editor, _, _, _) = Build();

        Assert.False(string.IsNullOrWhiteSpace(editor.Id));
        Assert.Equal("New Workflow", editor.Name);
        Assert.Empty(editor.Actions);
    }

    [Fact]
    public void AddActionCommand_AddsAndSelectsANewEditor()
    {
        var (editor, _, _, _) = Build();
        var entry = ActionPaletteEntry.All.First(e => e.Label == "Wait");

        editor.AddActionCommand.Execute(entry);

        var added = Assert.Single(editor.Actions);
        Assert.Same(added, editor.SelectedAction);
        Assert.IsType<WaitEditorViewModel>(added);
    }

    [Fact]
    public void RemoveSelectedActionCommand_RemovesOnlyTheSelectedAction()
    {
        var (editor, _, _, _) = Build();
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
        var (editor, _, _, _) = Build();
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
        var (editor, _, _, _) = Build();
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
        var (editor, _, _, _) = Build();
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
        var (editor, _, workflows, _) = Build();
        editor.Name = "My Workflow";

        await editor.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(workflows.Saved.Values);
        Assert.Equal("My Workflow", saved.Workflow.Name);
        Assert.NotNull(editor.StatusMessage);
    }

    [Fact]
    public async Task RunAsync_ExecutesEveryActionAndRecordsExecutionInRepository()
    {
        var (editor, desktop, _, executions) = Build();
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
        var (editor, _, _, executions) = Build();
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

        var (editor, _, _, _) = Build(existing);

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
}
