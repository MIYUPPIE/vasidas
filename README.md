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
- Whisper.net (whisper.cpp bindings, ONNX-adjacent) for offline speech-to-text; PvRecorder for cross-platform microphone capture
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

# Run the full test suite (7 test projects)
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
| `src/AutomationStudio.Core` | Workflow engine (retry/timeout/pause/stop, sequential execution), action types, JSON (de)serialization |
| `src/AutomationStudio.Infrastructure` | Serilog logging setup |
| `src/AutomationStudio.Linux` | Linux backend: X11/XTest input, AT-SPI element finding, XWayland/portal screenshot fallback |
| `src/AutomationStudio.Windows` | Windows backend: SendInput/GDI, `EnumWindows`, loads UI Automation via reflection |
| `src/AutomationStudio.Windows.UIAutomation` | Windows UI Automation implementation — isolated `net10.0-windows` assembly (see the doc comment in `WindowsAutomationEnvironment.cs` for why it can't be referenced directly from the cross-platform UI project) |
| `src/AutomationStudio.Storage` | SQLite repositories (workflows, execution history) + migration runner |
| `src/AutomationStudio.Vision` | Cross-platform OCR: `RapidOcrEngine` (PaddleOCR primary), `TesseractOcrEngine` (CLI fallback), `CompositeOcrEngine` (engine-availability fallback between the two) |
| `src/AutomationStudio.Voice` | Voice command pipeline (DAS.md section 5): `PvMicrophoneCapture`, `EnergyVoiceActivityDetector`, `WhisperSpeechToText`, `DeterministicIntentRecognizer`, tied together by `VoiceCommandListener` |
| `src/AutomationStudio.UI` | Avalonia workflow builder app; `PlatformAutomationEnvironment` picks the Windows/Linux backend at runtime |
| `tests/*.Tests` | One test project per `src` project it covers |
| `workflows/` | Example workflow JSON (`obs-recording-linux.json`, `obs-recording-ocr.json`) |

## Status

**The roadmap this project follows is [`DAS.md`](DAS.md)**, not section 40 of the original spec —
a revised 11-phase plan (voice control, intent/task planning, a VideoPsalm plugin) adopted in
place of the original's phase ordering. `Desktop Automation Studio.md` remains the source for
architectural principles (section 2, 42) and low-level mechanism detail (X11/AT-SPI/UI Automation)
that DAS.md doesn't restate. Shipped so far, each with its own test suite (281 tests passing across
8 projects):

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
  local AI only enters once this deterministic layer is stable. Recognizing an `Intent` is as far
  as this phase goes; routing an intent to an actual workflow is Phase 6 (Task Planner).

Not yet built: VideoPsalm integration (Phase 4), context engine (Phase 5), task planner (Phase 6),
recorder/teaching (Phase 7), OpenCV/template matching (rest of Phase 8), more application profiles
(Phase 9), local LLM/VLM (Phase 10), deeper Linux/Wayland coverage (Phase 11).

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

## Testing your own changes

Every project has its own gate-test suite; run the whole solution's tests before committing
(`dotnet test AutomationStudio.slnx`). For a UI change, also launch the app
(`dotnet run --project src/AutomationStudio.UI`) and drive it directly — the test suite catches
logic regressions, not runtime wiring issues (e.g. a DI constructor mismatch, or an Avalonia
binding that only fails at runtime).

cd /home/okhub/Documents/PROJECTS/VASIDAS

# 1. Build everything
dotnet build AutomationStudio.slnx

# 2. Run the full test suite
dotnet test AutomationStudio.slnx

# 3. Launch the app
dotnet run --project src/AutomationStudio.UI
