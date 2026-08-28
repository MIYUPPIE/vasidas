# Desktop Automation Studio
## Cross-Platform Offline RPA & Computer-Control Platform

**Document Version:** 1.0  
**Target Platforms:** Windows 10/11 and Linux  
**Primary Language:** C#  
**Framework:** .NET 10  
**UI Framework:** Avalonia UI  
**Architecture:** Modular / Clean Architecture  
**Operating Mode:** 100% Offline  
**Application Type:** Desktop RPA / Workflow Automation

---

# 1. Project Overview

Desktop Automation Studio is a cross-platform desktop application that allows users to create, record, configure, execute, schedule, and monitor automated workflows that interact with other desktop applications.

The software must be capable of controlling applications such as:

- OBS Studio
- Microsoft Word
- Microsoft Excel
- VideoPlasm
- File managers
- Web browsers
- Media applications
- Custom desktop applications
- Other applications accessible through the operating system UI

The system must be capable of:

- Moving the mouse
- Clicking
- Double-clicking
- Right-clicking
- Typing text
- Pressing keyboard keys
- Executing keyboard shortcuts
- Opening applications
- Closing applications
- Switching windows
- Taking screenshots
- Detecting UI elements
- Reading screen text
- Finding images on screen
- Waiting for conditions
- Executing conditional logic
- Repeating actions
- Handling errors
- Recording user actions
- Replaying workflows
- Scheduling workflows

The system must operate without requiring an internet connection.

---

# 2. Core Design Principle

The software must NOT be built primarily around fixed mouse coordinates.

Avoid workflows such as:

```text
Move to X=700, Y=450
Click
```

as the primary automation method.

Instead, use a hierarchy of interaction mechanisms:

```text
1. Native UI Automation
        ↓
2. Accessibility/UI element detection
        ↓
3. OCR
        ↓
4. Image/template matching
        ↓
5. Computer vision
        ↓
6. Coordinate-based mouse interaction
```

The system should attempt the most reliable method first and fall back to less reliable methods when necessary.

---

# 3. Cross-Platform Requirement

The application must support:

```text
Windows
Linux
```

The workflow engine and workflow definitions should remain platform-independent.

Platform-specific functionality must be isolated behind interfaces.

Example:

```csharp
public interface IDesktopController
{
    Task MoveMouseAsync(int x, int y);
    Task ClickAsync(MouseButton button);
    Task DoubleClickAsync();
    Task TypeTextAsync(string text);
    Task PressKeyAsync(Key key);
    Task OpenApplicationAsync(string path);
    Task CloseApplicationAsync(string processName);
    Task<Screenshot> CaptureScreenAsync();
}
```

Windows and Linux implement this interface independently.

---

# 4. Recommended Technology Stack

## Core

```text
C#
.NET 10
```

## Desktop UI

```text
Avalonia UI
```

Avalonia is preferred over WPF because the application must run on both Windows and Linux.

## Database

```text
SQLite
```

## Workflow format

```text
JSON
```

SQLite should be the primary application database while JSON should be supported for workflow export/import.

## Computer Vision

```text
OpenCV
```

## OCR

Primary:

```text
PaddleOCR
```

Fallback:

```text
Tesseract OCR
```

## Windows UI Automation

Use:

```text
Windows UI Automation
UIAutomationClient / UI Automation APIs
Win32 APIs
```

## Linux UI Automation

Use:

```text
AT-SPI
```

The Linux implementation must account for differences between:

```text
X11
Wayland
GNOME
KDE
XFCE
```

## Logging

```text
Serilog
```

## Local AI

Optional advanced layer:

```text
ONNX Runtime
```

or another local inference runtime capable of executing supported models offline.

---

# 5. High-Level Architecture

```text
┌───────────────────────────────────────────────┐
│               AUTOMATION STUDIO               │
├───────────────────────────────────────────────┤
│                                               │
│                 Avalonia UI                   │
│                                               │
├───────────────────────────────────────────────┤
│                Workflow Engine                │
├───────────────────────────────────────────────┤
│              Action Execution Layer           │
├───────────────────────────────────────────────┤
│              Automation Abstraction           │
├───────────────┬───────────────┬───────────────┤
│   Windows     │    Linux      │    Vision     │
│   Backend     │    Backend    │    Engine     │
├───────────────┼───────────────┼───────────────┤
│ UI Automation │ AT-SPI        │ OpenCV        │
│ Win32         │ X11           │ PaddleOCR     │
│ Input APIs    │ Wayland*      │ ONNX Runtime  │
└───────────────┴───────────────┴───────────────┘
```

---

# 6. Recommended Project Structure

```text
AutomationStudio/
│
├── src/
│
│   ├── AutomationStudio.UI/
│   │   ├── Views/
│   │   ├── ViewModels/
│   │   ├── Controls/
│   │   └── Assets/
│   │
│   ├── AutomationStudio.Core/
│   │   ├── Workflows/
│   │   ├── Actions/
│   │   ├── Conditions/
│   │   ├── Execution/
│   │   └── Events/
│   │
│   ├── AutomationStudio.Abstractions/
│   │   ├── IDesktopController.cs
│   │   ├── IWindowManager.cs
│   │   ├── IElementFinder.cs
│   │   ├── IOcrEngine.cs
│   │   ├── IVisionEngine.cs
│   │   └── IScreenshotService.cs
│   │
│   ├── AutomationStudio.Windows/
│   │   ├── Input/
│   │   ├── UIAutomation/
│   │   ├── Windows/
│   │   └── Screenshots/
│   │
│   ├── AutomationStudio.Linux/
│   │   ├── Input/
│   │   ├── ATSPI/
│   │   ├── X11/
│   │   ├── Wayland/
│   │   └── Screenshots/
│   │
│   ├── AutomationStudio.Vision/
│   │   ├── OpenCV/
│   │   ├── OCR/
│   │   ├── Templates/
│   │   └── Models/
│   │
│   ├── AutomationStudio.Recorder/
│   │   ├── MouseRecorder/
│   │   ├── KeyboardRecorder/
│   │   └── ActionNormalizer/
│   │
│   ├── AutomationStudio.Storage/
│   │   ├── SQLite/
│   │   ├── Repositories/
│   │   └── Migrations/
│   │
│   └── AutomationStudio.Infrastructure/
│       ├── Logging/
│       ├── Configuration/
│       ├── Scheduling/
│       └── Processes/
│
├── tests/
│
├── models/
│
├── templates/
│
├── workflows/
│
└── docs/
```

---

# 7. Workflow Engine

The Workflow Engine is the heart of the application.

A workflow consists of ordered actions.

Example:

```text
Workflow
│
├── Open Application
├── Wait
├── Find Element
├── Click
├── Type Text
├── Keyboard Shortcut
├── Wait Until
├── Condition
│    ├── TRUE
│    └── FALSE
├── Loop
└── End
```

The engine must execute actions sequentially.

Each action should return an execution result.

Example:

```csharp
public enum ActionStatus
{
    Success,
    Failed,
    Timeout,
    Skipped,
    Cancelled
}
```

---

# 8. Initial Action Types

Version 1 must support:

### Application

```text
OpenApplication
CloseApplication
FocusApplication
FocusWindow
```

### Mouse

```text
MoveMouse
Click
DoubleClick
RightClick
MouseDown
MouseUp
Scroll
```

### Keyboard

```text
TypeText
PressKey
KeyCombination
```

Examples:

```text
CTRL + S
CTRL + C
CTRL + V
ALT + TAB
```

### Timing

```text
Wait
WaitUntil
Timeout
```

### Screen

```text
TakeScreenshot
FindImage
FindText
```

### Logic

```text
If
Else
Loop
Repeat
Stop
```

### Workflow

```text
RunWorkflow
PauseWorkflow
ResumeWorkflow
StopWorkflow
```

---

# 9. Workflow JSON

Example:

```json
{
  "id": "obs-recording",
  "name": "OBS Recording",
  "version": 1,
  "actions": [
    {
      "type": "OpenApplication",
      "path": "obs64.exe"
    },
    {
      "type": "Wait",
      "milliseconds": 5000
    },
    {
      "type": "FindText",
      "text": "Start Recording",
      "timeout": 10000
    },
    {
      "type": "Click"
    },
    {
      "type": "Wait",
      "milliseconds": 60000
    },
    {
      "type": "FindText",
      "text": "Stop Recording"
    },
    {
      "type": "Click"
    }
  ]
}
```

The JSON must remain platform-independent.

---

# 10. Element Detection System

The element finder must support multiple strategies.

```text
ElementFinder
      │
      ├── NativeUIFinder
      │
      ├── AccessibilityFinder
      │
      ├── OCRFinder
      │
      ├── TemplateFinder
      │
      ├── VisionFinder
      │
      └── CoordinateFinder
```

Each finder returns:

```csharp
public class DetectedElement
{
    public string? Text { get; set; }

    public Rectangle Bounds { get; set; }

    public double Confidence { get; set; }

    public ElementType Type { get; set; }

    public string? AutomationId { get; set; }
}
```

---

# 11. OCR System

PaddleOCR should be the primary offline OCR engine.

The OCR engine receives:

```text
Screenshot
```

and returns:

```text
Detected text
Bounding box
Confidence
```

Example:

```text
Screenshot
      ↓
PaddleOCR
      ↓
"Export"
      ↓
X: 742
Y: 510
Width: 80
Height: 30
      ↓
Automation Engine
      ↓
Click center
```

OCR should support configurable:

```text
Language
Confidence threshold
Region of interest
Scale
Preprocessing
```

---

# 12. OpenCV Image Detection

The system must support template matching.

Example:

```text
templates/
    export_button.png
    start_recording.png
    save_icon.png
```

Workflow:

```text
FindImage
template = "export_button.png"
threshold = 0.85
```

The vision engine:

```text
Capture screen
      ↓
OpenCV
      ↓
Template matching
      ↓
Confidence score
      ↓
Return bounding box
```

---

# 13. Computer Vision Layer

Computer vision should be used when:

- UI Automation cannot detect an element
- Application uses custom-rendered UI
- Buttons are represented only by icons
- Canvas-based interfaces are used
- Accessibility metadata is unavailable

The architecture should allow models to be added later.

Example:

```text
VisionEngine
│
├── TemplateMatching
├── OCR
├── ObjectDetection
└── LocalVLM
```

The VLM must be optional.

The application must remain fully functional without an AI model.

---

# 14. Local AI Requirement

If a local vision-language model is implemented, it must run entirely on the local computer.

No screenshot or workflow data should be uploaded to:

```text
OpenAI
Google
Anthropic
Azure
AWS
or any external service
```

unless a future version explicitly adds an optional online integration.

The default application must remain offline.

---

# 15. Action Verification

Every important action should support verification.

Bad:

```text
Click Export
Continue
```

Better:

```text
Click Export
       ↓
Wait
       ↓
Check whether "Exporting..." exists
       ↓
Wait
       ↓
Check whether "Completed" exists
       ↓
Continue
```

Actions should support:

```text
Timeout
Retry
Retry count
Verification
Failure behavior
```

Example:

```json
{
  "type": "ClickElement",
  "target": {
    "text": "Export"
  },
  "retryCount": 3,
  "timeout": 10000,
  "verify": {
    "type": "TextExists",
    "text": "Exporting"
  }
}
```

---

# 16. Error Handling

Automation will fail occasionally.

The engine must not silently continue after a critical failure.

Possible policies:

```text
STOP
RETRY
SKIP
RUN ALTERNATIVE ACTION
ASK USER
```

Example:

```text
Find "Export"
      ↓
Not found
      ↓
Retry 3 times
      ↓
Still not found
      ↓
Try OCR
      ↓
Still not found
      ↓
Take screenshot
      ↓
Log error
      ↓
Stop workflow
```

---

# 17. Action Recorder

The application should include a recorder.

User selects:

```text
● Record
```

Then performs actions normally.

The recorder captures:

```text
Mouse movement
Mouse clicks
Keyboard input
Keyboard shortcuts
Window changes
Application changes
```

However, raw coordinates should NOT automatically become the final workflow representation.

Example recorded action:

```text
Mouse click:
X=743
Y=510
```

The system should attempt to normalize it into:

```text
ClickElement:
text = "Export"
```

or:

```text
ClickElement:
automationId = "exportButton"
```

If no semantic target can be identified, retain the coordinate action as a fallback.

---

# 18. Recorder Data Flow

```text
User
 │
 │ performs action
 ↓
OS Event Capture
 │
 ↓
Action Recorder
 │
 ↓
Context Detection
 │
 ├── Application
 ├── Window
 ├── UI Element
 ├── Screenshot
 └── Coordinates
 │
 ↓
Action Normalizer
 │
 ↓
Workflow
```

---

# 19. Workflow Builder UI

The main interface should have:

```text
┌──────────────────────────────────────────────┐
│ Automation Studio                            │
├─────────────┬────────────────────────────────┤
│ ACTIONS     │ WORKFLOW                       │
│             │                                │
│ Open App    │ 1. Open OBS                    │
│ Click       │ 2. Wait 5 seconds              │
│ Type        │ 3. Find "Start Recording"      │
│ Key         │ 4. Click                       │
│ Wait        │ 5. Wait 60 seconds             │
│ Find Text   │ 6. Find "Stop Recording"       │
│ Find Image  │ 7. Click                       │
│ If          │                                │
│ Loop        │                                │
│             │                                │
│             │       [ RUN WORKFLOW ]         │
└─────────────┴────────────────────────────────┘
```

Actions should support drag-and-drop ordering.

---

# 20. Action Configuration

When an action is selected:

```text
Action: Find Text

Text:
[ Start Recording             ]

Search Region:
[ Full Screen ▼ ]

Timeout:
[ 10 seconds ]

Confidence:
[ 80% ]

Retry:
[ 3 ]

[Save]
```

---

# 21. Workflow Variables

The engine must support variables.

Example:

```text
{{username}}
{{filename}}
{{date}}
{{time}}
{{output_directory}}
```

Example:

```text
TypeText:

"Hello {{username}}"
```

Variables can come from:

```text
User input
Workflow configuration
System information
Environment variables
Previous action results
```

---

# 22. Conditions

Support:

```text
If element exists
If text exists
If image exists
If application is running
If window exists
If variable equals
If file exists
If process is running
```

Example:

```text
IF "Export Complete" exists

    → Close application

ELSE

    → Wait 10 seconds
    → Retry
```

---

# 23. Loops

Support:

```text
Repeat N times
For each item
While condition is true
Until condition is true
```

Example:

```text
WHILE "Rendering" exists

    Wait 5 seconds

END
```

---

# 24. Scheduling

Scheduling must work offline.

Examples:

```text
Every day at 8:00 AM
Every Monday
Every 30 minutes
Run once at 2:00 PM
Run when application starts
```

The scheduler must use the operating system's local clock.

No cloud scheduler is required.

---

# 25. Application Profiles

Create application-specific profiles.

Example:

```text
profiles/
    obs.json
    word.json
    videoplasm.json
    chrome.json
```

A profile may contain:

```text
Known UI elements
Templates
OCR settings
Window detection rules
Keyboard shortcuts
Fallback strategies
```

Example:

```json
{
  "application": "OBS",
  "elements": [
    {
      "name": "StartRecording",
      "text": "Start Recording"
    }
  ]
}
```

This improves reliability without hard-coding application behavior into the engine.

---

# 26. Screenshot Management

Screenshots should be captured locally.

Use cases:

```text
Debugging
Failed actions
Vision detection
OCR
Workflow recording
Verification
```

On failure:

```text
workflow failed
      ↓
capture screenshot
      ↓
save locally
      ↓
attach to execution log
```

The user should be able to inspect the screenshot.

---

# 27. Execution Log

The application must maintain detailed logs.

Example:

```text
09:20:01 Workflow started
09:20:02 OBS launched
09:20:07 Window detected
09:20:08 "Start Recording" found
09:20:08 Click executed
09:21:08 "Stop Recording" found
09:21:08 Click executed
09:21:09 Workflow completed
```

For failures:

```text
ERROR 09:21:08

Action: FindElement
Target: Export
Method: UI Automation
Result: Not Found

Fallback: OCR
Result: Not Found

Fallback: Template Matching
Result: Confidence 0.41

Required: 0.85

Workflow stopped.
```

---

# 28. Security and Privacy

The application is intended to control the local computer.

The software must clearly indicate when automation is running.

The application should provide:

```text
RUN
PAUSE
STOP
EMERGENCY STOP
```

The emergency stop should immediately terminate automation input.

Credentials should not be stored as plain text.

For sensitive variables, use the operating system's secure credential storage where practical.

---

# 29. Offline Requirement

The application must not require:

```text
Internet
Cloud API
Remote database
External authentication
Cloud OCR
Cloud computer vision
```

for normal operation.

All required components must be locally available.

The installer should bundle or install:

```text
Application
Runtime
OCR dependencies
Vision dependencies
Required native libraries
Default templates
```

Optional AI models may be separately installed because of their size.

---

# 30. Windows Backend

Windows backend responsibilities:

```text
Mouse input
Keyboard input
Window management
Process management
UI Automation
Screenshots
Application detection
```

Preferred hierarchy:

```text
Windows UI Automation
        ↓
Win32
        ↓
Input APIs
        ↓
Screen/OCR/Vision
```

---

# 31. Linux Backend

Linux backend must isolate display-server-specific functionality.

Architecture:

```text
LinuxBackend
│
├── AT-SPI
│
├── X11Backend
│
├── WaylandBackend
│
└── ScreenshotBackend
```

Important:

Wayland places stronger security restrictions on applications that attempt to inject arbitrary mouse/keyboard events into other applications.

Therefore, Linux support must not assume that unrestricted global mouse/keyboard injection will always be available.

Where possible:

```text
AT-SPI / accessibility
```

should be preferred over raw input injection.

X11 can provide broader input automation capabilities than Wayland.

The application must detect the current environment and report unsupported capabilities clearly.

---

# 32. Capability Detection

At startup:

```text
Detect OS
    ↓
Detect Desktop Environment
    ↓
Detect Display Server
    ↓
Detect available automation APIs
    ↓
Build capability map
```

Example:

```text
Operating System: Linux
Desktop: GNOME
Display Server: Wayland

UI Automation: Available
OCR: Available
OpenCV: Available
Mouse Injection: Restricted
Keyboard Injection: Restricted
```

The UI should show the user which features are available.

---

# 33. Plugin Architecture

The system should eventually support plugins.

Example:

```text
Plugins/
│
├── OBSPlugin
├── MicrosoftWordPlugin
├── VideoPlasmPlugin
└── BrowserPlugin
```

A plugin may define:

```text
Application detection
Application-specific actions
Known UI elements
Application-specific shortcuts
Templates
Verification rules
```

This prevents the core engine from becoming dependent on individual applications.

---

# 34. Example OBS Workflow

```text
Open OBS
    ↓
Wait until OBS window exists
    ↓
Find "Start Recording"
    ↓
Click
    ↓
Verify recording started
    ↓
Wait 60 seconds
    ↓
Find "Stop Recording"
    ↓
Click
    ↓
Verify recording stopped
    ↓
Workflow complete
```

---

# 35. Example Microsoft Word Workflow

```text
Open Word
    ↓
Wait until Word window exists
    ↓
Open document
    ↓
Type content
    ↓
CTRL + B
    ↓
Type heading
    ↓
CTRL + S
    ↓
Verify document saved
    ↓
Close Word
```

---

# 36. Example Video Workflow

```text
Open VideoPlasm
    ↓
Open project
    ↓
Wait for project
    ↓
Find Export
    ↓
Click
    ↓
Select output format
    ↓
Enter filename
    ↓
Start export
    ↓
Monitor progress
    ↓
Wait until export complete
    ↓
Verify output file exists
    ↓
Close application
```

---

# 37. Database Schema

Initial SQLite tables:

```text
workflows
-----------
id
name
description
version
platform
created_at
updated_at
enabled
```

```text
workflow_actions
----------------
id
workflow_id
sequence
action_type
configuration_json
enabled
```

```text
executions
----------
id
workflow_id
started_at
completed_at
status
error
```

```text
execution_logs
--------------
id
execution_id
timestamp
level
message
action_id
screenshot_path
```

```text
templates
---------
id
name
path
type
application
threshold
created_at
```

---

# 38. Testing Strategy

Testing must occur at multiple levels.

## Unit tests

Test:

```text
Workflow parser
Action serialization
Conditions
Variables
Retry logic
Timeout logic
```

## Integration tests

Test:

```text
Application launching
Mouse control
Keyboard control
UI Automation
OCR
OpenCV
Screenshot capture
```

## Platform tests

Windows:

```text
Windows 10
Windows 11
```

Linux:

```text
X11
Wayland
GNOME
KDE
```

---

# 39. Performance Requirements

The core automation engine should be lightweight.

Do not continuously run expensive AI inference.

Use:

```text
UI Automation → first
OCR → when required
OpenCV → when required
YOLO/VLM → only when required
```

Vision inference should be event-driven rather than continuously processing the screen.

Example:

```text
Wait 5 seconds
      ↓
Capture screenshot
      ↓
Detect target
      ↓
Stop vision processing
      ↓
Execute action
```

---

# 40. Development Roadmap

## Phase 1 — Automation Core

Build:

```text
Mouse
Keyboard
Open application
Close application
Wait
Screenshot
Workflow execution
```

Do not implement AI yet.

---

## Phase 2 — Workflow Builder

Implement:

```text
Avalonia UI
Action palette
Workflow editor
Save workflow
Load workflow
JSON import/export
SQLite
```

---

## Phase 3 — UI Automation

Implement:

```text
Windows UI Automation
Linux AT-SPI
Element inspection
Find element
Click element
Type into element
```

---

## Phase 4 — OCR

Implement:

```text
PaddleOCR
Find text
Click text
Wait for text
Verify text
```

---

## Phase 5 — Computer Vision

Implement:

```text
OpenCV
Template matching
Image detection
Confidence thresholds
```

---

## Phase 6 — Recorder

Implement:

```text
Mouse recorder
Keyboard recorder
Application detection
Window detection
Action normalization
Workflow generation
```

---

## Phase 7 — Conditions & Loops

Implement:

```text
If
Else
While
Repeat
Wait Until
Retry
Error handling
```

---

## Phase 8 — Scheduler

Implement:

```text
Scheduled workflows
Startup workflows
Recurring workflows
Local task execution
```

---

## Phase 9 — Advanced AI

Only after the deterministic system is stable:

```text
YOLO
ONNX Runtime
Local VLM
Semantic UI understanding
```

AI should augment the automation engine, not replace it.

---

# 41. MVP Definition

The first usable release should contain only:

```text
✓ Windows support
✓ Linux support
✓ Avalonia interface
✓ Workflow editor
✓ Open application
✓ Mouse movement
✓ Click
✓ Double click
✓ Keyboard input
✓ Keyboard shortcuts
✓ Wait
✓ Screenshot
✓ Workflow save/load
✓ JSON export/import
✓ SQLite
✓ Execution logs
✓ Stop/Pause
```

Then add:

```text
✓ Windows UI Automation
✓ Linux AT-SPI
✓ OCR
✓ OpenCV
✓ Recorder
✓ Conditions
✓ Loops
✓ Scheduler
```

AI should come later.

---

# 42. Important Engineering Rule

The developer must maintain a strict separation between:

```text
WHAT TO DO
```

and

```text
HOW THE OPERATING SYSTEM DOES IT
```

For example:

```text
Workflow:

Click "Start Recording"
```

must not know whether the implementation uses:

```text
Windows UI Automation
AT-SPI
OCR
OpenCV
Mouse coordinates
```

The workflow engine only requests:

```csharp
FindElement("Start Recording")
```

The platform implementation determines how that element is found.

This separation is essential for maintaining Windows/Linux compatibility.

---

# 43. Final Architecture

The final system should operate approximately as follows:

```text
                       USER
                         │
                         ▼
                ┌────────────────┐
                │ Avalonia UI    │
                └───────┬────────┘
                        │
                        ▼
                ┌────────────────┐
                │ Workflow       │
                │ Engine         │
                └───────┬────────┘
                        │
                        ▼
                ┌────────────────┐
                │ Action System  │
                └───────┬────────┘
                        │
              ┌─────────┴─────────┐
              │                   │
              ▼                   ▼
       Element Finder       Input Controller
              │                   │
      ┌───────┼────────┐          │
      ▼       ▼        ▼          ▼
   UI API    OCR     Vision    Mouse/Keyboard
      │       │        │          │
      └───────┴────────┴──────────┘
                    │
                    ▼
             Target Application
                    │
                    ▼
                Verification
                    │
              ┌─────┴─────┐
              ▼           ▼
            Success      Failure
              │           │
              ▼           ▼
           Continue     Retry/Fallback
```

---

# 44. Definition of Success

The project is successful when a developer can install the application on a Windows or Linux machine with no internet connection, create a workflow such as:

```text
Open OBS
→ Find "Start Recording"
→ Click
→ Wait
→ Find "Stop Recording"
→ Click
→ Verify recording stopped
```

and execute that workflow reliably without manually controlling the computer.

The same workflow architecture should also support applications such as Microsoft Word, VideoPlasm, browsers, Excel, file managers, and other desktop applications.

The application should prioritize **deterministic automation, verification, recoverability, and cross-platform abstraction** over AI-first automation.