<img src="assets/batereai-256.png" alt="Batereai logo" width="64" height="64">

# Batereai

**Know your exact AI quota, prompt-cache timer, and context usage before you hit send.**

Batereai is a lightweight native Windows companion for [T3 Code](https://github.com/pingdotgg/t3code).
It embeds a live status pill right beside T3 Code's composer model picker and provides a multi-account
overview board so you can compare providers, spot exhausted pools, and switch to an account or model
that still has room.

---

## Features

### 1. Live Composer Pill (inside T3 Code)

![Batereai composer pill inside T3 Code](docs/pill.png)

Batereai anchors a translucent pill directly to the left of T3 Code's model picker via Windows UI
Automation — following window moves, resizes, and DPI changes without stealing focus. Everything on
the pill reflects **the exact provider and model currently selected in the active thread**:

- **Prompt-cache warmth (`WARM` / `COLD` / `CACHE`)** — shows a live countdown (`42m`, `0:37`) when
  the provider reports `promptCacheExpiresAt` (such as Claude), an estimated `~5m` window (`~4m`) when
  a provider only reports cached token counts, `WARM active` while a turn is streaming, or
  `COLD expired` once the cache expires so you know before sending an uncached turn.
- **Context window usage (`CTX`)** — displays conversation token usage (`64k`, `509k`, `1.5m`) and
  shifts to amber at **80%** and red at **90%** of the model's context limit.
- **Model-specific quota & reset countdown** — shows a circular ring, remaining percentage, and time
  until reset (`↻ 3h 51m`). When a provider enforces multiple rate-limit windows (for example
  `5-hour` + `Weekly`, or separate `Gemini`, `Claude & GPT`, and `Argon` pools on Antigravity),
  Batereai automatically displays **the most restrictive window that applies to the selected model**:
  - **Green (`60–100%`)**, **Yellow (`30–59%`)**, **Orange (`11–29%`)**, **Red (`0–10%`)**
  - **`stale`** (grey ring + amber label) when quota data is older than 30 minutes; **`awaiting`**
    once a reset timestamp has passed and T3 Code has not fetched fresh numbers yet.

**Pill interactions:**
- **Hover any segment** (`WARM`/`COLD`, `CTX`, or the quota meter) to see exact clock reset/expiry
  times, cache lifetime, `used / max` token counts, and data age.
- **Left-click** the pill to open or close the Board.
- **Click `−` / `+`** (or right-click → **Hide/Show WARM and CTX**) to collapse the pill to a compact
  quota-only badge. When the T3 Code window narrows, the pill also steps down automatically
  (*full → hides reset timer → hides WARM/CTX → hides cleanly before colliding*).
- **Right-click** for quick actions: **Refresh**, **Open T3 model picker**, **Hide/Show WARM and CTX**,
  and **Quit Batereai**.

---

### 2. Multi-Account & Multi-Provider Board

![Batereai multi-provider board](docs/board.png)

Open the Board anytime with **`Ctrl+Alt+U`**, clicking the composer pill, left-clicking the tray icon,
or running `Batereai.exe` again. Built for workflows juggling **multiple AI providers and multiple
accounts of the same provider**:

- **Active thread bar & battery-ring strip** — displays the active model, effort level, and provider
  (`● Gemini 4 Carbon · High · Ultra`, or an **Open T3 Code** launch button when T3 Code is closed),
  alongside one circular battery gauge per enabled provider for instant visual comparison. Click any
  ring to jump to its card, or drag rings to reorder.
- **Every quota window & per-model availability** — each card breaks down all rate-limit pools
  (`Session · 5h`, `Weekly`, `Monthly`, `Credits · monthly`, and model-family pools) with color-coded
  bars and reset timers, plus a collapsible **Model** list showing **each individual model's effective
  remaining `%`**. Depleted (`0%`) models dim automatically while models backed by another pool on the
  same account remain highlighted.
- **One-click model & account switching** — click any model row on any enabled card to switch T3 Code's
  composer picker directly to that provider and model. The active card is outlined in blue with an
  **`in use`** badge on the current model.
- **Turn providers on or off (`+ Manage` / `M`)** — toggle providers from a card's header switch or
  the **Manage** popover (`10 of 14 providers on`). Changes sync to T3 Code's
  `~/.t3/userdata/settings.json` (writing a `settings.json.t3usage.bak` backup first and offering an
  instant **Undo** banner) so disabled or exhausted accounts are hidden from T3 Code's model picker too.
- **Flexible layout & full keyboard control:**
  - **`F` (Best fit)** — auto-scales large multi-card grids so every enabled card fits on screen
    without scrolling, or toggles `100%` scale. Window edges and corners are also freely resizable.
  - **`R` (Refresh)** — triggers a live provider quota refresh through the local T3 Code server
    (Stable, Beta, Dev, and Nightly installs) plus `kiro-cli` when installed.
  - **`P` (Pin)** — keeps the Board floating on top while you work in other windows instead of
    closing when focus moves away.
  - **Drag & customize** — drag cards or top-bar rings (`Alt+Arrow` / `Alt+Home` / `Alt+End` via
    keyboard, or right-click a card to move it or collapse its model list). Layout preferences are
    saved in `%LOCALAPPDATA%\t3-usage\board-layout.json`.

---

### 3. System Tray Meter & Silent Autostart

- **Dynamic tray battery icon** — renders a pixel-crisp battery fill matching the active model's
  remaining `%`, adapting automatically to light and dark Windows taskbars and shifting to amber/red
  when quota is low.
- **Tray menu & autostart** — right-click the tray icon for **Open usage**, **Refresh**,
  **Start with Windows** (launches quietly with `--background` at sign-in), and **Quit**.
- **Clean first-boot behavior** — starts cleanly even if T3 Code has never been opened yet and begins
  watching automatically as soon as `~/.t3` is created.

---

## Requirements

- **Windows 10 or 11 (x64)** — uses .NET Framework 4.8, already built into Windows.
- **[T3 Code](https://github.com/pingdotgg/t3code)** installed and signed in to at least one provider.
  Batereai reads the quota data T3 Code already collects; it requires no separate API keys or sign-in.
- **Optional:** Kiro CLI at `%LOCALAPPDATA%\Kiro-Cli\kiro-cli.exe` (its default install path) for Kiro
  monthly credit tracking.

## Install

1. Download `Batereai-<version>-win-x64.zip` from [Releases](../../releases) and verify it against the
   `.sha256` file next to it.
2. Extract it to any writable folder, for example `%LOCALAPPDATA%\Programs\Batereai`.
   Keep the `fonts` folder next to `Batereai.exe`.
3. Run `Batereai.exe`. No administrator rights are needed:
   - The first launch starts quietly in the system tray.
   - Running `Batereai.exe` again (or pressing **`Ctrl+Alt+U`**) opens the Board.
   - Right-click the tray icon and enable **Start with Windows** to launch silently at sign-in.

To uninstall, uncheck **Start with Windows**, choose **Quit** from the tray menu (or run
`Batereai.exe --quit`), and delete the folder. Saved layout and logs live in `%LOCALAPPDATA%\t3-usage`.

| Command-line switch | Behavior |
| :--- | :--- |
| *(none)* | Starts in the tray if not running; opens the Board if already running |
| `-Open` / `t3usage:` | Starts (if needed) and immediately opens the Board |
| `--background` | Starts quietly in the tray and never pops open the Board (used by autostart) |
| `--quit` | Cleanly stops the running Batereai instance |

> Releases are not code-signed yet, so Windows SmartScreen may warn on first run.
> Choose **More info → Run anyway**, or build from source below.

## Build from source

```powershell
git clone https://github.com/Ssesuatu/batereai.git
cd batereai
powershell -ExecutionPolicy Bypass -File build\build.ps1
.\bin\Batereai.exe
```

Zero NuGet packages or external SDKs are needed — `build\build.ps1` compiles directly with the
`csc.exe` included in Windows (.NET Framework 4.8) and `winsqlite3.dll`.

## Tests

```powershell
.\bin\Batereai.exe --check    .\out\checks    # regression suite; writes checks.json, exit code 1 on failure
.\bin\Batereai.exe --selftest .\out\selftest  # renders pill and board states to PNG
```

`--selftest` renders synthetic sample scenarios as well as live T3 Code snapshots (`board-live*.png`)
when T3 Code is running. Review live PNGs before sharing them publicly.
`tracker-hang-recovery` requires a visible T3 Code window on the active desktop and is reported as
skipped on headless CI runners.

## How it works

| Signal / Action | Local source |
| :--- | :--- |
| Provider quota windows & model lists | `~/.t3/caches/*.json` written by T3 Code (or `%T3CODE_HOME%`) |
| Provider enabled/disabled state | `~/.t3/userdata/settings.json` (backed up to `.t3usage.bak` before toggle writes) |
| Active thread, prompt-cache TTL & context tokens | T3 Code's local SQLite database opened `SQLITE_OPEN_READONLY` via `winsqlite3.dll` |
| Pill placement & one-click model switching | Windows UI Automation against the T3 Code window (isolated on a watchdog thread) |
| Manual quota refresh (`R`) | Local T3 Code WebSocket server using a 2-minute `orchestration:read` session revoked immediately after refresh |
| Kiro monthly credits | `kiro-cli chat --no-interactive /usage` cached in `%LOCALAPPDATA%\t3-usage\kiro.json` |

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for a full component breakdown.

> **Compatibility note:** Batereai reads T3 Code's internal cache files, SQLite tables, and UI tree,
> which are not a public API and may change across T3 Code updates. If numbers look wrong or the pill
> stops appearing, please [open an issue](../../issues) with your T3 Code version.

## Privacy

Everything stays on your machine. Batereai sends **no telemetry**, reads **no message text or API
keys**, and makes **no internet connections** (only talking to T3 Code's local server on `localhost`
when you trigger a manual refresh). Full details in [PRIVACY.md](PRIVACY.md).

## License

[MIT](LICENSE). Third-party font and icon notices are listed in
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

Batereai is an independent community project and is not affiliated with T3 Tools Inc. or any AI
provider.
