namespace AutomationStudio.Ai.Intents;

/// <summary>One intent the local AI classifier is allowed to pick — deliberately a closed list (design doc section 23's "Local AI → Intent" is classification against known intents, not free invention); <see cref="AiIntentInterpreter"/> rejects anything not in this list, including a hallucinated name.</summary>
public sealed record IntentDescriptor(string Name, string Description, IReadOnlyList<string> Parameters);

/// <summary>The full intent vocabulary every plugin built in Phases 4/6/9 exposes, described for the local AI's classification prompt — one source of truth for "what can this system do," even though it necessarily duplicates the DeterministicIntentRecognizer's own regex patterns (the same trade-off already present between that recognizer and TaskPlanner's dispatch switch).</summary>
public static class IntentCatalog
{
    public static IReadOnlyList<IntentDescriptor> All { get; } =
    [
        new("DISPLAY_BIBLE_VERSE", "Show a specific Bible verse, verse range, or whole chapter", ["book", "chapter", "startVerse", "endVerse"]),
        new("NEXT_VERSE", "Move to the next Bible verse", []),
        new("PREVIOUS_VERSE", "Move to the previous Bible verse", []),
        new("DISPLAY_SONG", "Show a song by title", ["title"]),
        new("NEXT_SONG", "Move to the next song", []),
        new("PREVIOUS_SONG", "Move to the previous song", []),
        new("CLEAR_SCREEN", "Blank the presentation output", []),
        new("HIDE_OUTPUT", "Hide the presentation output entirely", []),

        new("OPEN_APPLICATION", "Launch an application by name", ["application"]),
        new("CLOSE_APPLICATION", "Close a running application by name", ["application"]),

        new("START_RECORDING", "Start recording in OBS", []),
        new("STOP_RECORDING", "Stop recording in OBS", []),
        new("START_STREAM", "Start streaming in OBS", []),
        new("STOP_STREAM", "Stop streaming in OBS", []),
        new("SWITCH_SCENE", "Switch OBS to a named scene", ["scene"]),

        new("NEXT_SLIDE", "Advance to the next PowerPoint slide", []),
        new("PREVIOUS_SLIDE", "Go back to the previous PowerPoint slide", []),
        new("GO_TO_SLIDE", "Jump to a specific PowerPoint slide number", ["slideNumber"]),
        new("START_PRESENTATION", "Start the PowerPoint slide show", []),
        new("END_PRESENTATION", "End the PowerPoint slide show", []),

        new("PLAY_MEDIA", "Play or resume media in VLC", []),
        new("PAUSE_MEDIA", "Pause media in VLC", []),
        new("STOP_MEDIA", "Stop media playback in VLC", []),
        new("NEXT_TRACK", "Skip to the next track in VLC", []),
        new("PREVIOUS_TRACK", "Go back to the previous track in VLC", []),
        new("TOGGLE_MUTE", "Mute or unmute VLC", []),
        new("TOGGLE_FULLSCREEN", "Toggle VLC fullscreen", []),

        new("NEW_TAB", "Open a new tab in Chrome", []),
        new("CLOSE_TAB", "Close the current Chrome tab", []),
        new("RELOAD_PAGE", "Reload the current Chrome page", []),
        new("GO_BACK", "Navigate back a page in Chrome", []),
        new("GO_FORWARD", "Navigate forward a page in Chrome", []),
        new("OPEN_URL", "Open a specific web address in Chrome", ["url"]),
        new("FIND_ON_PAGE", "Find text on the current Chrome page", ["text"]),

        new("NEW_FOLDER", "Create a new folder in the file manager", []),
        new("COPY_FILE", "Copy the selected file", []),
        new("CUT_FILE", "Cut the selected file", []),
        new("PASTE_FILE", "Paste the previously copied or cut file", []),
        new("RENAME_FILE", "Rename the selected file", ["name"]),
        new("DELETE_FILE", "Delete the selected file", []),
        new("SEARCH_FILES", "Search for files by name", ["query"]),

        new("SAVE_DOCUMENT", "Save the current document — parameters.application must be exactly \"word\" or \"excel\"", ["application"]),
    ];

    public static string ToPromptCatalog() => string.Join(
        "\n",
        All.Select(i => $"- {i.Name}: {i.Description}" + (i.Parameters.Count > 0 ? $" (parameters: {string.Join(", ", i.Parameters)})" : "")));
}
