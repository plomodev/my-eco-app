# ModernSlider

- Source: `ModernControls.cs`, lines 438–578, at `https://github.com/plomodev/my-eco-app/blob/633a0f0d829e0e77bf290c873ab51d1d8cdebe76/ModernControls.cs`.
- Source evidence: `Minimum` defaults to 5, `Maximum` to 100, and `Value` to 100; values are clamped and raise `ValueChanged`. Mouse drag updates the value. The active track is a violet gradient, the inactive track is dark violet, and the circular thumb has a violet glow and center.
- Usage evidence: `Form1.cs` creates the angle-snapping percentage control at line 546; the adjacent settings block provides its label and status.
- Dependencies: WinForms `Control`, `LinearGradientBrush`, and GDI+ in source. No third-party component runtime.
- Web adaptation: native range input with keyboard support, output value, configurable range, controlled or uncontrolled state, gradient fill, and luminous raised thumb. No C# implementation was copied.
- Stories/tests: none found in the source repository.
