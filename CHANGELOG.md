# Changelog

## 2.0.0 - 2026-11-14

First public release.

- Pill next to T3 Code's model picker with quota left, reset time, prompt-cache warmth (`WARM`/`COLD`)
  with countdown, and context usage (`CTX`).
- Board (`Ctrl+Alt+U`, tray icon, or launching `Batereai.exe` again while running) with one card per
  provider, drag to reorder, pin, best fit, model switching, and provider on/off.
- Manual provider refresh through the local T3 Code server across stable, Beta, Dev, and Nightly
  installs.
- Kiro credit usage support via `kiro-cli` when installed.
- Starts cleanly when T3 Code has never run yet and begins watching once T3 Code creates its folders.
- Local-only operation: read-only SQLite access, a short-lived `orchestration:read` session for
  manual refresh, and no telemetry or internet connections.
