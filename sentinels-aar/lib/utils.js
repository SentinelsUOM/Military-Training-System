export const formatTime = (seconds) => {
  const s = Math.max(0, seconds || 0)
  const m = Math.floor(s / 60)
  const r = Math.floor(s % 60)
  return `${m}:${r.toString().padStart(2, '0')}`
}

export const formatDate = (iso) => {
  if (!iso) return 'N/A'
  return new Date(iso).toLocaleDateString('en-GB', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  })
}

export const scoreColor = (v) =>
  v >= 0.8 ? 'var(--success)' : v >= 0.6 ? 'var(--warning)' : 'var(--danger)'

export const scoreLabel = (v) =>
  v >= 0.8 ? 'Excellent' : v >= 0.6 ? 'Good' : 'Needs Work'

// Returns a plain number (0–100), no % sign — callers append % themselves
export const scorePercent = (v) => Math.round((v || 0) * 100)

// Takes the category field (string) on an event, not the eventType
export const categoryColor = (category) => {
  const map = {
    Combat:        '#f85149',
    Movement:      '#d29922',
    Communication: '#58a6ff',
    Cognitive:     '#bc8cff',
    System:        '#8b949e',
    Other:         '#8b949e',
  }
  return map[category] || '#8b949e'
}

export const stateColor = (state) => {
  const map = {
    Idle:         '#8b949e',
    Unknown:      '#8b949e',
    Suspicious:   '#58a6ff',
    Alert:        '#d29922',
    Fearful:      '#d29922',
    Engage:       '#f85149',
    Panic:        '#f85149',
    TakeCover:    '#bc8cff',
    Calm:         '#3fb950',
    Follow:       '#3fb950',
    // Indigo, not the near-black used for inactive/neutralized — Freeze here is tonic
    // immobility, a severe hostage response research shows outranks Panic in distress
    // (see HostageTab.jsx STATE_INFO), so it must not read as "calm/off" on this map.
    Freeze:       '#818cf8',
    Held:         '#d29922',   // captivity — amber
    Threatened:   '#f0883e',   // captor's verbal threat — orange
    Wounded:      '#da3633',   // shot but alive — crimson
    Freed:        '#3fb950',   // extracted — green
    Down:         '#8e1519',   // killed — dark red
    Neutralized:  '#21262d'
  }
  return map[state] || '#8b949e'
}

// Args: (npcStateChanges[], actorId, timestamp) — npcChanges first for component call sites
export const getActorStateAt = (npcStateChanges, actorId, timestamp) => {
  const changes = (npcStateChanges || [])
    .filter(c => c.actorId === actorId && c.timestamp <= timestamp)
    .sort((a, b) => b.timestamp - a.timestamp)
  if (changes.length) return changes[0].newState
  const first = (npcStateChanges || []).find(c => c.actorId === actorId)
  return first ? first.previousState : 'Unknown'
}

// Args: (events[], currentTime) — returns string phase name
export const getMissionPhase = (events, currentTime) => {
  const hasCombat   = events.some(e => e.timestamp <= currentTime && e.category === 'Combat')
  const hasHostage  = events.some(e => e.timestamp <= currentTime && e.eventType === 'HostageFreed')
  const hasContact  = events.some(e => e.timestamp <= currentTime && (e.eventType === 'PlayerSeen' || e.eventType === 'TargetConfirmed'))
  if (hasHostage)  return 'Extraction'
  if (hasCombat)   return 'Room Clearing'
  if (hasContact)  return 'First Contact'
  return 'Infiltration'
}

// Chart chrome (grid lines, axis labels, tooltip background) tracks the page
// theme — a dark grid line is invisible on a white chart and vice versa. The
// categorical/series colors stay constant across themes (status/identity colors
// read fine on both a dark and a light surface; only structural chrome needs to flip).
const CHART_THEMES = {
  dark: {
    grid:    '#30363d',
    label:   '#8b949e',
    accent:  '#1f6feb',
    tooltip: '#161b22',
    colors:  ['#1f6feb', '#3fb950', '#d29922', '#f85149', '#bc8cff', '#58a6ff'],
  },
  light: {
    grid:    '#d0d7de',
    label:   '#59636e',
    accent:  '#0969da',
    tooltip: '#ffffff',
    colors:  ['#0969da', '#1a7f37', '#9a6700', '#cf222e', '#8250df', '#218bff'],
  },
}

export const getChartTheme = (theme) => CHART_THEMES[theme] || CHART_THEMES.dark

// Backward-compatible static default (dark) for any call site not yet using the
// theme-aware hook — keeps existing imports working unchanged.
export const chartTheme = CHART_THEMES.dark
