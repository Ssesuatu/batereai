# Privacy

Batereai runs entirely on your computer.

## What it reads

- T3 Code's data folder (`%USERPROFILE%\.t3`, or `%T3CODE_HOME%` if set): provider quota caches,
  settings, and the local SQLite database for the active thread, prompt-cache and context usage.
- The T3 Code window, through Windows UI Automation, to place the pill next to the model picker.
- The output of `kiro-cli` usage, if Kiro is installed.

From the database it reads thread metadata (id, title, model, timestamps) and token-usage counts.
It never reads provider API keys, OAuth tokens or the text of your messages, and none of this data
leaves your computer.

## What it writes

| Where | What | When |
| :--- | :--- | :--- |
| `%LOCALAPPDATA%\t3-usage\` | Log (`t3usage.log`, rotated at 1 MB) and primary-instance PID file | Always |
| `%LOCALAPPDATA%\t3-usage\board-layout.json` | Card order and collapsed model lists | When you reorder or collapse cards on the board |
| `%LOCALAPPDATA%\t3-usage\kiro.json` | Last Kiro credit reading | Only when Kiro CLI is installed and returns usage |
| `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\T3Usage` | Autostart entry | Only if you enable *Start with Windows* |
| `~/.t3/userdata/settings.json` | The `enabled` flag of a provider | Only when you turn a provider on or off in Batereai. A backup is kept as `settings.json.t3usage.bak`. |

## Network

Batereai makes no internet connections and sends no telemetry. The only connection it opens is a
WebSocket to the T3 Code server on `localhost`, when you press **Refresh**. It authenticates with a
session limited to `orchestration:read` that expires after two minutes and is revoked as soon as the
refresh finishes. The session token is never written to disk or to the log.
