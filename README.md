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
- LLamaSharp (llama.cpp bindings, CPU backend) + Qwen2.5-1.5B-Instruct (GGUF, Q4_K_M) for optional local AI — intent classification and dynamic workflow generation, genuinely offline (no network calls, no hosted API)
- Public-domain KJV Bible dataset (https://github.com/aruljohn/Bible-kjv, MIT), embedded
- Serilog (logging)

## Requirements

- .NET 10 SDK (`dotnet --version` → `10.x`)
- Linux: an X11 or XWayland session for input injection and X11-path screenshots (falls back to
  the `xdg-desktop-portal` Screenshot API on pure Wayland — see [Linux notes](#linux-notes) below)
- Windows: no extra setup: `AutomationStudio.Windows.UIAutomation` is a separate `net10.0-windows`
  assembly, loaded via reflection only at runtime on Windows, so nothing Windows-only is needed to
  build on Linux
- Local AI (optional): nothing to install ahead of time — `LocalAiModelProvisioning.EnsureModelAsync`
  downloads the ~1.1GB Qwen2.5 GGUF model once to a local cache path on first use (never committed
  to the repo) and reuses it after that; the app works fully without it (DAS.md section 23's "must
  remain functional without a local LLM"), just without AI-classified/AI-generated fallback commands

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
| `src/AutomationStudio.Abstractions` | Platform-neutral interfaces and shared types (`IDesktopController`, `IScreenshotService`, `IWindowManager`, `IElementFinder`, `ICapabilityProvider`, `ILocalAiEngine`, PNG encoder) |
| `src/AutomationStudio.Ai` | Local AI (DAS.md section 23): `Engines/LlamaSharpEngine` (LLamaSharp/llama.cpp, CPU backend), `Engines/LocalAiModelProvisioning` (download-once model cache, mirrors `WhisperModelProvisioning`), `Intents/AiIntentInterpreter` (classifies against a fixed, closed `IntentCatalog` — never invents a new intent name), `Planning/AiWorkflowGenerator` (dynamic multi-step workflow generation, validated through the same `WorkflowSerializer`/`WorkflowExecutionEngine` every taught workflow uses) |
| `src/AutomationStudio.Core` | Workflow engine (retry/timeout/pause/stop, sequential execution), action types, JSON (de)serialization; `Vision/TargetResolver` (design doc section 12's Target Resolution Engine) and `Vision/WorkflowHealer` (section 26's self-healing signal) |
| `src/AutomationStudio.Infrastructure` | Serilog logging setup |
| `src/AutomationStudio.Linux` | Linux backend: X11/XTest input, AT-SPI element finding, XWayland/portal screenshot fallback, X11 RECORD-extension input recording (`X11InputRecorder`, DAS.md section 25) |
| `src/AutomationStudio.Windows` | Windows backend: SendInput/GDI, `EnumWindows`, loads UI Automation via reflection, `WH_KEYBOARD_LL`/`WH_MOUSE_LL` input recording (`WindowsInputRecorder`) |
| `src/AutomationStudio.Windows.UIAutomation` | Windows UI Automation implementation — isolated `net10.0-windows` assembly (see the doc comment in `WindowsAutomationEnvironment.cs` for why it can't be referenced directly from the cross-platform UI project) |
| `src/AutomationStudio.Storage` | SQLite repositories (workflows, execution history) + migration runner; `WorkflowCommandRegistry` resolves a taught command's spoken phrase to its stored workflow id |
| `src/AutomationStudio.Vision` | Cross-platform OCR: `RapidOcrEngine` (PaddleOCR primary), `TesseractOcrEngine` (CLI fallback), `CompositeOcrEngine` (engine-availability fallback between the two). `TemplateMatching/OpenCvTemplateMatcher` (design doc section 24) — OpenCvSharp4 normalized cross-correlation, the vision-fallback tier below OCR |
| `src/AutomationStudio.Voice` | Voice command pipeline (DAS.md section 5): `PvMicrophoneCapture`, `EnergyVoiceActivityDetector`, `WhisperSpeechToText`, `DeterministicIntentRecognizer` (DAS.md section 9's Bible/VideoPsalm intents), `Intents/AiAugmentedIntentRecognizer` (optional Tier-2 AI classification fallback, section 23), `Context/ContextManager` + `Context/IntentContextResolver` (DAS.md section 8 — resolves "next verse" against what's currently displayed), tied together by `VoiceCommandListener` |
| `src/AutomationStudio.Bible` | Local, offline Bible text lookup (DAS.md section 16): `KjvBibleService` over the embedded public-domain KJV dataset, `BibleReferenceParser` (DAS.md section 17 — "John 3:16" / "John chapter 3 verse 16" / "John three sixteen" all resolve to the same reference) |
| `src/AutomationStudio.Recorder` | Recorder/Teaching (DAS.md section 25): `ActionNormalizer` turns a raw `RecordedInputEvent` stream (from `IInputRecorder`) into the same `WorkflowAction`s a human would pick by hand — click/drag/double-click/scroll detection, typed-text accumulation, key-combination detection, Wait insertion for real pauses. `TeachingSession` also captures which window was focused at record-start. Storage-free by design — fully unit-testable without SQLite or input hardware |
| `src/AutomationStudio.Applications` | Application plugins (DAS.md section 14): `VideoPsalm`, `Obs`, `PowerPoint`, `Vlc`, `Chrome`, `Word`, `Excel`, `FileManager` — each an `I<X>Plugin` interface + a data-only `<X>Profile` (shortcuts/process name) + a plugin that builds and runs `Workflow`s through the same generic engine everything else uses, sharing focus/shortcut/click/run mechanics via `Plugins/ApplicationPluginBase`. `Planning/TaskPlanner` (DAS.md section 10) dispatches a resolved `Intent` to whichever plugin owns it, including RUN_WORKFLOW for a taught command; `Planning/TeachingCoordinator` drives the "teach new command" dialogue end to end; `Planning/VoiceAssistant` chains the whole voice→action pipeline (deterministic → optional AI intent classification → optional AI dynamic workflow generation, and, when teaching, the dialogue) end to end |
| `src/AutomationStudio.UI` | Avalonia workflow builder app; `PlatformAutomationEnvironment` picks the Windows/Linux backend at runtime. `ViewModels/VoiceControlViewModel` composes and drives the full voice pipeline (mic/STT/intent/AI/task-planning/teaching) behind model-download buttons and a Start/Stop Listening toggle — the app's one control surface for everything `AutomationStudio.Voice`/`.Applications`/`.Ai` build |
| `tests/*.Tests` | One test project per `src` project it covers |
| `workflows/` | Example workflow JSON (`obs-recording-linux.json`, `obs-recording-ocr.json`, `obs-recording-target.json`) |

## Status

**The roadmap this project follows is [`DAS.md`](DAS.md)**, not section 40 of the original spec —
a revised 11-phase plan (voice control, intent/task planning, a VideoPsalm plugin) adopted in
place of the original's phase ordering. `Desktop Automation Studio.md` remains the source for
architectural principles (section 2, 42) and low-level mechanism detail (X11/AT-SPI/UI Automation)
that DAS.md doesn't restate. Shipped so far, each with its own test suite (820 tests passing across
25 projects):

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
- **Phase 9 — More Applications**: OBS, PowerPoint, VLC, Chrome, Word, Excel, File Manager — DAS.md
  section 14's exact list. Every plugin follows VideoPsalm's own shape (an `I<X>Plugin` interface, a
  data-only `<X>Profile`, a plugin that builds a short `Workflow` and runs it through the existing
  engine), now sharing the focus/shortcut/click/run mechanics through a new
  `Plugins/ApplicationPluginBase` (VideoPsalm was refactored onto it too, retiring its own
  near-duplicate helpers and its own `VideoPsalmActionResult` in favour of a shared
  `PluginActionResult` every plugin now returns). OBS is the one plugin that does **not** use
  keyboard shortcuts — confirmed via research that OBS ships with no default recording/streaming
  hotkeys at all ("no universal defaults, hotkeys are user-defined," per OBS's own forum), so
  `ObsPlugin` clicks its controls by their on-screen text through Phase 8's Target Resolution Engine
  instead, the first real caller of that machinery outside its own tests. Every other plugin's
  shortcuts are sourced from each application's own published documentation (Microsoft's support
  site for PowerPoint/Word/Excel, Google's for Chrome, VLC's own hotkey docs, GNOME's for
  Nautilus) — VLC's Play/Pause deliberately send the identical key, matching VLC's own real
  play/pause *toggle* rather than inventing two shortcuts that don't exist. `FileManagerProfile` is
  the one profile that's genuinely OS-dependent (Explorer vs. Nautilus): its `Default` picks between
  two presets at startup, differing only in process name since every shortcut itself is confirmed
  identical between the two. DAS.md section 9 has no per-app vocabulary for Chrome/File Manager, so
  those get their own new, unambiguous intent names (`NEW_TAB`, `NEW_FOLDER`, etc.) per section 9's
  own "the system must allow new intents to be registered through plugins" — SAVE_DOCUMENT (which
  section 9 *does* name) dispatches to Word or Excel by an explicit `application` parameter, the
  same pattern OPEN_APPLICATION already uses, rather than guessing from unpopulated context. Word
  and Excel's other verbs (bold/italic/find/autosum/go-to-cell/...) deliberately have **no** voice
  intent at all — generic names would collide across the two (and PowerPoint) with no
  current-application context resolution to disambiguate them; the plugin methods are fully built
  and tested, just not voice-reachable yet. One real bug this phase's own tests caught: OBS's
  button-click actions inherited `WorkflowAction`'s 30-second default timeout, so a genuinely
  missing button took a real 30 seconds to fail — fixed with a 5-second default specific to
  `ApplicationPluginBase.ClickButton`, caught by a test run that was suspiciously slow, not by
  inspection.

- **Phase 10 — Local AI**: DAS.md section 23's "Deterministic Parser → known command execute
  immediately; unknown/complex → Local AI → Intent" fallback chain, as a genuinely offline local
  LLM — no network calls, no hosted API — per an explicit architecture decision (this project's
  "100% offline" identity outranks any general default toward routing LLM calls through a hosted
  service). New `AutomationStudio.Ai` project: `LlamaSharpEngine` (LLamaSharp/llama.cpp bindings,
  CPU backend) runs Qwen2.5-1.5B-Instruct (GGUF, Q4_K_M quantization, ~1.1GB, downloaded once via
  `LocalAiModelProvisioning` — never committed, same pattern as `WhisperModelProvisioning`).
  `ILocalAiEngine` (Abstractions) is a raw one-shot-completion primitive, matching `IOcrEngine`/
  `ISpeechToText`'s own shape — the semantic layer sits above it: `AiIntentInterpreter` classifies
  a spoken command against `IntentCatalog`, a fixed, closed list of every intent every plugin
  already exposes (rejects a hallucinated name, fails closed on a missing required parameter, never
  invents a new intent), and `AiWorkflowGenerator` generates a genuinely novel multi-step workflow
  (matching DAS.md section 4.3's own worked example, "Start Sunday service" → open VideoPsalm, load
  service, open OBS, start streaming, display opening slide) — validated through the exact same
  `WorkflowSerializer`/`WorkflowExecutionEngine` every taught or hand-authored workflow already runs
  through, never a separate, less-validated execution path. Three tiers, tried in order and each
  fully optional: `AiAugmentedIntentRecognizer` (Voice) tries the existing deterministic recognizer
  first, falling back to `AiIntentInterpreter` only on a miss; `VoiceAssistant` tries
  `AiWorkflowGenerator` only when neither of those resolved anything. Leaving the AI pieces
  unconfigured (the default — nothing wires them into the UI composition root yet) keeps every
  existing code path byte-for-byte unchanged, which is exactly section 23's "the application must
  remain functional without a local LLM." `StatelessExecutor`, not `InteractiveExecutor`, is
  required — confirmed live before writing `LlamaSharpEngine` that the interactive executor retains
  conversation state across calls on a shared context, silently returning empty output on the
  second/third of independent one-shot classification prompts; every call this project makes really
  is independent, never a back-and-forth conversation, so the stateless executor isn't just a
  workaround for that symptom, it's the architecturally correct choice. **Deliberately not built
  this phase**: DAS.md section 23's Local VLM / visual UI reasoning — a genuinely separate
  multimodal model and runtime path, and the least concretely specified item in the section's own
  bullet list, so it gets its own dedicated pass rather than being bolted on hastily alongside the
  LLM work. **One real, unresolved tension found while testing this**: DAS.md section 4.3's own
  Tier-3 worked example, "Start Sunday service," is actually claimed by Tier 1 in this codebase —
  `DeterministicIntentRecognizer`'s `OpenApplicationPattern` (`^(open|launch|start)\s+(?<app>.+)$`)
  matches "start" followed by anything, so that exact phrase resolves as
  `OPEN_APPLICATION(application="sunday service")` before AI ever gets a turn, never reaching the
  multi-step Tier-3 case the roadmap's own example illustrates. Not fixed this phase (a genuine
  design question — tighten the catch-all, or accept the example doesn't reach Tier 3 as written —
  not a one-line bug fix); see `tests/AutomationStudio.Applications.Tests/VoiceAssistantTests.cs`'s
  Tier-3 tests, which had to use a different phrase for exactly this reason. 798 tests passing
  across 25 projects, 0 build warnings — including 4 tests that run real inference against the
  actual downloaded model (`AutomationStudio.Ai.Tests/LlamaSharpEngineTests.cs`), not just fakes.

- **Phase 11 — Linux**: DAS.md section 31/32's "deepen AT-SPI/X11/Wayland/GNOME/KDE coverage" and
  "capabilities need to be detected rather than assumed." Scoped deliberately after investigating
  feasibility first: this dev machine's own portal backend (COSMIC) doesn't implement
  `org.freedesktop.portal.RemoteDesktop` at all (only `ScreenCast`), and no KDE session is available
  here either, so a native-Wayland input-injection backend (the `WaylandBackend` node in section
  31's architecture diagram) would have shipped compile-verified-only with zero way to catch a
  wire-protocol mistake — deferred rather than guessed at blind, same call as Phase 10's Local VLM.
  Instead: **X11 window focus is now the real EWMH protocol**, not just `XRaiseWindow`/
  `XSetInputFocus` called directly — `X11WindowManager.Focus` sends the `_NET_ACTIVE_WINDOW` client
  message to the root window (the same request a taskbar or alt-tab switcher sends) and verifies the
  window manager actually honoured it before reporting success, rather than assuming "we asked" means
  "it happened" (the entire previous behavior, confirmed live to have zero test coverage before this
  phase). That verification work surfaced a real, live-confirmed bug along the way: this dev
  machine's COSMIC/XWayland session advertises `_NET_ACTIVE_WINDOW` in `_NET_SUPPORTED` but never
  actually updates its value — switching real focus between two separately-launched XWayland windows
  left the root property pinned to the root window's own id the entire time. `GetActiveWindow` now
  falls back to scanning `_NET_CLIENT_LIST` for whichever window carries `_NET_WM_STATE_FOCUSED`
  (confirmed live to track real focus changes correctly on this compositor even though
  `_NET_ACTIVE_WINDOW` doesn't) — which also fixes a real, pre-existing Phase 7 bug: `TeachingSession`
  capturing "which window was focused when recording started" was silently wrong on this exact
  machine the whole time, not something this phase introduced. A second, bigger finding surfaced
  investigating the first: a plain `gnome-font-viewer`/`gnome-system-monitor` launch on this COSMIC
  session renders **natively via Wayland**, which means it never appears in `_NET_CLIENT_LIST` at
  all — X11-based window listing/focus is completely blind to it, only an XWayland-forced (or
  legacy X11-only) app is visible. This isn't just an input-injection restriction (section 31's own
  framing); it's the entire X11 window-management layer being unable to see most modern GNOME/GTK4
  apps by default on a Wayland desktop, now documented on `LinuxCapabilityProvider` as a `Notes`
  entry rather than silently returning "not found" indistinguishable from "not running." Separately,
  **AT-SPI element finding is now ~7-8x faster**: `AtspiElementFinder`'s tree walk explores a node's
  children concurrently (`Task.WhenAll`) instead of one at a time — each child's subtree is an
  independent D-Bus-round-trip-latency-bound unit of work, so a full walk of gnome-font-viewer's font
  grid (~5800 accessible objects) went from ~1.9s sequential to ~250-285ms parallel for the identical
  result, confirmed live before shipping. AT-SPI2's bulk `Cache.GetItems` (which would avoid the N+1
  round trips entirely) was investigated first and ruled out as non-viable on this desktop — every
  GTK app's own AT-SPI bridge returned `UnknownMethod`/`UnknownObject` for it; only Qt's bridge
  appears to implement it in practice. 802 tests passing across 25 projects, 0 build warnings.

- **Voice control wired into the Avalonia UI**: DAS.md's 11-phase roadmap ends at Phase 11, but every
  phase since 6 flagged the same gap — `VoiceAssistant`/`TeachingCoordinator` were built and tested
  but had no in-app control. New `VoiceControlViewModel` (`AutomationStudio.UI`) closes it: a fifth
  panel in the main window composes the *entire* pipeline every phase since 3 shipped — mic → VAD →
  Whisper STT → deterministic/AI-augmented intent recognition → context resolution → task planning
  (every plugin `TaskPlanner` supports is constructed here: VideoPsalm, OBS, PowerPoint, VLC, Chrome,
  Word, Excel, File Manager — whichever are actually installed on the end user's machine is what
  voice control ends up able to drive) → optional AI dynamic-workflow fallback, plus the "teach new
  command" dialogue — behind two model-download buttons and a Start/Stop Listening toggle. Neither
  model is bundled or auto-downloaded at startup: the panel reports what's missing and downloads on
  click, matching every other optional-capability pattern in this codebase (never silently degrade
  without saying why). `VoiceAssistant` gained `ListenAndExecuteWithTranscriptAsync` (a non-breaking
  sibling to `ListenAndExecuteAsync`, same pattern as Phase 10's `ListenWithTextAsync`) so the UI can
  show "you said: …" — the raw transcript was never previously exposed past `VoiceCommandListener`.
  Verified live: the real app was launched with a temporary console trace listener attached to
  Avalonia's own binding-diagnostics log, confirming zero binding errors on the new panel (two
  pre-existing, unrelated `SelectedAction`-is-null warnings were the only output, proving the trace
  capture actually works) — screenshotting the running window itself isn't possible in this session's
  COSMIC/Wayland environment (the same `gnome-screenshot` limitation noted in Phase 8), so this is the
  strongest live verification available here. 811 tests passing across 25 projects, 0 build warnings.

- **Fixed: the OPEN_APPLICATION/Tier-3 collision Phase 10 flagged but didn't resolve.** DAS.md
  section 4.3's own worked example, "Start Sunday service," used to be claimed by Tier 1 —
  `DeterministicIntentRecognizer`'s `OpenApplicationPattern` catch-all matched any "start X" phrase
  regardless of whether X was a real, launchable app, dispatching `OPEN_APPLICATION` and failing at
  task-planning time ("no known launch path") before AI ever got a turn. Fixed with a new
  `IApplicationNameResolver` (`AutomationStudio.Abstractions`) — `DeterministicIntentRecognizer`
  takes one as an optional third constructor parameter and only commits to `OPEN_APPLICATION` when
  the name actually resolves to something known; `ApplicationLaunchRegistryResolver`
  (`AutomationStudio.Applications`) adapts the existing `ApplicationLaunchRegistry` to it, keeping
  `AutomationStudio.Voice` (where the recognizer lives) from needing to reference
  `AutomationStudio.Applications` at all — the interface lives in the shared, neutral Abstractions
  layer instead. Leaving the resolver unconfigured (the default, unchanged for every existing
  caller) preserves the exact old always-match behavior. `VoiceControlViewModel` now wires a real
  resolver through to both the recognizer and `TaskPlanner`, sharing the same
  `ApplicationLaunchRegistry` instance. The regression test that had to work around this
  (`VoiceAssistantTests.ListenAndExecuteAsync_NothingMatchesButAiGeneratesAWorkflow_RunsIt`) now
  uses DAS.md's exact phrase, "Start Sunday service," directly — it genuinely reaches Tier 3 now,
  not a substitute phrase chosen to dodge the bug. 815 tests passing across 25 projects, 0 build
  warnings.

- **Self-healing proposals wired into the UI**: design doc section 26's "the original selector
  failed, a fallback succeeded — update this workflow?" was, until now, a structured log line
  nobody actually reads (`TargetResolutionHelper.LogResolution`, since Phase 8). Now surfaced as a
  real, dismissible panel in the workflow editor. Threading the signal through needed a genuine
  side-channel: every action's `ExecuteAsync` has a fixed `Task` signature with no return value for
  "also, here's a healing signal," so `ActionExecutionContext` gained a mutable `HealingProposal`
  property — safe specifically because `WorkflowExecutionEngine` builds a fresh context per action
  attempt, never reused — set by `TargetResolutionHelper` and read back by the engine right after
  `ExecuteAsync` returns, to attach onto the now-richer `ActionExecutionResult`. `WorkflowEditorViewModel`
  collects these into a new `HealingProposals` collection (cleared every run) with Apply/Dismiss
  commands, following this codebase's own established parameterized-`RelayCommand` pattern (`AddActionCommand(ActionPaletteEntry)`)
  rather than adding per-item commands to the proposal objects themselves. Writing the tests surfaced
  a genuine, narrow limitation in `WorkflowHealer` itself (Phase 8, unchanged): a Coordinates-tier
  resolution always returns the position exactly as pre-configured, so `HealingProposal.UpdatedTarget.Coordinates`
  can never actually differ from what the action's fallback fields already held — "Apply" in that
  case is a defensive, idempotent re-write rather than ever flipping a false setting to true, since
  the coordinates tier can only ever succeed if it was already configured. OCR/template-matching
  succeeding is deliberately informational only (no Apply button) — those positions are computed
  fresh each run, and freezing one observed position into a permanent coordinate fallback risks
  clicking the wrong thing if the layout ever shifts. Verified live the same way as the two panels
  above (trace-listener capture of Avalonia's binding diagnostics — zero new errors). 820 tests
  passing across 25 projects, 0 build warnings.

Not yet built: local VLM / visual UI reasoning (Phase 10's deferred items), native-Wayland input
injection via the RemoteDesktop portal (Phase 11's deferred item — see above), and no KDE-specific
testing exists anywhere in this project (no KDE session available in this environment; the X11/EWMH
protocol work above is spec-portable and should generalize, but that's reasoned, not confirmed, for
KDE specifically). Every "built but not wired into the UI" gap flagged across every earlier phase is
now closed.

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
- Local AI: `LocalAiModelProvisioning.EnsureModelAsync` follows the exact same download-once-and-
  cache pattern as `WhisperModelProvisioning` above, just for a much bigger file (~1.1GB vs. ~75MB
  for the tiny Whisper model) — `AutomationStudio.Ai.Tests` also disables xUnit parallel-collection
  execution for the same real-CPU-bound-inference reason, via its own `xunit.runner.json`.
- Recording: `X11InputRecorder` opens two of its own X11 connections (never the shared one other
  backends use) because the RECORD extension's own API requires it — `XRecordCreateContext` and
  `XRecordEnableContext` must run on the *same* connection (a context id is scoped to the connection
  that created it), while only `XRecordDisableContext` needs a separate one to unblock it; getting
  that backwards makes `XRecordEnableContext` fail immediately rather than block, confirmed live.
  `AutomationStudio.Linux.Tests` also touches real X11 connections directly (not just through the
  app's usual single shared one), so it carries the same parallel-collection caveat as Voice.Tests
  above, for the same reason (Xlib isn't thread-safe without `XInitThreads`).
- Window focus: `_NET_ACTIVE_WINDOW` on the root window is advertised in `_NET_SUPPORTED` on this
  dev machine's COSMIC session but its value never actually updates, confirmed live by switching
  real focus between two windows and reading it back unchanged both times — `X11WindowManager.
  GetActiveWindow` falls back to scanning for `_NET_WM_STATE_FOCUSED` on individual windows instead,
  which does track real focus correctly here. Don't assume `_NET_ACTIVE_WINDOW` alone is trustworthy
  on every compositor without checking; the fallback exists specifically because it wasn't on this one.
- Window visibility: a GTK app rendering natively via Wayland (the default for most modern GNOME/
  GTK4 apps, e.g. `gnome-font-viewer`) never appears in `_NET_CLIENT_LIST` at all on this dev
  machine's session — confirmed live via `xprop -root _NET_CLIENT_LIST` showing zero trace of a
  freshly-launched instance, while forcing `GDK_BACKEND=x11` on the same app made it show up
  correctly. `FocusWindowByProcessAsync`/`FocusWindowByTitleAsync`/`ListWindowsAsync` are blind to
  any window that isn't XWayland-backed, regardless of whether it's actually running and visible on
  screen — `LinuxCapabilityProvider` now notes this explicitly rather than the failure looking like
  "app isn't running." `AutomationStudio.Linux.Tests/LinuxWindowManagerTests.cs` launches its own
  test apps with `GDK_BACKEND=x11` specifically to test what the X11 backend IS capable of seeing.
- AT-SPI performance: `AtspiElementFinder`'s tree walk explores a node's children concurrently
  (`Task.WhenAll`), not sequentially — each child is an independent D-Bus round trip, so parallel
  exploration is a real ~7-8x win (confirmed live: ~1.9s → ~250-285ms for an identical, ~5800-node
  full walk), not a micro-optimization. AT-SPI2's bulk `Cache.GetItems` (which would avoid the
  round trips entirely) was investigated and found non-viable on this desktop — every GTK app's own
  AT-SPI bridge returns `UnknownMethod`/`UnknownObject` for it; it appears to be Qt-bridge-specific
  in practice, not a general AT-SPI2-core feature every toolkit implements.

## Testing your own changes

Every project has its own gate-test suite; run the whole solution's tests before committing
(`dotnet test AutomationStudio.slnx`). For a UI change, also launch the app
(`dotnet run --project src/AutomationStudio.UI`) and drive it directly — the test suite catches
logic regressions, not runtime wiring issues (e.g. a DI constructor mismatch, or an Avalonia
binding that only fails at runtime).
