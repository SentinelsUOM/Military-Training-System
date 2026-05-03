# Sentinels AAR Dashboard

Full-stack Next.js 14 After-Action Review dashboard for the Sentinels VR military training system.

## Stack

- **Next.js 14** App Router, server + client components
- **MongoDB** via Mongoose (global connection cache)
- **Recharts** — RadarChart, AreaChart, LineChart, ScatterChart, BarChart
- **CSS Modules** — no Tailwind, no external component libraries

## Getting Started

```bash
cd sentinels-aar
npm install
cp .env.local.example .env.local
# edit .env.local and set MONGODB_URI
npm run dev
```

Open [http://localhost:3000](http://localhost:3000).

## Environment Variables

Copy `.env.local.example` to `.env.local`:

```env
MONGODB_URI=mongodb+srv://<user>:<pass>@cluster.mongodb.net/sentinels?retryWrites=true&w=majority
NEXT_PUBLIC_APP_URL=http://localhost:3000
```

`NEXT_PUBLIC_APP_URL` is used by server components to call internal API routes.

## Demo Data

Click **Load Demo Data** on the home page, or POST to `/api/sessions/seed`:

```bash
curl -X POST http://localhost:3000/api/sessions/seed
```

This inserts 3 pre-built sessions (SEN-2026-0045, 0046, 0047). Safe to re-run — duplicates are ignored.

## Unity Integration

The Unity `WebReportExporter` sends session data via HTTP POST:

```
POST /api/sessions
Content-Type: application/json
Body: SessionSummary JSON
```

The API uses `findOneAndUpdate` with `upsert: true` keyed on `sessionId`, so re-posting the same session is idempotent.

**Unity endpoint target:**
```csharp
string url = "http://<dashboard-host>/api/sessions";
```

Replace `<dashboard-host>` with the machine running `npm run dev` (LAN IP for Quest 3).

## API Routes

| Method | Route | Description |
|--------|-------|-------------|
| `GET`  | `/api/sessions?page=1&limit=20` | Paginated session list (no large arrays) |
| `POST` | `/api/sessions` | Upsert session from Unity |
| `GET`  | `/api/sessions/:id` | Full session by `sessionId` |
| `DELETE` | `/api/sessions/:id` | Delete session |
| `GET`  | `/api/stats` | Aggregate stats across all sessions |
| `POST` | `/api/sessions/seed` | Insert demo data |

All routes include CORS headers (`*`) for Unity `UnityWebRequest` compatibility.

## Project Structure

```
sentinels-aar/
├── app/
│   ├── globals.css
│   ├── layout.jsx
│   ├── page.jsx                      ← Home (server component)
│   ├── not-found.jsx
│   ├── session/[id]/page.jsx         ← Session detail (server component)
│   └── api/
│       ├── sessions/route.js
│       ├── sessions/[id]/route.js
│       ├── sessions/seed/route.js
│       └── stats/route.js
├── components/
│   ├── HomeClient.jsx
│   ├── ui/
│   │   ├── MetricCard.jsx
│   │   ├── ScoreBar.jsx
│   │   ├── Badge.jsx
│   │   ├── LoadingSpinner.jsx
│   │   └── TabBar.jsx
│   └── dashboard/
│       ├── SessionClient.jsx         ← Sticky header + tab routing
│       ├── SummaryTab.jsx            ← RadarChart + scores + cognitive
│       ├── TimelineTab.jsx           ← SVG timeline + AreaChart + event log
│       ├── IncidentsTab.jsx          ← Severity filter + incident cards
│       ├── HostageTab.jsx            ← State journey + distress LineChart
│       └── ReplayTab.jsx             ← rAF scrubber + ScatterChart + actor board
└── lib/
    ├── mongodb.js                    ← Global connection cache
    ├── utils.js                      ← formatTime, scoreColor, chartTheme, etc.
    └── models/Session.js             ← Mongoose schema
```

## Dashboard Tabs

| Tab | Contents |
|-----|----------|
| **Summary** | Performance radar, score bars, combat stats, cognitive metrics, scenario config |
| **Timeline** | Interactive SVG dot timeline, activity density chart, filterable event log |
| **Incidents** | Severity counts, filter buttons, incident cards with badges |
| **Hostage** | Distress index chart, state journey bar, trigger table per hostage |
| **Replay** | rAF playback scrubber, top-down scatter positions, actor state board, active events |
