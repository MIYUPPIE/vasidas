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

# Run the full test suite (5 test projects)
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
| `src/AutomationStudio.UI` | Avalonia workflow builder app; `PlatformAutomationEnvironment` picks the Windows/Linux backend at runtime |
| `tests/*.Tests` | One test project per `src` project it covers |
| `workflows/` | Example workflow JSON (`obs-recording-linux.json`, `obs-recording-ocr.json`) |

## Status

Roadmap and phase definitions are in [section 40](Desktop%20Automation%20Studio.md) of the spec.
Shipped so far, each with its own test suite (242 tests passing across 6 projects as of Phase 4):

- **Phase 1 — Automation Core**: mouse/keyboard, app open/close, wait, screenshot, sequential
  workflow execution with retry/timeout/pause/stop, JSON workflow (de)serialization.
- **Phase 2 — Workflow Builder**: Avalonia UI, action palette, workflow editor, SQLite save/load,
  JSON import/export.
- **Phase 3 — UI Automation**: Windows UI Automation and Linux AT-SPI element finding, find/click
  element, type into element, with automatic fallback to coordinate-based interaction when a
  native automation pattern isn't supported.
- **Phase 4 — OCR**: PaddleOCR (via RapidOcrNet/ONNX, not Sdcb.PaddleSharp — see
  `AutomationStudio.Vision.csproj`'s comment) primary, Tesseract CLI fallback. FindText/ClickText/
  VerifyText actions; "wait for text" is FindText's own poll loop bounded by the action's existing
  timeout, not a separate action type.

Not yet built: computer vision / image matching (Phase 5), the action recorder (Phase 6),
conditions/loops (Phase 7), scheduling (Phase 8), local AI (Phase 9). **See `DAS.md`** — a
proposed, much larger revision of the product (voice control, intent/task planning, a VideoPsalm
plugin) with its own roadmap; not yet adopted or reconciled with the phases above.

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
