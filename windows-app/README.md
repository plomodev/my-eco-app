# GameEcoBot for Windows

GameEcoBot is a native Windows Forms desktop app based on the project's GameEcoBot design system.

Current release: **1.0.8**.

## What it does

- Reads CPU load and physical memory use locally through Windows APIs.
- Shows a rolling 60-sample CPU/RAM graph and the processor name reported by Windows.
- Scans local Steam libraries and Epic Games manifests. A game is listed only when its local installation folder exists.
- Keeps GPU telemetry marked unavailable until a real provider is configured.
- Offers a reversible Windows power-plan action. It asks before changing the computer-wide plan and records the previous plan so it can be restored.
- Keeps all scan results and measurements on the computer; it does not call an online game catalog or send telemetry.

## Build the Windows executable

The project targets .NET 10 and Windows 10 version 2004 or later. From the repository root:

```sh
dotnet publish windows-app/GameEcoBot.csproj \
  --configuration Release \
  --runtime win-x64 \
  --self-contained true \
  --output generated-artifacts/GameEcoBot-win-x64
```

The publish output contains a self-contained `GameEcoBot.exe`; the .NET runtime does not need to be installed on the target PC.

## Limitations

- GPU usage is intentionally not fabricated; a supported telemetry provider still needs to be added.
- Automatic game detection currently supports Steam and Epic Games local manifests.
- Windows UI execution and live hardware sampling must be verified on a Windows PC. This source can be cross-published from a non-Windows build environment.
