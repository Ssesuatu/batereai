# Changelog

## 0.1.2 - 2026-11-14

First public release.

- **Composer pill inside T3 Code:** anchors left of T3 Code's model picker to show model-specific
  remaining quota (`%`) and reset countdown, prompt-cache warmth (`WARM` / `COLD` / `active`) with
  live or estimated countdown, and conversation context usage (`CTX` with 80% / 90% alerts), plus
  hover tooltips, `−`/`+` segment collapse, and responsive width step-downs.
- **Multi-account & multi-provider Board (`Ctrl+Alt+U`, pill click, tray click, or second launch):**
  top battery-ring overview strip, per-provider quota window bars (`5h`, `Weekly`, `Monthly`,
  `Credits`, and model-family pools), per-model effective quota, one-click model switching into
  T3 Code, provider on/off syncing (`+ Manage` / `M` with `.t3usage.bak` backup and undo),
  **Best fit** (`F`), **Pin on top** (`P`), drag-and-drop card/ring reordering, and full keyboard
  navigation.
- **Manual live refresh (`R`):** refreshes provider quotas through the local T3 Code server across
  Stable, Beta, Dev, and Nightly installs, plus Kiro monthly credits via `kiro-cli` when installed.
- **System tray & autostart:** dynamic battery-fill tray icon, `--background` silent autostart
  (**Start with Windows**), `--quit` shutdown flag, and clean startup on machines where T3 Code has
  not run yet.
- **Local-only & zero-dependency build:** single C# / .NET Framework 4.8 executable using Windows'
  built-in `winsqlite3.dll` (`SQLITE_OPEN_READONLY`), a short-lived `orchestration:read` localhost
  session for manual refresh, and zero telemetry or internet traffic.
