# Hardware and installed-game data boundary

Source: `https://github.com/plomodev/my-eco-app`, pinned at commit `633a0f0d829e0e77bf290c873ab51d1d8cdebe76`.

## Hardware

- `HardwareGraphControl.cs` samples CPU and RAM once per second (lines 59–62, 78–88). CPU usage comes from Windows system-time deltas; RAM usage comes from `NativeMethods.GetMemoryUsagePercentage()` (lines 128–157, 80–82).
- GPU graph mode deliberately displays `N/D` and says its telemetry provider is not configured (lines 12–16, 205–240). A UI must not show a fabricated GPU percentage.
- `Form1.cs` gives the graph a fixed title of `AMD Ryzen 5 5500` (line 454), so the current source does not reliably identify the actual CPU model.

## Installed games

- `GameIntelligenceService` preloads a master list of more than 300 process names into `KnownGames` (lines 127–211).
- `BoostIntelligenceAsync` scans local launchers, Windows registry entries, folders, and shortcuts into a temporary `foundGameNames` set (lines 257–301).
- It then enriches that local set from online services and returns `KnownGames.Count`, which includes preloaded and online-known games (lines 303–321). Therefore `KnownGames.Count` must not be labeled as the number of installed games on the user's PC.
- The installed-games view should use only positively identified local installations; online catalog entries and the master database are not proof that a game is installed.

## Preview rule

The web preview has no access to a visitor's Windows hardware or installed files. Its CPU/RAM percentages are explicitly labeled as examples, GPU stays unavailable, and the installed-games area remains in an unscanned state. Live data belongs in the Windows app.
