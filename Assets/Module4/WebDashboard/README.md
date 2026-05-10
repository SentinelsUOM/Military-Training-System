# Sentinels AAR Web Dashboard

**Module 4 · University of Moratuwa · 2026**

A standalone, self-contained HTML dashboard that renders a full After-Action Review
from a Unity session JSON file. No server, no install, no internet connection required.

---

## How to Use

1. **End a training session** in Unity — `SessionLogger.EndSession()` writes a JSON file automatically.
2. **Open `index.html`** in Chrome or Firefox (double-click, or drag it into the browser).
3. **Drop the session JSON** onto the landing zone, or click the zone to browse.
4. The dashboard loads instantly and shows five tabs:
   - **Summary** — scores, combat stats, cognitive analysis, scenario config
   - **Timeline** — interactive SVG event timeline + scrollable event log
   - **Incidents** — auto-detected notable moments with severity filtering
   - **Hostage** — each hostage's emotional journey and distress score
   - **Replay** — scrub through the mission and watch actor state changes

---

## Where Unity Writes the JSON

Unity's `SessionLogger.cs` saves to:

```
Application.persistentDataPath + "/Module4/Resources/session_{sessionId}.json"
```

### Finding `Application.persistentDataPath` on each platform

| Platform | Default path |
|---|---|
| **Windows PC (Editor)** | `C:\Users\{username}\AppData\LocalLow\{company}\{product}\Module4\Resources\` |
| **Windows PC (Build)** | `C:\Users\{username}\AppData\LocalLow\{company}\{product}\Module4\Resources\` |
| **macOS** | `~/Library/Application Support/{company}/{product}/Module4/Resources/` |
| **Meta Quest 3 (Android)** | `/storage/emulated/0/Android/data/{packageName}/files/Module4/Resources/` |

> The exact path is also printed to the Unity Console after every `EndSession()` call:
> `[SessionLogger] Saved → C:\Users\...\session_abc123.json`

### Getting the file off the Quest 3

After a VR session, transfer the JSON to your PC using one of these methods:

**Option A — Android Debug Bridge (ADB):**
```bash
adb pull /storage/emulated/0/Android/data/{packageName}/files/Module4/Resources/ ./sessions/
```

**Option B — Meta Quest Developer Hub:** Connect via USB → File Manager → navigate to the path above.

**Option C — Sidequest:** Use the built-in file manager.

---

## Print / PDF Export

Click **⎙ Export PDF** in the header, or press `Ctrl+P` / `Cmd+P`.

The browser's print dialog will open. To save as PDF:
- **Chrome/Edge:** Change destination to "Save as PDF"
- **Firefox:** Select "Microsoft Print to PDF" or "Save to PDF"

All five dashboard panels are printed stacked on separate pages.
Tab buttons and replay controls are automatically hidden in print output.
The report uses a light theme for legibility on paper.

---

## Notes

- The dashboard works entirely offline from `file://` — no data is sent anywhere.
- If the Google Font (Rajdhani) fails to load (no internet), the dashboard falls back
  to `Courier New, monospace` — all functionality is unaffected.
- Old session files that are missing the `cognitiveSummary` field are handled gracefully;
  those fields display as `N/A`.
- Multiple sessions can be compared by opening multiple browser tabs and loading one
  JSON file per tab.
- The "Load New Session" button in the header resets the dashboard so you can load
  a different file without refreshing the page.
