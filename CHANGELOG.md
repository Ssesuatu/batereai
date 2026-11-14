# Changelog

## 2.0.0 - unreleased

First public release.

- Pill next to T3 Code's model picker with quota left, reset time, prompt-cache warmth (WARM/COLD)
  and context usage (CTX).
- Board (Ctrl+Alt+U) with one card per provider, drag to reorder, pin, best fit, and provider on/off.
- Manual refresh through the local T3 Code server.
- English user interface.
- Manual refresh works with stable, Beta, Dev and Nightly T3 Code installs (previously Nightly only).
- `--check` runs every check group even after a failure, and skips the T3 window check when no
  T3 Code window is visible.
- The tray icon uses the Batereai icon instead of the generic Windows information icon.
- `--selftest` pill montage now renders each pill at its real width.
- No longer crashes when T3 Code has never run on the machine (no `settings.json` or `caches` folder yet),
  and starts watching T3's folders once they appear.
- `--quit` always ends the running instance within about 5 seconds, even while it is still starting up.
- Empty or half-written `settings.json` / `server-runtime.json` no longer cause errors.
