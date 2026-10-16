# ModernCircleButton

- Source: `ModernControls.cs`, lines 225–331, at `https://github.com/plomodev/my-eco-app/blob/633a0f0d829e0e77bf290c873ab51d1d8cdebe76/ModernControls.cs`.
- Source evidence: circular icon button with primary, normal, hover, pressed, and destructive-hover states. The default size is 32×32px. Normal and hover fills map to RGB(42,34,66) and RGB(70,58,105); destructive hover uses RGB(225,29,72). GDI+ paints the circle and centered icon text.
- Usage evidence: `Form1.cs` constructs the minimize and close controls at lines 336 and 348.
- Dependencies: WinForms `Control` and GDI+ in source. No third-party component runtime.
- Web adaptation: native circular button requiring an accessible label; sized variants and optional destructive treatment. A glossy radial gradient, rim light, and pressed depth preserve the source's circular treatment while adding the requested 3D finish. No C# implementation was copied.
- Stories/tests: none found in the source repository.
