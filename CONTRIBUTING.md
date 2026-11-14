# Contributing

Thanks for helping. Batereai is small on purpose; please keep it dependency-free.

## Build and test

```powershell
powershell -ExecutionPolicy Bypass -File build\build.ps1
.\bin\Batereai.exe --check    .\out\checks     # must exit 0
.\bin\Batereai.exe --selftest .\out\selftest   # look at the PNGs if you touched the UI
```

- C# 5 only (`/langversion:5`), .NET Framework 4.8, no NuGet packages.
- Keep each file's existing line endings and encoding (some files have a UTF-8 BOM).
- UI text is English. Keep strings short: the pill and cards have fixed widths, and `--selftest`
  shows when something gets cut off.

## Pull requests

- One change per pull request, with a line in `CHANGELOG.md` under the unreleased version.
- Include before/after PNGs from `--selftest` for visual changes.
- Never commit screenshots that show real account names, T3 Code's own source files or other
  copyrighted assets. `--selftest` uses sample data for this reason.

## Reporting a broken T3 Code update

Open an issue with your T3 Code version, Windows version and the last lines of
`%LOCALAPPDATA%\t3-usage\t3usage.log`. Check the log for account names before you paste it.
