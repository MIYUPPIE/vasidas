# VASIDAS — Desktop Automation Studio

Cross-platform, 100% offline RPA / desktop-automation tool for Windows and Linux. Build, save, and
run workflows that drive other desktop applications — mouse, keyboard, window management,
screenshots, and native UI element interaction — without any fixed-coordinate scripting as the
primary mechanism. Full functional spec: [`Desktop Automation Studio.md`](Desktop%20Automation%20Studio.md).

## Stack

- C#, .NET 10
- Avalonia UI 12 (desktop UI, cross-platform)
- SQLite via `Microsoft.Data.Sqlite` (plain ADO.NET, no ORM)
- Windows UI Automation / Linux AT-SPI (native element finding)
- PaddleOCR via RapidOcrNet (ONNX Runtime + SkiaSharp), Tesseract CLI fallback (text recognition)
- OpenCvSharp4 (template matching / computer-vision fallback)
- Whisper.net (whisper.cpp bindings, ONNX-adjacent) for offline speech-to-text; PvRecorder for cross-platform microphone capture
- Public-domain KJV Bible dataset (https://github.com/aruljohn/Bible-kjv, MIT), embedded
- Serilog (logging)

## Requirements

- .NET 10 SDK (`dotnet --version` → `10.x`)
- Linux: an X11 or XWayland session for input injection and X11-path screenshots (falls back to
  the `xdg-desktop-portal` Screenshot API on pure Wayland — see [Linux notes](#linux-notes) below)
- Windows: no extra setup: `AutomationStudio.Windows.UIAutomation` is a separate `net10.0-windows`
  assembly, loaded via reflection only at runtime on Windows, so nothing Windows-only is needed to
  build on Linux

## Build, test, run

```bash
# Build everything
dotnet build AutomationStudio.slnx

# Run the full test suite (9 test projects)
dotnet test AutomationStudio.slnx

# Launch the app
dotnet run --project src/AutomationStudio.UI
```

App data (SQLite database, logs, failure screenshots) lives under
`~/.local/share/AutomationStudio/` on Linux (`%APPDATA%\AutomationStudio\` on Windows).

## Project layout

Each project is a single, focused concern (`AutomationStudio.slnx` is the solution file — the new
.NET XML format, not `.sln`):

| Project | Purpose |
|---|---|
| `src/AutomationStudio.Abstractions` | Platform-neutral interfaces and shared types (`IDesktopController`, `IScreenshotService`, `IWindowManager`, `IElementFinder`, `ICapabilityProvider`, PNG encoder) |
| `src/AutomationStudio.Core` | Workflow engine (retry/timeout/pause/stop, sequential execution), action types, JSON (de)serialization; `Vision/TargetResolver` (design doc section 12's Target Resolution Engine) and `Vision/WorkflowHealer` (section 26's self-healing signal) |
| `src/AutomationStudio.Infrastructure` | Serilog logging setup |
| `src/AutomationStudio.Linux` | Linux backend: X11/XTest input, AT-SPI element finding, XWayland/portal screenshot fallback, X11 RECORD-extension input recording (`X11InputRecorder`, DAS.md section 25) |
| `src/AutomationStudio.Windows` | Windows backend: SendInput/GDI, `EnumWindows`, loads UI Automation via reflection, `WH_KEYBOARD_LL`/`WH_MOUSE_LL` input recording (`WindowsInputRecorder`) |
| `src/AutomationStudio.Windows.UIAutomation` | Windows UI Automation implementation — isolated `net10.0-windows` assembly (see the doc comment in `WindowsAutomationEnvironment.cs` for why it can't be referenced directly from the cross-platform UI project) |
| `src/AutomationStudio.Storage` | SQLite repositories (workflows, execution history) + migration runner; `WorkflowCommandRegistry` resolves a taught command's spoken phrase to its stored workflow id |
| `src/AutomationStudio.Vision` | Cross-platform OCR: `RapidOcrEngine` (PaddleOCR primary), `TesseractOcrEngine` (CLI fallback), `CompositeOcrEngine` (engine-availability fallback between the two). `TemplateMatching/OpenCvTemplateMatcher` (design doc section 24) — OpenCvSharp4 normalized cross-correlation, the vision-fallback tier below OCR |
| `src/AutomationStudio.Voice` | Voice command pipeline (DAS.md section 5): `PvMicrophoneCapture`, `EnergyVoiceActivityDetector`, `WhisperSpeechToText`, `DeterministicIntentRecognizer` (DAS.md section 9's Bible/VideoPsalm intents), `Context/ContextManager` + `Context/IntentContextResolver` (DAS.md section 8 — resolves "next verse" against what's currently displayed), tied together by `VoiceCommandListener` |
| `src/AutomationStudio.Bible` | Local, offline Bible text lookup (DAS.md section 16): `KjvBibleService` over the embedded public-domain KJV dataset, `BibleReferenceParser` (DAS.md section 17 — "John 3:16" / "John chapter 3 verse 16" / "John three sixteen" all resolve to the same reference) |
| `src/AutomationStudio.Recorder` | Recorder/Teaching (DAS.md section 25): `ActionNormalizer` turns a raw `RecordedInputEvent` stream (from `IInputRecorder`) into the same `WorkflowAction`s a human would pick by hand — click/drag/double-click/scroll detection, typed-text accumulation, key-combination detection, Wait insertion for real pauses. `TeachingSession` also captures which window was focused at record-start. Storage-free by design — fully unit-testable without SQLite or input hardware |
| `src/AutomationStudio.Applications` | Application plugins (DAS.md section 14) — `VideoPsalm/IVideoPsalmPlugin`, profile-driven (`VideoPsalmProfile`), builds and runs `Workflow`s through the same generic engine everything else uses. `Planning/TaskPlanner` (DAS.md section 10) dispatches a resolved `Intent` to it, including RUN_WORKFLOW for a taught command; `Planning/TeachingCoordinator` drives the "teach new command" dialogue end to end; `Planning/VoiceAssistant` chains the whole voice→action pipeline (and, when teaching, the dialogue) end to end |
| `src/AutomationStudio.UI` | Avalonia workflow builder app; `PlatformAutomationEnvironment` picks the Windows/Linux backend at runtime |
| `tests/*.Tests` | One test project per `src` project it covers |
| `workflows/` | Example workflow JSON (`obs-recording-linux.json`, `obs-recording-ocr.json`, `obs-recording-target.json`) |

## Status

**The roadmap this project follows is [`DAS.md`](DAS.md)**, not section 40 of the original spec —
a revised 11-phase plan (voice control, intent/task planning, a VideoPsalm plugin) adopted in
place of the original's phase ordering. `Desktop Automation Studio.md` remains the source for
architectural principles (section 2, 42) and low-level mechanism detail (X11/AT-SPI/UI Automation)
that DAS.md doesn't restate. Shipped so far, each with its own test suite (623 tests passing across
15 projects):

- **Phase 1 — Automation Core**: mouse/keyboard, app open/close, wait, screenshot, sequential
  workflow execution with retry/timeout/pause/stop, JSON workflow (de)serialization. Plus, ahead of
  DAS.md's own sequence (built under the original spec's numbering before the roadmap switch):
  Avalonia UI/SQLite workflow builder, and OCR (PaddleOCR via RapidOcrNet primary, Tesseract CLI
  fallback — DAS.md's Phase 8) with FindText/ClickText/VerifyText actions.
- **Phase 2 — Windows UI Automation**: Windows UI Automation and Linux AT-SPI element finding,
  find/click element, type into element, with automatic fallback to coordinate-based interaction
  when a native automation pattern isn't supported.
- **Phase 3 — Voice Foundation**: offline mic capture (PvRecorder) → energy-based voice activity
  detection → local speech-to-text (Whisper.net) → deterministic command normalization → rule-based
  intent recognition (`AutomationStudio.Voice`), covering "Open OBS", "Start/Stop recording",
  "Next/Previous slide", "Pause"/"Resume". Deliberately not an LLM — DAS.md's own Phase 10 says
  local AI only enters once this deterministic layer is stable.
- **Phase 4 — VideoPsalm Integration**: local KJV Bible lookup + reference parser
  (`AutomationStudio.Bible`), a profile-driven `VideoPsalmPlugin` (`AutomationStudio.Applications`)
  exposing DisplayBibleVerse/NextVerse/PreviousVerse/DisplaySong/NextSong/PreviousSong/ClearScreen/
  HideOutput, and the intent recognizer extended with DAS.md section 9's Bible/VideoPsalm
  vocabulary — "Show John 3:16", "Next verse", "Show Amazing Grace", "Clear the screen" all resolve
  to a validated `Intent` (a nonexistent reference like "John chapter 99" is checked against the
  real dataset and never emitted as `DISPLAY_BIBLE_VERSE`). New `FocusWindow`/`FocusApplication`
  workflow actions (present in the original spec's action list but never built until this phase)
  underpin the plugin's window-focusing step. **VideoPsalm itself is not installed here** (no
  Linux build is advertised, and the product's own site references a Windows ribbon UI) — the
  plugin's keyboard shortcuts are sourced from VideoPsalm's own published documentation and
  compile/logic-verified, not live-verified against the real application; see the doc comment on
  `VideoPsalmProfile.Default` for exactly what's confirmed vs. best-effort.
- **Phase 5 — Context Engine**: `IContextManager` tracks short-lived session state (current
  application/workflow/verse/slide/media/scene/task/previous command — DAS.md section 8's exact
  list) and `IntentContextResolver` resolves a context-dependent intent against it — `NEXT_VERSE`/
  `PREVIOUS_VERSE` become a fully-specified `DISPLAY_BIBLE_VERSE` with the actual next/previous
  reference (crossing chapter and book boundaries correctly — e.g. Malachi 4:6 → Matthew 1:1 — and
  returning nothing past Genesis 1:1 or Revelation 22:21), the same shape the recognizer already
  produces for an explicit "John 3:16". `VoiceCommandListener` now runs every recognized intent
  through this resolver, so "Show John 3:16" then "Next verse" (DAS.md section 8's own example)
  resolves end to end to John 3:17 without the caller repeating the reference. `IIntentRecognizer`
  and `IBibleService` both grew the members this needed (`RecognizeAsync` became genuinely async
  for validation; `IBibleService` gained `GetNextVerseAsync`/`GetPreviousVerseAsync`/`BookOrder`).
- **Phase 6 — Task Planner**: `ITaskPlanner`/`TaskPlanner` (DAS.md section 10 — "Intent → Task
  Planner → Task Plan → Workflow Engine") dispatches a resolved `Intent` to whichever
  `IVideoPsalmPlugin` method or generic `Workflow` actually carries it out — every DISPLAY_BIBLE_
  VERSE/DISPLAY_SONG/NEXT_SONG/CLEAR_SCREEN/etc. intent maps onto the matching plugin method, and
  OPEN_APPLICATION/CLOSE_APPLICATION run a small generic workflow (`ApplicationLaunchRegistry`
  resolves a spoken app name to a real executable path — empty by default, deliberately: guessing a
  path for a machine this doesn't run on isn't something this codebase does). An intent this
  planner has no plan for yet fails clearly rather than silently no-opping or guessing.
  `VoiceAssistant` chains the *entire* pipeline from every phase since 3 into one call
  (`ListenAndExecuteAsync`) — this is the first point in the whole project where a spoken command
  actually does something, tested end to end including DAS.md section 8's own "Show John 3:16" →
  "Next verse" example resolving all the way through to an executed `DISPLAY_BIBLE_VERSE(John
  3:17)` call.
- **Phase 7 — Recorder/Teaching**: DAS.md section 25's "Record → Observe → Understand → Normalize →
  Generate workflow" pipeline. Global input capture (`IInputRecorder`) via the X11 RECORD extension
  on Linux and `WH_KEYBOARD_LL`/`WH_MOUSE_LL` low-level hooks on Windows; `ActionNormalizer`
  (`AutomationStudio.Recorder`) turns the raw event stream into the same action types a human would
  build by hand — down/up pairs at (nearly) the same spot become a `Click`, two of those close
  together become a `DoubleClick`, a down/up pair that moved becomes a drag's `MouseDown`+`MouseUp`,
  consecutive wheel ticks coalesce into one `Scroll`, typed characters accumulate into `TypeText`,
  held Ctrl/Alt/Super/Shift-with-a-named-key becomes a `KeyCombination`, and a real pause between
  actions becomes an explicit `Wait` (capped at 10s) — DAS.md's "Timing" signal, without which
  replay would fire everything back-to-back regardless of how long the user paused.
  `TeachingSession` also records which window was focused when capture started and prepends a
  `FocusApplication` step. `TeachingCoordinator` (`AutomationStudio.Applications`) drives the "Teach
  new command" → "Command name: ..." → (user performs the process) → "stop teaching" dialogue DAS.md
  section 25 describes almost verbatim, persists the result via `IWorkflowRepository`, and the
  taught command is immediately runnable by saying its name again — `WorkflowCommandRegistry`
  resolves it to a `RUN_WORKFLOW` intent, checked ahead of every built-in pattern so a re-taught
  command overrides its built-in namesake. `VoiceAssistant` intercepts TEACH_COMMAND/STOP_TEACHING
  itself rather than handing them to the task planner, and — specifically while awaiting a command
  name — routes the next utterance as raw text rather than through intent recognition, since a name
  like "Start church service" would otherwise be matched as OPEN_APPLICATION. The X11 RECORD path is
  live-verified (real XTest-injected clicks/keystrokes captured back out via the real extension, not
  mocked); one genuine deadlock was caught this way and fixed — resolving a captured keycode via
  `XKeycodeToKeysym` from inside the RECORD callback reenters the same blocked connection, so the
  keyboard mapping is now pre-fetched once before recording starts instead. The Windows hook path is
  compile-verified only, like every other Windows-only component here (no Windows machine available).
- **Phase 8 — OCR + Vision**: the OCR half shipped back in Phase 1 (under the original spec's
  numbering, ahead of DAS.md's own sequence); this phase adds the rest — template matching and the
  Target Resolution Engine design doc section 12 describes as one of the project's most important
  components. `TargetResolver` (`AutomationStudio.Core/Vision`) tries a `VisualTarget`'s tiers in
  section 12's fixed priority order — native element query (UI Automation/AT-SPI, already unified
  behind `IElementFinder`), then OCR text, then a template image, then fixed coordinates as the last
  resort — and reports back which tier actually found it, exactly section 24's "UI Automation →
  Accessibility → OCR → Template Matching → Computer Vision → Coordinates" chain, collapsed to the
  four tiers this codebase actually implements. Template matching itself is `OpenCvTemplateMatcher`
  (`AutomationStudio.Vision`) via OpenCvSharp4's normalized cross-correlation — chosen after
  confirming live that OpenCvSharp4 itself was never what broke Sdcb.PaddleSharp's OCR attempt back
  in Phase 1 (see that project's own csproj comment); the specific Ubuntu-version-pinned runtime
  package family was, and is now deprecated upstream in favour of the portable
  `OpenCvSharp4.official.runtime.linux-x64` build this project uses instead, verified live with a
  real `Cv2.MatchTemplate` call before committing to it. Three new unified workflow actions —
  `FindTarget`/`ClickTarget`/`VerifyTarget` — expose the whole chain (with the same 400ms poll-until-
  timeout waiting `OcrSearch` already established, generalized to cover element/OCR resolution
  together); `ClickTargetAction` invokes the native automation pattern when the ElementFinder tier
  resolved it, falling back to a coordinate click otherwise. `WorkflowHealer` implements design doc
  section 26's self-healing signal: when a higher-priority configured tier fails and a lower one
  catches it, the action logs a structured warning naming both tiers — design doc section 26's own
  "Update this workflow to use OCR as a fallback?" is phrased as a question to the user, so this
  surfaces the *signal* an eventual UI confirmation step would act on, rather than silently
  auto-editing a saved workflow. `obs-recording-target.json` is the worked example, mirroring
  `obs-recording-ocr.json`'s OBS scenario but through the full fallback chain instead of OCR alone.

Not yet built: more application profiles (Phase 9), local LLM/VLM (Phase 10), deeper Linux/Wayland
coverage (Phase 11). **`VoiceAssistant`/`TeachingCoordinator` are not yet wired into the Avalonia
UI**, and neither is a confirmation flow for `WorkflowHealer`'s proposals — the voice→action,
teach→workflow, and self-healing-detection pipelines all work end to end (tested that way), but
there's no in-app control that starts listening or surfaces a healing proposal yet; that's UI wiring
work, not a gap in the pipelines themselves.

## Linux notes

- Input injection needs XTest on an X11 or XWayland display. A pure-Wayland session with no
  XWayland restricts injection by design — the app's capability panel reports this rather than
  failing silently.
- Screen capture: `XGetImage` cannot read a Wayland compositor's framebuffer through XWayland's
  root window (confirmed BadMatch/BadAlloc on real hardware), so `LinuxScreenshotService`
  automatically falls back to the `xdg-desktop-portal` Screenshot API, which only needs a session
  D-Bus bus. The portal call shells out to `busctl` rather than a native .NET D-Bus client — see
  the doc comment in `src/AutomationStudio.Linux/Portal/PortalScreenshotService.cs` for why.
- The app's in-UI capability panel runs this detection at startup (section 32) and shows exactly
  what's available on your session — check it first if an action isn't behaving as expected.
- Voice: `WhisperModelProvisioning.EnsureModelAsync` downloads the ggml model once (network access
  needed only that first time — never committed to the repo, per CLAUDE.md's ban on committing
  model weights) and reuses the cached file on every later run. `AutomationStudio.Voice.Tests`
  touches the real microphone; xUnit parallel-collection execution isn't safe against real hardware
  and real CPU-bound inference running concurrently — confirmed live (the full suite reliably hung
  with default parallelism, every subset ran clean in isolation) — so that project's
  `xunit.runner.json` disables it. Don't remove that file without re-verifying.
- Recording: `X11InputRecorder` opens two of its own X11 connections (never the shared one other
  backends use) because the RECORD extension's own API requires it — `XRecordCreateContext` and
  `XRecordEnableContext` must run on the *same* connection (a context id is scoped to the connection
  that created it), while only `XRecordDisableContext` needs a separate one to unblock it; getting
  that backwards makes `XRecordEnableContext` fail immediately rather than block, confirmed live.
  `AutomationStudio.Linux.Tests` also touches real X11 connections directly (not just through the
  app's usual single shared one), so it carries the same parallel-collection caveat as Voice.Tests
  above, for the same reason (Xlib isn't thread-safe without `XInitThreads`).

## Testing your own changes

Every project has its own gate-test suite; run the whole solution's tests before committing
(`dotnet test AutomationStudio.slnx`). For a UI change, also launch the app
(`dotnet run --project src/AutomationStudio.UI`) and drive it directly — the test suite catches
logic regressions, not runtime wiring issues (e.g. a DI constructor mismatch, or an Avalonia
binding that only fails at runtime).
