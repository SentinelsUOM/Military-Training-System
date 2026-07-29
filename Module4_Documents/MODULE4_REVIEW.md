# Module 4 Review — After-Action Review (AAR) System

**Surveyed:** 17 July 2026 · **Branch:** `feture/VrRecord-1`
**Method:** static analysis of every file in `Assets/Module4/`, `Assets/Module4Integration/`, plus scene/prefab GUID resolution and `sentinels-aar/`. **The game was not run** — items marked *unverified at runtime* are code-truths that still need a live test.

---

## 1. What Module 4 is

Module 4 is the **scoring and after-action review** half of the trainer. Modules 1–3 build the level and run the fight; Module 4 watches, records, scores, and reports.

The pipeline is:

```
gameplay events → Module4Bridge → SessionLogger → (Performance + Incidents + Cognitive)
                                       ↓
                              SessionSummary (JSON)
                                       ↓
              ┌────────────────┬───────────────┬──────────────────┐
        MissionResultUI    WebReportExporter  DashboardUploader   (AARDashboardController)
        in-VR PASS/FAIL    standalone HTML    → Next.js + Mongo    ← DEAD CODE, see §4
```

**Architecture is genuinely good.** `TeamSentinels.Module4.asmdef` has `"references": []` — the core knows nothing about the game. `Module4Integration/` has no asmdef, so it lives in `Assembly-CSharp` and can see both sides. That is the correct dependency direction, and it is worth protecting.

---

## 2. Scoring — the formulas actually in the code

`PerformanceCalculator.cs`:

| Metric | Formula | Line |
|---|---|---|
| `accuracyScore` | `hits / max(totalShots, 1)` | `:39` |
| `safetyScore` | `hostagesSaved / hostagesTotal`, then `− 0.2 × friendlyFireCount` | `:45-51` |
| `speedScore` | `1 − clamp01(duration / 300s)` | `:76` |
| `missionSuccess` | `hostagesSaved ≥ 1 && !endedInFailure` | `:63-72` |
| `overallScore` | `safety×0.4 + accuracy×0.3 + speed×0.2 + (success ? 0.1 : 0)` | `:79-82` |

`IncidentExtractor.cs` auto-detects 7 incident types: `FirstContact`, `FirstShot`, `HostageEndangered`, `TerroristNeutralized`, `AlertCascade` (≥3 Alert transitions in 5 s), `HostageRescued`, `MissionEnd`.

---

## 3. What is DONE and works

Verified by reading code and resolving scene GUIDs:

- **The event bridge is complete.** `Module4Bridge` subscribes to `EventManager.OnEventRaised` and forwards **every** `ScenarioEventType` via `ToString()` — there is no per-event gap, and new enum members need no bridge change.
- **The integration is live in the shipping scene.** `EditorBuildSettings` lists exactly one scene, `Assets/XRI Starter Kit/XRI Starter Kit.unity`, and it contains `SessionLogger`, `ReplayRecorder`, `Module4Bridge`, `Module4SessionController`, `MissionResultUI`, `PlayerHealthHitbox`, `PlayerDamageFeedback` — all wired.
- **Session lifecycle is complete**: starts on `ScenarioReady`; ends on `player_down`, `hostage_executed`, `hostage_killed`, `hostages_rescued`, or `timeout`. `FreezeAllNpcs()` on end fixes a real bug (a guardian executing the hostage *after* the result was recorded).
- **Player damage is real end-to-end** — `PlayerHealthHitbox` implements the XRI kit's `IDamageable`, forwards to `PlayerHealth`, and the controller polls `health <= 0`.
- **`MissionResultUI` is fully runtime-built** — no prefab needed, so it cannot silently break from a missing asset.
- **The web report + web dashboard are complete and good.** `HtmlTemplateBuilder` (614 lines) emits a self-contained, correctly HTML-escaped report. `WebDashboard/index.html` (1401 lines) is a working standalone dashboard.
- **The `sentinels-aar` Next.js backend is real**, and implements exactly the contract `DashboardUploader` expects.

### Two backlog items are now FIXED — `REALISM_BACKLOG.md` is stale

The backlog was surveyed 13 July; Module 4 changed on 15 and 17 July. Both of these are done:

- **P0-1 (result screen could declare success on a failed mission)** — `PerformanceCalculator.cs:63-72` now reads the end reason and forces `missionSuccess = false` on `player_down` / `hostage_executed` / `hostage_killed`. Banner and subtitle now derive from the same reason.
- **P0-2 (shooting the hostage carried no penalty)** — `ScenarioEventType.HostageHit` was added, `HostageHitBox.cs:45` raises it, `Module4Bridge.cs:77-79` tags it `friendly_fire`, and the `−0.2` penalty now fires.

---

## 4. What is BROKEN — ranked

### ✅ B1 · The Quest build cannot save a session, and shows no result screen — **FIXED 17 Jul**

> **Fixed.** `SessionLogger.SaveToJson` now writes to `Application.persistentDataPath` and is wrapped in try/catch, so a disk failure can never again suppress `OnSessionComplete`. This also stops Editor runs writing session JSONs into `Assets/` (the cause of B6). The `WebDashboard/README.md` path is now accurate.

The single worst bug, and it was not in any backlog.

`SessionLogger.cs:266` writes to `Application.dataPath`:
```csharp
string dir = Path.Combine(Application.dataPath, "Module4", "Resources");
```
On Android/Quest, `dataPath` points **inside the read-only APK**. The write throws.

It gets worse — in `EndSession()`, `SaveToJson(summary)` (`:190`) runs **before** `OnSessionComplete?.Invoke(summary)` (`:195`), with **no try/catch**. So on a headset the exception propagates and the event never fires, meaning **`MissionResultUI` never appears, no HTML report is exported, and nothing is uploaded**. The mission just ends with nothing.

This is live: `build/` contains `buildone.apk`, `build2.apk`, `build3.apk`, and `AndroidTargetArchitectures: 2`.

`WebDashboard/README.md:29` documents `Application.persistentDataPath` — the correct path, which the code does not use. `WebReportExporter.cs:94` already uses `persistentDataPath` correctly, so the fix is a one-line change plus a try/catch.

**Fix:** use `Application.persistentDataPath`, and wrap `SaveToJson` in try/catch so a disk failure can never suppress the result screen.

### 🔴 B2 · Committed database credentials (security)

`sentinels-aar/.env.local` is **git-tracked** and contains `MONGODB_URI` with a **remote host and embedded `user:pass`**, committed since PR #34. `sentinels-aar/.gitignore:26-29` ignores `.env`, `.env.development.local`, `.env.test.local`, `.env.production.local` — but **not `.env.local`**, the one file that exists.

All four API routes set `'Access-Control-Allow-Origin': '*'`, and the repo is `SentinelsUOM/Military-Training-System` on GitHub.

**Action:** rotate the DB password now, `git rm --cached sentinels-aar/.env.local`, add `.env*.local` to `.gitignore`. Rotate first — the credential is in git history, so removing the file does not un-leak it. If the repo is public, treat as already compromised.

### 🔴 B3 · The trainee's accuracy score counts enemy gunfire

`PerformanceCalculator.cs:35` counts every `ShotFired` event as a trainee shot:
```csharp
int totalShots = events.Count(e => e.eventType == "ShotFired");
```
There is no filter on who fired. And `Assets/Prefabs/TerroristNPC.prefab` has **both** `ProjectileWeapon` and `GunFireDetector` on it — and `GunFireDetector` raises `ShotFired` on *every* bullet from the weapon it is attached to (I confirmed both GUIDs are present in the prefab).

So every terrorist round inflates the trainee's `totalShots`. Terrorists fire **3-round bursts**. `accuracyScore = hits / totalShots` therefore collapses toward zero the longer a firefight runs, and `misses = totalShots − hits` is badly inflated. Accuracy is **30% of the overall score**, so the AAR's headline number is currently not meaningful.

**Fix is easy:** `MissionEvent.sourceActorId` already carries the shooter's name (`Module4Bridge.cs:65`). Filter on it. (`misses` can also go negative — `hits` counts `TerroristHit`, which is independent of `ShotFired`.)

### 🟠 B4 · The in-VR AAR dashboard is dead code

`AARDashboardController` + its 5 panels (`SummaryPanel`, `EventTimelinePanel`, `IncidentListPanel`, `HostagePerspectivePanel`, `ReplayControlPanel`) — ~970 lines — appear in **no scene and no prefab**. I resolved every GUID and searched all `.unity`/`.prefab`.

They cannot run as-is:
- They need 3 prefabs that **do not exist**: `eventDotPrefab`, `incidentEntryPrefab`, `hostageEntryPrefab`.
- The hierarchy exists only as an ASCII comment, `AARDashboardController.cs:3-38`, prefaced *"build this manually"*.
- `Module4ManagerSetup` deliberately omits it.
- `AARDashboardController.cs:145`'s `gameObject.SetActive(true)` is unreachable — it subscribes in `Start()`, which never runs while inactive.

**The shipped AAR is the web dashboard, not the VR one.** That is a legitimate product choice — but decide it explicitly, because right now ~970 lines look finished and are not. Either build the prefabs, or delete the folder and let the web dashboard be the answer.

### ✅ B5 · Replay recorder deletes whole actors — **FIXED 17 Jul**

> **Fixed.** `TrimIfTooLarge` now downsamples by *tick* (detected via shared timestamps, so it survives actors registering mid-session), and `RecordLoop` builds its `WaitForSeconds` each iteration so the backoff actually applies. `StartRecording` resets the interval so one long mission no longer degrades the next. Verified with a headless test across 1–5 actors plus a mid-session join: the old code kept 1/2 actors at 2 actors and 2/4 at 4 actors; the new code keeps all of them and correctly halves the tick rate.

`ReplayRecorder.cs:154-155` trims by dropping every odd index:
```csharp
for (int i = _frames.Count - 1; i >= 0; i--)
    if (i % 2 == 1) _frames.RemoveAt(i);
```
But `_frames` is interleaved **one frame per actor per tick**. With an **even** number of actors this does not halve time resolution — it **erases half the actors from the replay entirely** (with 4 actors, you keep actors 0 and 2 and lose 1 and 3). With an odd count it mangles each actor's path.

Compounding it, the intended backoff at `:157` (`recordInterval *= 2f`) **does nothing**, because `RecordLoop` caches `new WaitForSeconds(recordInterval)` once before the loop (`:132`). So past 4000 frames it re-trims on nearly every tick and the replay degenerates.

**Fix:** drop every other *tick* (`(i / actorCount) % 2 == 1`), and move the `WaitForSeconds` inside the loop.

### 🟠 B6 · 9 MB of fake sessions are committed and ship in the build

`Assets/Module4/Resources/` contains 4 tracked session JSONs — **5.1 MB, 2.1 MB, 1.7 MB**, 7.2 KB. They are artifacts of B1 writing into `Assets/`. Because they are in a **`Resources/`** folder, Unity ships them inside the APK.

They are also **not representative**: `SessionLogger.SimulateTestSession()` and `AARTestRunner` only ever log the hostage state `"Follow"`, but `CountHostagesSaved` only counts `"Freed"` — so every simulated session scores `safetyScore = 0`, `missionSuccess = false`, and a **maximum overall of 0.5**.

**Fix:** delete them, and move fake-data generation out of the runtime class.

### 🟡 B7 · Fake-data generators live in production code and can hit the live DB

`SessionLogger.SimulateTestSession()` (`:203-241`) is 39 lines of hardcoded fake data inside the **runtime** logger — no `#if UNITY_EDITOR`. It calls the real `EndSession()`, which writes a real JSON and fires `OnSessionComplete`.

Because `WebReportExporter` and `DashboardUploader` both subscribe to the same **static** event and `Module4ManagerSetup` puts both on one GameObject, triggering either test context-menu **uploads fabricated data to the live database**.

### 🟡 B8 · Smaller correctness issues

- **Cognitive metrics are entirely fake.** `LogCognitiveUpdate` is called **only** from `AARTestRunner.cs:131-134` with literals. No gameplay path feeds it — so `cognitiveSummary` is empty/default in every real session, and the dashboard's whole "Cognitive Analysis" section is meaningless.
- **`CognitiveSummary.cs:56,64` mixes units** — blends `reactionTime` in *seconds* directly with 0–1 scores against 0.33/0.66 cutoffs. Any reaction ≥1.1 s pins load **and** stress to `"high"` regardless of anything else.
- **`missionTimeoutSeconds: 0`** in the shipping scene ⇒ the documented timeout safety net is **off**. An idle session never ends, never scores, never uploads.
- **`hostagesTotal` is inferred from recorded history** (`SessionLogger.cs:163-167`), so a hostage that never changed state is invisible — inflating `safetyScore`'s denominator downward.
- **`AllHostagesFreed()` returns `freed >= 1`** (`:395`, comment: *"Lenient rule for testing"*). Fine for 1 hostage; wrong the moment there are 2.
- **`DashboardUploader` is hardcoded to `http://localhost:3000`** — on a Quest, `localhost` is the headset, so every upload fails. Needs the laptop's LAN IP per deployment.
- **Two auto-start paths race** — `autoStartOnFirstEvent: 1` means any event beating `ScenarioReady` labels the session `RUNTIME_SESSION` instead of `VR_TRAINING`.
- **`PerformanceSummary.RecalculateOverallScore()` is dead** — the weighting formula is duplicated in two places that can drift.
- **`ReplayControlPanel.RefreshActorBoard`** runs `Where→GroupBy→ToDictionary→OrderByDescending` over up to 4000 frames **every frame** — a GC disaster on Quest (moot while B4 stands).
- `IncidentExtractor.cs:165` uses insertion order, not chronology, for `MissionEnd`.
- `?.` on Unity `Object`s (`AARDashboardController.cs:139-143`) bypasses Unity's overloaded `==` → `MissingReferenceException` on destroyed panels.

---

## 5. What is NOT DONE (never built)

- **Cognitive load / stress measurement** — the data model, the classifier, and the dashboard section all exist; **nothing measures it**. This is the largest genuinely-unbuilt feature.
- **The in-VR AAR dashboard** — see B4.
- **Richer telemetry into the AAR** — `TelemetryLogger.LogDecision`, `LogSnapshot`, `LogDirective` have no `OnXLogged` events, so responder scoring, perception snapshots, and leader directives reach the log files but never the dashboard.
- **`ReplayFrame.annotations`** — serialized on every frame, never populated; always `[]`.
- **Player replay state is a constant** — `Module4SessionController.cs:328` registers the player as `() => "Trainee"`, so the trainee's state is `"Trainee"` in every replay frame.

### Still unverified at runtime (from `REALISM_BACKLOG.md`, still open)

- **P0-3** player death → FAIL, **P0-4** the success path, **P0-5** the human shield — all three are *statically sound but have never been watched happen*. P0-4 matters most: `hostages_rescued` is the **only** win condition in the game.
- **P0-6** terrorists may not be able to path to the trainee's spawn.

---

## 6. Suggested order

| # | Item | Why first |
|---|---|---|
| 1 | **B2** rotate DB credentials | Security, and every hour it stays live is worse |
| 2 | ~~**B1** `persistentDataPath` + try/catch~~ | ✅ done 17 Jul |
| 3 | **B3** filter `ShotFired` by shooter | 30% of the score is currently noise |
| 4 | **B6** delete the 9 MB of fake sessions | Trivial; shrinks the APK. B1's fix stops new ones appearing |
| 5 | **P0-4** watch a success end-to-end | It is the only win condition |
| 6 | **P0-3** watch a death end-to-end | Pairs with B1 — both meet at the result screen |
| 7 | ~~**B5** replay trim~~ | ✅ done 17 Jul |
| 8 | **B4** decide: build the VR dashboard, or delete it | Stop ~970 lines from looking done |
| 9 | **B8** cognitive metrics | Biggest unbuilt feature; needs design, not just a fix |

**Still open and cheap:** B3 and B6 are each roughly a one-line-to-one-function change. With B1 and B5 done, doing B3 + B6 would leave Module 4 producing a trustworthy AAR on a headset — with cognitive metrics (B8) the only large unbuilt piece remaining.

---

## 7. Connecting the Quest to the AAR dashboard (LAN setup)

The upload path was never missing — `DashboardUploader` POSTs the `SessionSummary` to `/api/sessions`, and the Next.js route upserts by `sessionId`, so re-uploading is safe. It worked in the Editor and only ever failed on the headset. Three things blocked it; all three are now addressed:

| Blocker | Why it broke on Quest | Status |
|---|---|---|
| `EndSession` threw before firing `OnSessionComplete` | `dataPath` is the read-only APK ⇒ uploader never called | ✅ fixed (B1) |
| `dashboardBaseUrl: http://localhost:3000` | `localhost` on a Quest **is** the Quest | ✅ set to `http://192.168.8.158:3000` |
| `insecureHttpOption: 0` | Unity refuses cleartext HTTP; Editor worked only because loopback is exempt | ✅ set to `2` (Always allowed) |

### Runbook

1. **Laptop:** `cd sentinels-aar && npm run dev`. If the Quest can't reach it, force the bind: `npx next dev -H 0.0.0.0`.
2. **Same Wi-Fi** for laptop and Quest. Windows Firewall already has inbound Allow rules for Node on the Public profile, which is the Wi-Fi's category — no new rule needed.
3. **Build & deploy** the APK, run a mission, end it.
4. **Confirm** via `adb logcat -s Unity` — look for `[DashboardUploader] POST http://192.168.8.158:3000/api/sessions` followed by `Upload OK — HTTP 200`.

### The one fragile part

`192.168.8.158` is a **DHCP** address, so the router can reassign it and the URL is baked into the APK — meaning a rebuild to change it. Before relying on this for a demo, set a **DHCP reservation** on the router (or a static IP on the laptop). Otherwise the first thing that breaks on demo day is an IP that moved overnight.

### Fallback if the network misbehaves

Because B1 is fixed, the session JSON now reliably lands at
`/storage/emulated/0/Android/data/{packageName}/files/Module4/Resources/`.
`adb pull` it and drop it into `Assets/Module4/WebDashboard/index.html` — a fully offline AAR with no network at all. Worth rehearsing once as a backup.

---

## 8. Honest summary

Module 4 is **more complete than it looks in places, and less complete than it looks in others**.

The skeleton is genuinely well built — clean assembly boundaries, a generic event bridge with no per-event gaps, a complete session lifecycle, a real backend, and a real 1400-line web dashboard. Two of the backlog's P0 bugs have been fixed since it was written.

But **it has never been proven to work on the device it ships to.** The one bug that matters most (B1) means a Quest build produces no report and no result screen at all — and because everything has only ever been exercised in the Editor, that has gone unnoticed. Three of the four scores it reports are compromised: accuracy counts enemy bullets, cognitive load is fabricated, and safety's denominator is guessed. The most polished-looking artifacts in the repo — the committed session JSONs — are fake and cap out at 0.5.

None of that is hard to fix. B1 and B3 are small, surgical changes. The gap here is not engineering effort — it is that **nothing has been run on a headset end-to-end**, and the Editor hides exactly these bugs.
