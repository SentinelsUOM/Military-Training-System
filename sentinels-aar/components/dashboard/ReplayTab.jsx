'use client'
import { useState, useEffect, useRef, useCallback, useMemo } from 'react'
import {
  ScatterChart, Scatter, XAxis, YAxis, ZAxis, Tooltip, ResponsiveContainer, Cell, LabelList
} from 'recharts'
import dynamic from 'next/dynamic'
import styles from './ReplayTab.module.css'
import { formatTime, getActorStateAt, getMissionPhase, stateColor } from '@/lib/utils'
import { useChartTheme } from '@/lib/useTheme'

// three.js can't server-render (needs the DOM/WebGL), so load the 3D view client-only.
const Replay3D = dynamic(() => import('./Replay3D'), {
  ssr: false,
  loading: () => <div style={{ padding: 24, color: 'var(--muted)' }}>Loading 3D replay…</div>,
})

const SPEEDS = [0.5, 1, 2]

// The trainee (player) is registered in the replay with actorId "trainee_..." and
// state "Trainee", which isn't in stateColor's map, so it fell through to grey.
// Give the player a dedicated blue so they stand out from the NPCs on the map.
const TRAINEE_COLOR = '#1f6feb'
const isTrainee  = (a) => (a?.actorId || '').toLowerCase().startsWith('trainee') || a?.state === 'Trainee'
const actorColor = (a) => (isTrainee(a) ? TRAINEE_COLOR : stateColor(a?.state))

export default function ReplayTab({ session }) {
  const chartTheme = useChartTheme()
  const duration    = session.performance?.missionDuration || 1
  const frames      = session.replayFrames    || []
  const npcChanges  = session.npcStateChanges || []
  const events      = session.events          || []

  const [time, setTime]   = useState(0)
  const [playing, setPlaying] = useState(false)
  const [speed, setSpeed] = useState(1)

  // Fixed map bounds across the WHOLE replay. Without this, recharts auto-scales
  // each axis to only the dots visible in the current frame, so actors appear to
  // teleport as they move and the top-down view can't be read as a map. Computed
  // once from every frame (x, and z mapped to the vertical axis).
  const bounds = useMemo(() => {
    let minX = Infinity, maxX = -Infinity, minY = Infinity, maxY = -Infinity
    for (const f of frames) {
      const x = f.position?.x
      const y = f.position?.z
      // Ignore missing / non-finite / clearly-junk coordinates. A single stray huge
      // value from a mis-captured frame would otherwise blow up the whole axis scale
      // (e.g. an axis that reads in the hundreds of thousands for a room-sized map).
      if (!Number.isFinite(x) || !Number.isFinite(y)) continue
      if (Math.abs(x) > 5000 || Math.abs(y) > 5000) continue
      if (x < minX) minX = x; if (x > maxX) maxX = x
      if (y < minY) minY = y; if (y > maxY) maxY = y
    }
    if (!Number.isFinite(minX)) return null
    const padX = (maxX - minX) * 0.1 || 1
    const padY = (maxY - minY) * 0.1 || 1
    return { x: [minX - padX, maxX + padX], y: [minY - padY, maxY + padY] }
  }, [frames])
  const rafRef   = useRef(null)
  const lastRef  = useRef(null)
  const timeRef  = useRef(0)

  const tick = useCallback((ts) => {
    if (lastRef.current == null) lastRef.current = ts
    const delta = (ts - lastRef.current) / 1000
    lastRef.current = ts
    timeRef.current = Math.min(timeRef.current + delta * speed, duration)
    setTime(timeRef.current)
    if (timeRef.current >= duration) {
      setPlaying(false)
      return
    }
    rafRef.current = requestAnimationFrame(tick)
  }, [speed, duration])

  useEffect(() => {
    if (playing) {
      lastRef.current = null
      rafRef.current = requestAnimationFrame(tick)
    } else {
      if (rafRef.current) cancelAnimationFrame(rafRef.current)
    }
    return () => { if (rafRef.current) cancelAnimationFrame(rafRef.current) }
  }, [playing, tick])

  const handleScrub = (e) => {
    const v = parseFloat(e.target.value)
    timeRef.current = v
    setTime(v)
    lastRef.current = null
  }

  const handleReset = () => {
    setPlaying(false)
    timeRef.current = 0
    setTime(0)
  }

  // Unity sends one ReplayFrame per actor per tick. Group by actorId and
  // pick the most recent frame at or before `time` for each actor. Before an
  // actor's first sample (e.g. at t=0, since the first tick lands ~0.5s in),
  // fall back to its earliest frame so the map is never blank at the start.
  const latestByActor   = new Map()
  const earliestByActor = new Map()
  for (const f of frames) {
    const e = earliestByActor.get(f.actorId)
    if (!e || f.timestamp < e.timestamp) earliestByActor.set(f.actorId, f)
    if (f.timestamp > time) continue
    const existing = latestByActor.get(f.actorId)
    if (!existing || f.timestamp > existing.timestamp) {
      latestByActor.set(f.actorId, f)
    }
  }
  for (const [id, f] of earliestByActor) {
    if (!latestByActor.has(id)) latestByActor.set(id, f)
  }
  const frameActors = [...latestByActor.values()]

  const actorIds = [...new Set([
    ...npcChanges.map(n => n.actorId),
    ...frameActors.map(a => a.actorId),
  ])]

  const scatterData = frameActors.map(a => ({
    x: a.position?.x ?? 0,
    y: a.position?.z ?? 0,
    actorId: a.actorId,
    state: a.currentState || getActorStateAt(npcChanges, a.actorId, time),
  }))

  const activeEvents = events.filter(e => Math.abs(e.timestamp - time) <= 2)
  const phase = getMissionPhase(events, time)

  // Always-on label above each dot, coloured to match the actor's state, so you
  // can read who is who without hovering. Recharts passes x/y (pixel coords of the
  // point) and index (row in scatterData) to a LabelList content renderer.
  const renderActorLabel = ({ x, y, index }) => {
    if (x == null || y == null) return null
    const entry = scatterData[index]
    if (!entry) return null
    return (
      <text
        x={x}
        y={y - 9}
        textAnchor="middle"
        fontSize={9}
        fontWeight={600}
        fill={actorColor(entry)}
        style={{ pointerEvents: 'none' }}
      >
        {entry.actorId}
      </text>
    )
  }

  return (
    <div className={styles.wrap}>
      <div className={styles.controls}>
        <button className={styles.ctrlBtn} onClick={handleReset} title="Reset">⏮</button>
        <button
          className={`${styles.ctrlBtn} ${styles.playBtn}`}
          onClick={() => setPlaying(p => !p)}
        >
          {playing ? '⏸' : '▶'}
        </button>
        <input
          type="range"
          className={styles.scrubber}
          min={0}
          max={duration}
          step={0.1}
          value={time}
          onChange={handleScrub}
        />
        <span className={styles.timeLabel}>
          {formatTime(time)} / {formatTime(duration)}
        </span>
        <div className={styles.speedGroup}>
          {SPEEDS.map(s => (
            <button
              key={s}
              className={`${styles.speedBtn} ${speed === s ? styles.activeSpeed : ''}`}
              onClick={() => setSpeed(s)}
            >
              {s}×
            </button>
          ))}
        </div>
      </div>

      <div className={styles.phaseBar}>
        <span className={styles.phaseLabel}>Phase:</span>
        <span className={styles.phaseValue}>{phase}</span>
      </div>

      <div className={styles.mainGrid}>
        <div className={styles.card}>
          <h3 className={styles.cardTitle}>Positions (top-down)</h3>
          <ResponsiveContainer width="100%" height={320}>
            <ScatterChart margin={{ top: 8, right: 8, left: -20, bottom: 0 }}>
              <XAxis type="number" dataKey="x" name="X" tick={{ fill: chartTheme.label, fontSize: 10 }}
                domain={bounds ? bounds.x : ['auto', 'auto']} allowDataOverflow />
              <YAxis type="number" dataKey="y" name="Z" tick={{ fill: chartTheme.label, fontSize: 10 }}
                domain={bounds ? bounds.y : ['auto', 'auto']} allowDataOverflow />
              <ZAxis range={[60, 60]} />
              <Tooltip
                cursor={{ strokeDasharray: '3 3' }}
                contentStyle={{ background: chartTheme.tooltip, border: `1px solid ${chartTheme.grid}`, borderRadius: 6 }}
                content={({ payload }) => {
                  if (!payload?.length) return null
                  const d = payload[0].payload
                  return (
                    <div style={{ background: chartTheme.tooltip, border: `1px solid ${chartTheme.grid}`, borderRadius: 6, padding: '6px 10px', fontSize: 12 }}>
                      <div style={{ fontWeight: 600 }}>{d.actorId}</div>
                      <div style={{ color: stateColor(d.state) }}>{d.state || '—'}</div>
                      <div style={{ color: chartTheme.label }}>({d.x?.toFixed(1)}, {d.y?.toFixed(1)})</div>
                    </div>
                  )
                }}
              />
              <Scatter data={scatterData} name="Actors" isAnimationActive={false}>
                {scatterData.map((entry, i) => (
                  <Cell key={i} fill={actorColor(entry)} />
                ))}
                <LabelList dataKey="actorId" content={renderActorLabel} />
              </Scatter>
            </ScatterChart>
          </ResponsiveContainer>
          {scatterData.length === 0 && (
            <div className={styles.noData}>No position data for this frame.</div>
          )}
        </div>

        <div className={styles.right}>
          <div className={styles.card}>
            <h3 className={styles.cardTitle}>Actor States</h3>
            <div className={styles.actorBoard}>
              {actorIds.length === 0 ? (
                <span className={styles.muted}>No actors recorded.</span>
              ) : actorIds.map(id => {
                const state = getActorStateAt(npcChanges, id, time) ||
                  frameActors.find(a => a.actorId === id)?.currentState || '—'
                return (
                  <div key={id} className={styles.actorRow}>
                    <span className={styles.actorId}>{id}</span>
                    <span className={styles.actorState} style={{ color: stateColor(state) }}>
                      {state}
                    </span>
                  </div>
                )
              })}
            </div>
          </div>

          <div className={styles.card}>
            <h3 className={styles.cardTitle}>Active Events (±2s)</h3>
            <div className={styles.eventLog}>
              {activeEvents.length === 0 ? (
                <span className={styles.muted}>No events near this timestamp.</span>
              ) : activeEvents.map((ev, i) => (
                <div key={i} className={styles.eventEntry}>
                  <span className={styles.evTime}>{formatTime(ev.timestamp)}</span>
                  <span>{ev.eventType}</span>
                  {ev.sourceActorId && <span className={styles.muted}>{ev.sourceActorId}</span>}
                </div>
              ))}
            </div>
          </div>
        </div>
      </div>

      <div className={styles.card} style={{ marginTop: 16 }}>
        <h3 className={styles.cardTitle}>3D Replay — drag to orbit, scroll to zoom, right-drag to pan</h3>
        <div style={{ width: '100%', height: 440, borderRadius: 8, overflow: 'hidden' }}>
          <Replay3D frames={frames} time={time} layout={session.layout} />
        </div>
      </div>
    </div>
  )
}
