# ModernCard

- Source: `ModernControls.cs`, lines 11–95, at `https://github.com/plomodev/my-eco-app/blob/633a0f0d829e0e77bf290c873ab51d1d8cdebe76/ModernControls.cs`.
- Source evidence: XML summary describes a rounded card with a subtle neon border, drawn pixel-by-pixel. `BorderRadius` defaults to 12px; `FillColor` defaults to RGB(19,16,36); `BorderColor` defaults to RGB(48,38,80). The painter uses anti-aliasing, transparent parent handling, and a reusable rounded-rectangle path.
- Usage evidence: `Form1.cs` uses cards for status, graph, metric, settings, and log surfaces (lines 369, 417, 466–468, 503, 575, 628, 798).
- Dependencies: WinForms `Panel` and GDI+ in source. No third-party component runtime.
- Web adaptation: semantic `div` container with optional eyebrow, title, description, and trailing content. Tokenized dark surface, violet rim light, and restrained perspective lift add the user-requested 3D emphasis. No C# implementation was copied.
- Stories/tests: none found in the source repository.
