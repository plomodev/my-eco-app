# HardwareGraphControl

- Source: `HardwareGraphControl.cs`, at `https://github.com/plomodev/my-eco-app/blob/633a0f0d829e0e77bf290c873ab51d1d8cdebe76/HardwareGraphControl.cs`.
- Source evidence: live CPU, RAM, and visible-application samples are drawn as a grid and filled line graph. GPU telemetry explicitly reports unavailable rather than fabricating data. Sampling is timer-driven and bounded to 48 points.
- Usage evidence: `Form1.cs` embeds the control in the main dashboard at line 451 and switches its mode with the graph controls.
- Dependencies: WinForms, GDI+, native Windows system-time and memory readings. No third-party chart library.
- Stories/tests: none found in the source repository.
- Status: promoted into the requested task-manager preview; preserve the unavailable-GPU state and do not imply that the web preview is reading live PC telemetry.
