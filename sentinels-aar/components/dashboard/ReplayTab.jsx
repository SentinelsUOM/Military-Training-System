'use client'
import { useState, useEffect, useRef, useCallback } from 'react'
import {
  ScatterChart, Scatter, XAxis, YAxis, ZAxis, Tooltip, ResponsiveContainer, Cell
} from 'recharts'
import styles from './ReplayTab.module.css'
import { formatTime, getActorStateAt, getMissionPhase, stateColor, chartTheme } from '@/lib/utils'

const SPEEDS = [0.5, 1, 2]

export default function ReplayTab({ session }) {
  const duration    = session.missionDuration || 1
  const frames      = session.replayFrames    || []
  const npcChanges  = session.npcStateChanges || []
  const events      = session.events          || []

  const [time, setTime]   = useState(0)
  const [playing, setPlaying] = useState(false)
  const [speed, setSpeed] = useState(1)
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

  const currentFrame = frames.length > 0
    ? frames.reduce((best, f) =>
        f.timestamp <= time && f.timestamp > (best?.timestamp ?? -1) ? f : best
      , null)
    : null

  const frameActors = currentFrame?.actors || []

  const actorIds = [...new Set([
    ...npcChanges.map(n => n.actorId),
    ...frameActors.map(a => a.actorId),
  ])]

  const scatterData = frameActors.map(a => ({
    x: a.position?.x ?? 0,
    y: a.position?.z ?? 0,
    actorId: a.actorId,
    state: a.state || getActorStateAt(npcChanges, a.actorId, time),
  }))

  const activeEvents = events.filter(e => Math.abs(e.timestamp - time) <= 2)
  const phase = getMissionPhase(events, time)

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
              <XAxis type="number" dataKey="x" name="X" tick={{ fill: chartTheme.label, fontSize: 10 }} />
              <YAxis type="number" dataKey="y" name="Z" tick={{ fill: chartTheme.label, fontSize: 10 }} />
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
              <Scatter data={scatterData} name="Actors">
                {scatterData.map((entry, i) => (
                  <Cell key={i} fill={stateColor(entry.state)} />
                ))}
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
                  frameActors.find(a => a.actorId === id)?.state || '—'
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
    </div>
  )
}
