'use client'

import { useEffect, useMemo, useState } from 'react'
import styles from './MissionLauncher.module.css'
import ThemeToggle from '@/components/ui/ThemeToggle'
import {
  DEFAULT_CONFIG,
  ENTRY_TYPES,
  LAYOUT_TYPES,
  MISSION_TYPES,
  NPC_LEVELS,
  PLACEMENT_STRATEGIES,
  ROOM_SIZES,
} from '@/lib/scenarioEnums'

const STORAGE_KEY_URL    = 'sentinels.questServerUrl'
const STORAGE_KEY_CONFIG = 'sentinels.lastScenarioConfig'

const HEALTH_UNKNOWN = 'unknown'
const HEALTH_OK      = 'ok'
const HEALTH_DOWN    = 'down'

export default function MissionLauncher({ open, onClose }) {
  // ── Server URL (persisted in localStorage) ──────────────────────────────
  const [serverUrl, setServerUrl] = useState('http://localhost:8080')
  const [health, setHealth] = useState(HEALTH_UNKNOWN)

  // ── Scenario config state ───────────────────────────────────────────────
  const [config, setConfig] = useState(DEFAULT_CONFIG)

  // ── Action state ────────────────────────────────────────────────────────
  const [busy, setBusy]   = useState(false)
  const [status, setStatus] = useState({ kind: 'idle', text: 'Ready' })

  // Restore from localStorage on first mount.
  useEffect(() => {
    if (typeof window === 'undefined') return
    const savedUrl = window.localStorage.getItem(STORAGE_KEY_URL)
    if (savedUrl) setServerUrl(savedUrl)
    const savedCfg = window.localStorage.getItem(STORAGE_KEY_CONFIG)
    if (savedCfg) {
      try {
        const merged = { ...DEFAULT_CONFIG, ...JSON.parse(savedCfg) }
        // Clamp values saved before the limits changed (rooms 3-5, terrorists <= 4).
        const rooms = Math.min(5, Math.max(3, merged.missionStructure?.roomCount?.max ?? 4))
        merged.missionStructure = { ...merged.missionStructure, roomCount: { min: rooms, max: rooms } }
        merged.entityConfiguration = {
          ...merged.entityConfiguration,
          terroristCount: Math.min(4, Math.max(1, merged.entityConfiguration?.terroristCount ?? 4)),
        }
        setConfig(merged)
      }
      catch { /* corrupt entry — ignore */ }
    }
  }, [])

  // Ping /health on open and whenever the URL changes.
  useEffect(() => {
    if (!open || !serverUrl) return
    let cancelled = false
    setHealth(HEALTH_UNKNOWN)
    const ctrl = new AbortController()
    const timer = setTimeout(() => ctrl.abort(), 4000)
    fetch(`${serverUrl.replace(/\/$/, '')}/health`, { signal: ctrl.signal })
      .then(r => r.ok ? r.json() : Promise.reject(r.status))
      .then(() => { if (!cancelled) setHealth(HEALTH_OK) })
      .catch(() => { if (!cancelled) setHealth(HEALTH_DOWN) })
      .finally(() => clearTimeout(timer))
    return () => { cancelled = true; ctrl.abort() }
  }, [open, serverUrl])

  const ms = config.missionStructure
  const ec = config.entityConfiguration
  const ex = config.executionControls

  // ── Field setters ───────────────────────────────────────────────────────
  const setMs = (patch) => setConfig(c => ({ ...c, missionStructure:    { ...c.missionStructure,    ...patch } }))
  const setEc = (patch) => setConfig(c => ({ ...c, entityConfiguration: { ...c.entityConfiguration, ...patch } }))
  const setEx = (patch) => setConfig(c => ({ ...c, executionControls:   { ...c.executionControls,   ...patch } }))

  // Single room-count control; the wire format still carries min/max, so both
  // are set to the same value.
  const setRoomCount = (v) => {
    const n = Number(v)
    setMs({ roomCount: { min: n, max: n } })
  }

  // ── Lightweight client-side warning (mirror of EvaluatorConfigPanel) ────
  const warning = useMemo(() => {
    if (ec.terroristCount > ms.roomCount.max * 2)
      return `terroristCount (${ec.terroristCount}) exceeds roomCount x 2 (${ms.roomCount.max * 2}).`
    return null
  }, [ms, ec])

  // ── Build the wire payload (drops empty optionals) ──────────────────────
  const buildPayload = () => ({
    schemaVersion: '1.0.0',
    missionStructure: {
      missionType: ms.missionType,
      roomCount:   { min: ms.roomCount.min, max: ms.roomCount.max },
      roomSize:    ms.roomSize,
      layoutType:  ms.layoutType,
      entryType:   ms.entryType,
    },
    entityConfiguration: {
      hostageCount:      1,
      terroristCount:    ec.terroristCount,
      placementStrategy: ec.placementStrategy,
      // Fixed defaults — controls removed from the form. Literals (not state)
      // so stale localStorage configs cannot re-inject old values.
      hostageRiskLevel:  'medium',
    },
    executionControls: {
      difficultyLevel: 3,
      randomnessLevel: 'medium',
      seed:            ex.seed === '' || ex.seed == null ? null : Number(ex.seed),
      timeLimit:       null,
      customLabel:     null,
      npcLevel:        ex.npcLevel ?? 'advanced',   // evaluation tier for the terrorists
      playerId:        (ex.playerId ?? '').trim() || null, // evaluation participant code
    },
  })

  // ── Submit ──────────────────────────────────────────────────────────────
  const submit = async (endpoint, label) => {
    if (!serverUrl) {
      setStatus({ kind: 'error', text: 'Set the Quest server URL first.' })
      return
    }
    if (warning) {
      setStatus({ kind: 'error', text: warning })
      return
    }

    setBusy(true)
    setStatus({ kind: 'busy', text: `${label}...` })
    const payload = buildPayload()

    try {
      const res = await fetch(`${serverUrl.replace(/\/$/, '')}${endpoint}`, {
        method:  'POST',
        headers: { 'Content-Type': 'application/json' },
        body:    JSON.stringify(payload),
      })
      const body = await res.json().catch(() => ({}))

      if (!res.ok) {
        const detail = body?.message
          || (Array.isArray(body?.details) ? body.details.join('; ') : null)
          || res.statusText
        setStatus({ kind: 'error', text: `${label} failed: ${detail}` })
        return
      }

      const summary = `scenario ${shortId(body.scenarioId)} - ${body.rooms} rooms, ${body.entities} entities, seed=${body.seedUsed}`
      setStatus({ kind: 'success', text: `${label}: ${summary}` })

      window.localStorage.setItem(STORAGE_KEY_CONFIG, JSON.stringify(payload))
      window.localStorage.setItem(STORAGE_KEY_URL,    serverUrl)
    } catch (err) {
      setStatus({ kind: 'error', text: `${label} failed: ${err.message || 'network error'}` })
    } finally {
      setBusy(false)
    }
  }

  if (!open) return null

  return (
    <div className={styles.overlay} role="dialog" aria-modal="true" aria-labelledby="ml-title">
      <div className={styles.panel} onClick={e => e.stopPropagation()}>
        <header className={styles.head}>
          <h2 id="ml-title" className={styles.title}>Scenario Configuration</h2>
          <div className={styles.headActions}>
            <ThemeToggle />
            <button className={styles.closeBtn} onClick={onClose} aria-label="Close">✕</button>
          </div>
        </header>

        {/* ── Server connection ────────────────────────────────────────── */}
        <section className={styles.connRow}>
          <label className={styles.connLabel}>Quest server</label>
          <input
            className={styles.urlInput}
            value={serverUrl}
            placeholder="http://192.168.1.42:8080"
            onChange={e => setServerUrl(e.target.value)}
          />
          <span className={`${styles.healthPill} ${styles[`health_${health}`]}`}>
            {health === HEALTH_OK ? 'reachable' : health === HEALTH_DOWN ? 'unreachable' : 'checking...'}
          </span>
        </section>

        <div className={styles.scroll}>
          {/* ── Mission Structure ──────────────────────────────────────── */}
          <h3 className={styles.section}>Mission Structure</h3>

          <Row label="Mission Type">
            <Select
              value={ms.missionType}
              onChange={v => setMs({ missionType: v })}
              options={MISSION_TYPES}
            />
          </Row>

          <Row label="Room Count">
            <RangeWithValue
              min={3} max={5} value={ms.roomCount.max}
              onChange={setRoomCount}
            />
          </Row>

          <Row label="Room Size">
            <Select value={ms.roomSize}   onChange={v => setMs({ roomSize:   v })} options={ROOM_SIZES}   />
          </Row>

          <Row label="Layout Type">
            <Select value={ms.layoutType} onChange={v => setMs({ layoutType: v })} options={LAYOUT_TYPES} />
          </Row>

          <Row label="Entry Type">
            <Select value={ms.entryType}  onChange={v => setMs({ entryType:  v })} options={ENTRY_TYPES}  />
          </Row>

          {/* ── Entity Configuration ──────────────────────────────────── */}
          <h3 className={styles.section}>Entity Configuration</h3>

          <Row label="Hostage Count">
            <span className={styles.fixedValue}>1</span>
          </Row>

          <Row label="Terrorist Count">
            <RangeWithValue
              min={1} max={4} value={ec.terroristCount}
              onChange={v => setEc({ terroristCount: Number(v) })}
            />
          </Row>

          <Row label="Placement Strategy">
            <Select value={ec.placementStrategy} onChange={v => setEc({ placementStrategy: v })} options={PLACEMENT_STRATEGIES} />
          </Row>

          {/* ── Execution Controls ────────────────────────────────────── */}
          <h3 className={styles.section}>Execution Controls</h3>

          <Row label="Player ID">
            <input
              className={styles.textInput}
              type="text"
              placeholder="e.g. P03 (groups this player's 3 plays)"
              value={ex.playerId ?? ''}
              onChange={e => setEx({ playerId: e.target.value })}
            />
          </Row>

          <Row label="NPC Level">
            <Select
              value={ex.npcLevel ?? 'advanced'}
              onChange={v => setEx({ npcLevel: v })}
              options={NPC_LEVELS}
            />
          </Row>

          <Row label="Seed (optional)">
            <input
              className={styles.textInput}
              type="number"
              placeholder="empty = random"
              value={ex.seed ?? ''}
              onChange={e => setEx({ seed: e.target.value })}
            />
          </Row>
        </div>

        {/* ── Status + actions ─────────────────────────────────────────── */}
        <footer className={styles.foot}>
          <div className={`${styles.status} ${styles[`status_${status.kind}`]}`}>
            {warning && status.kind === 'idle' ? `Warning: ${warning}` : status.text}
          </div>

          <div className={styles.actions}>
            <button
              className={styles.btnSecondary}
              onClick={() => submit('/scenario/generate', 'Generate')}
              disabled={busy || health !== HEALTH_OK}
            >
              Generate
            </button>
            <button
              className={styles.btnPrimary}
              onClick={() => submit('/scenario/start', 'Start mission')}
              disabled={busy || health !== HEALTH_OK}
            >
              Start Mission
            </button>
          </div>
        </footer>
      </div>
    </div>
  )
}

// ── Local sub-components ────────────────────────────────────────────────

function Row({ label, children }) {
  return (
    <div className={styles.row}>
      <label className={styles.rowLabel}>{label}</label>
      <div className={styles.rowControl}>{children}</div>
    </div>
  )
}

function Select({ value, onChange, options }) {
  return (
    <select
      className={styles.select}
      value={value}
      onChange={e => onChange(e.target.value)}
    >
      {options.map(o => (
        <option key={o.value} value={o.value}>{o.label}</option>
      ))}
    </select>
  )
}

function RangeWithValue({ min, max, value, onChange, renderValue }) {
  return (
    <div className={styles.rangeWrap}>
      <input
        className={styles.range}
        type="range"
        min={min} max={max}
        value={value}
        onChange={e => onChange(e.target.value)}
      />
      <span className={styles.rangeValue}>
        {renderValue ? renderValue(value) : value}
      </span>
    </div>
  )
}

function shortId(id) {
  if (!id) return '?'
  return id.length > 8 ? id.slice(0, 8) : id
}
