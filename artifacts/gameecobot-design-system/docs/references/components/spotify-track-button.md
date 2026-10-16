# SpotifyTrackButton

- Source: `ModernControls.cs`, lines 676–747, at `https://github.com/plomodev/my-eco-app/blob/633a0f0d829e0e77bf290c873ab51d1d8cdebe76/ModernControls.cs`.
- Source evidence: circular previous/next track control; `ButtonType` distinguishes its direction.
- Usage evidence: `Form1.cs` creates previous and next instances near lines 287 and 314 and wires both to the audio tracker.
- Dependencies: WinForms and GDI+; track navigation behavior belongs to `AudioSessionTracker`.
- Story: `src/preview/demos/spotify-track-button.tsx`; the demo updates a local track counter only.
- Status: implemented in the design-system preview.
