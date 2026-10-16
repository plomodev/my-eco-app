# ModernToggleSwitch

- Source: `ModernControls.cs`, lines 333–436, at `https://github.com/plomodev/my-eco-app/blob/633a0f0d829e0e77bf290c873ab51d1d8cdebe76/ModernControls.cs`.
- Source evidence: public `Checked`, `Title`, and nullable `Subtitle` properties plus `CheckedChanged` event. Track defaults to 42×22px; active/inactive colors map to RGB(124,58,237) and RGB(36,30,60). The thumb is a white circle, positioned from the checked state.
- Usage evidence: four settings toggles in `Form1.cs` at lines 513, 585, 594, and 610.
- Dependencies: WinForms `Control` and GDI+ in source. No third-party component runtime.
- Web adaptation: button with `role="switch"`, `aria-checked`, controlled or uncontrolled state, optional description, and disabled state. A recessed track and raised thumb add depth while preserving the binary interaction. No C# implementation was copied.
- Stories/tests: none found in the source repository.
