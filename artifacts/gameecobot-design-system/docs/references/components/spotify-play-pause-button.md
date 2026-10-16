# SpotifyPlayPauseButton

- Source: `ModernControls.cs`, lines 580–668, at `https://github.com/plomodev/my-eco-app/blob/633a0f0d829e0e77bf290c873ab51d1d8cdebe76/ModernControls.cs`.
- Source evidence: source XML summary describes a white circular button with a black vector play or pause glyph. The track state is represented by `IsPlaying`.
- Usage evidence: `Form1.cs` creates it near line 300 and wires it to the audio tracker.
- Dependencies: WinForms and GDI+; audio transport behavior belongs to `AudioSessionTracker`.
- Story: `src/preview/demos/spotify-play-pause-button.tsx`; the interactive preview toggles state only and does not control an audio service.
- Status: implemented in the design-system preview.
