using System.Text;
using AutomationStudio.Abstractions;
using AutomationStudio.Core.Actions;
using AutomationStudio.Core.Workflows;
using AutomationStudio.Recorder.Normalization;

namespace AutomationStudio.Recorder;

/// <summary>
/// One "Record → Observe" pass of DAS.md section 25's teaching pipeline: captures which
/// application/window was focused when recording started (DAS's "Application, Window" signals) and
/// hands the raw input stream to <see cref="ActionNormalizer"/> for "Understand → Normalize →
/// Generate workflow". Persisting the result and registering it under the taught command name is a
/// level up from here — see <c>AutomationStudio.Applications.Planning.TeachingCoordinator</c>, which
/// is the thing that actually knows about <c>IWorkflowRepository</c>; this class stays storage-free
/// so it (and <see cref="ActionNormalizer"/>) can be fully unit-tested without SQLite.
/// </summary>
public sealed class TeachingSession(IInputRecorder inputRecorder, IWindowManager windowManager)
{
    private WindowInfo? _targetWindow;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _targetWindow = await windowManager.GetActiveWindowAsync(cancellationToken).ConfigureAwait(false);
        inputRecorder.Start();
    }

    /// <summary>Stops capturing and returns the generated <see cref="Workflow"/> — <paramref name="commandName"/> becomes both the workflow's display name and (slugified, matching DAS.md section 25's own "Start church service" → StartChurchService.workflow example) its id, so re-teaching the same command name later updates the same workflow instead of accumulating duplicates.</summary>
    public Workflow Stop(string commandName)
    {
        var events = inputRecorder.Stop();
        var actions = new List<WorkflowAction>();

        if (_targetWindow is { ProcessName.Length: > 0 } window)
        {
            actions.Add(new FocusApplicationAction { ProcessName = window.ProcessName });
        }

        actions.AddRange(ActionNormalizer.Normalize(events));

        var description = _targetWindow is { } target
            ? $"Taught by demonstration ({events.Count} captured input events, targeting '{target.Title}')."
            : $"Taught by demonstration ({events.Count} captured input events).";

        var workflow = new Workflow
        {
            Id = ToWorkflowId(commandName),
            Name = commandName,
            Description = description,
            Actions = actions
        };

        _targetWindow = null;
        return workflow;
    }

    private static string ToWorkflowId(string commandName)
    {
        var builder = new StringBuilder();
        foreach (var rawWord in commandName.Split([' ', '\t', '-', '_'], StringSplitOptions.RemoveEmptyEntries))
        {
            var word = new string(rawWord.Where(char.IsLetterOrDigit).ToArray());
            if (word.Length == 0)
            {
                continue;
            }

            builder.Append(char.ToUpperInvariant(word[0])).Append(word[1..].ToLowerInvariant());
        }

        return builder.Length > 0 ? builder.ToString() : $"Workflow-{Guid.NewGuid():n}";
    }
}
