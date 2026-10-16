# ModernButton

- Source: `ModernControls.cs`, lines 97–223, at `https://github.com/plomodev/my-eco-app/blob/633a0f0d829e0e77bf290c873ab51d1d8cdebe76/ModernControls.cs`.
- Source evidence: XML summary calls out rounded corners, subtle gradient, border, and hover micro-animation. `BorderRadius` defaults to 10px. Primary/hover/pressed defaults map to RGB(124,58,237), RGB(139,92,246), and RGB(109,40,217); the border uses RGB(168,85,247) and brightens on hover. Mouse enter/leave/down/up drives visual states.
- Usage evidence: `Form1.cs` creates repeated tab/action controls and dashboard actions (lines 738–779 and 480–493).
- Dependencies: WinForms `Button`, `ControlPaint`, and GDI+ in source. No third-party component runtime.
- Web adaptation: native button with primary, secondary, ghost, and danger treatments; native disabled and keyboard behavior. Raised gradient face, lower edge, and pressed compression add the requested depth. No C# implementation was copied.
- Stories/tests: none found in the source repository.
