# Component inventory

Source: `https://github.com/plomodev/my-eco-app`, pinned at commit `633a0f0d829e0e77bf290c873ab51d1d8cdebe76`.

The repository is a Windows Forms application, not a standalone component-library package. Its reusable component set is the eight public controls in `ModernControls.cs` plus `HardwareGraphControl`. The design-system preview adapts their visible API and interaction states to web controls; it does not copy C# implementation code. The repository has no declared license, so this catalog retains source references and visual evidence rather than verbatim code.

| Family | Build reference | Dependencies / blockers | Importance evidence | Chunk | Status |
| --- | --- | --- | --- | --- | --- |
| ModernCard | `components/modern-card.md` | React DOM only | Repeated dashboard container and metric surface in `Form1.cs` | 1 — pilot | implemented |
| ModernButton | `components/modern-button.md` | React DOM only | Repeated navigation and primary dashboard actions in `Form1.cs` | 1 — pilot | implemented |
| ModernToggleSwitch | `components/modern-toggle-switch.md` | React DOM only | Four settings controls in `Form1.cs` | 1 — pilot | implemented |
| ModernSlider | `components/modern-slider.md` | React DOM only | Angle-snapping percentage control in `Form1.cs` | 1 — pilot | implemented |
| ModernCircleButton | `components/modern-circle-button.md` | React DOM only | Window minimize and close actions in `Form1.cs` | 1 — pilot | implemented |
| SpotifyPlayPauseButton | `components/spotify-play-pause-button.md` | React DOM only | One media transport control in `Form1.cs` | 2 | implemented |
| SpotifyTrackButton | `components/spotify-track-button.md` | React DOM only | Previous and next media transport controls in `Form1.cs` | 2 | implemented |
| SpotifyCheckButton | `components/spotify-check-button.md` | React DOM only | Reusable custom check control in `ModernControls.cs` | 2 | implemented |
| HardwareGraphControl | `components/hardware-graph-control.md` | React DOM only | Live CPU/RAM/application graph embedded in the main dashboard | 1 — requested graph | implemented |

The preview includes the five controls with strongest dashboard use, the requested hardware graph, and all three audio controls. Stories demonstrate the control states only; they do not connect to an audio service. Source C# implementations, XML comments, and `Form1.cs` call sites provide the visual and usage evidence.
