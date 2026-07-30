'use client'
import { useState, useCallback, useEffect } from 'react'
import { useRouter } from 'next/navigation'
import MetricCard from '@/components/ui/MetricCard'
import LoadingSpinner from '@/components/ui/LoadingSpinner'
import MissionLauncher from '@/components/MissionLauncher'
import ThemeToggle from '@/components/ui/ThemeToggle'
import SimTlxTrend from '@/components/SimTlxTrend'
import { dismissedSessionIds } from '@/components/simtlx/SimTlxClient'
import styles from './HomeClient.module.css'
import { formatDate, formatTime, scoreColor, scorePercent } from '@/lib/utils'
import { workloadColor } from '@/lib/simTlx'

// How often the dashboard checks whether a just-finished mission is waiting
// for its SIM-TLX questionnaire.
const SIMTLX_POLL_MS = 5000

export default function HomeClient({ initialSessions, initialStats, total: initialTotal }) {
  const router = useRouter()
  const [sessions, setSessions] = useState(initialSessions)
  const [stats, setStats] = useState(initialStats)
  const [total, setTotal] = useState(initialTotal)
  const [page, setPage] = useState(1)
  const [loading, setLoading] = useState(false)
  const [seeding, setSeeding] = useState(false)
  const [launcherOpen, setLauncherOpen] = useState(false)

  const LIMIT = 20

  // Auto-redirect into the combined Post-Mission Survey (workload + enemy-AI) when a
  // mission has just completed (or failed): Unity uploads the session at mission end,
  // and this poll notices the fresh session that is still missing EITHER survey.
  // Sessions the trainee explicitly skipped are excluded via localStorage.
  useEffect(() => {
    let cancelled = false
    const check = async () => {
      if (document.hidden) return
      try {
        const res = await fetch('/api/sessions/pending-survey')
        if (!res.ok) return
        const data = await res.json()
        if (cancelled || !data.sessionId) return
        if (dismissedSessionIds().includes(data.sessionId)) return
        router.push(`/survey/${data.sessionId}`)
      } catch { /* dashboard offline / API hiccup — try again next tick */ }
    }
    check()
    const timer = setInterval(check, SIMTLX_POLL_MS)
    return () => { cancelled = true; clearInterval(timer) }
  }, [router])

  const fetchPage = useCallback(async (p) => {
    setLoading(true)
    try {
      const res = await fetch(`/api/sessions?page=${p}&limit=${LIMIT}`)
      if (res.ok) {
        const data = await res.json()
        setSessions(data.sessions || [])
        setTotal(data.total || 0)
        setPage(p)
      }
    } finally {
      setLoading(false)
    }
  }, [])

  const handleSeed = async () => {
    setSeeding(true)
    try {
      const res = await fetch('/api/sessions/seed', { method: 'POST' })
      if (res.ok) {
        const [sesRes, stRes] = await Promise.all([
          fetch(`/api/sessions?page=1&limit=${LIMIT}`),
          fetch('/api/stats')
        ])
        if (sesRes.ok) {
          const d = await sesRes.json()
          setSessions(d.sessions || [])
          setTotal(d.total || 0)
          setPage(1)
        }
        if (stRes.ok) setStats(await stRes.json())
      }
    } finally {
      setSeeding(false)
    }
  }

  const totalPages = Math.ceil(total / LIMIT)

  return (
    <div className={styles.page}>
      <header className={styles.header}>
        <div>
          <h1 className={styles.title}>SENTINELS AAR</h1>
          <p className={styles.sub}>After-Action Review Dashboard — VR Military Training</p>
        </div>
        <div className={styles.headerActions}>
          <ThemeToggle />
          <button
            className={styles.seedBtn}
            onClick={() => router.push('/evaluation')}
          >
            Evaluation Results
          </button>
          <button
            className={styles.seedBtn}
            onClick={() => setLauncherOpen(true)}
          >
            New Mission
          </button>
          <button
            className={styles.seedBtn}
            onClick={handleSeed}
            disabled={seeding}
          >
            {seeding ? 'Seeding…' : 'Load Demo Data'}
          </button>
        </div>
      </header>

      <MissionLauncher open={launcherOpen} onClose={() => setLauncherOpen(false)} />

      {stats && (
        <section className={styles.statsGrid}>
          <MetricCard
            label="Total Sessions"
            value={stats.totalSessions}
            unit=""
            color="var(--accent)"
          />
          <MetricCard
            label="Avg Overall Score"
            value={scorePercent(stats.averageOverallScore)}
            color={scoreColor(stats.averageOverallScore)}
          />
          <MetricCard
            label="Mission Success Rate"
            value={scorePercent(stats.missionSuccessRate)}
            color={scoreColor(stats.missionSuccessRate)}
          />
          <MetricCard
            label="Avg Safety Score"
            value={scorePercent(stats.averageSafetyScore)}
            color={scoreColor(stats.averageSafetyScore)}
          />
          <MetricCard
            label="Avg Accuracy"
            value={scorePercent(stats.averageAccuracyScore)}
            color={scoreColor(stats.averageAccuracyScore)}
          />
          <MetricCard
            label="Avg Speed Score"
            value={scorePercent(stats.averageSpeedScore)}
            color={scoreColor(stats.averageSpeedScore)}
          />
          {stats.averageOperatorSafetyScore != null && (
            <MetricCard
              label="Avg Operator Safety"
              value={scorePercent(stats.averageOperatorSafetyScore)}
              color={scoreColor(stats.averageOperatorSafetyScore)}
            />
          )}
          <MetricCard
            label="Avg Reaction Time"
            value={stats.averageReactionTime != null ? stats.averageReactionTime.toFixed(2) : '—'}
            unit="s"
            color="var(--muted)"
          />
          <MetricCard
            label="Avg Workload"
            value={stats.averageWorkload != null ? stats.averageWorkload.toFixed(1) : '—'}
            unit={stats.averageWorkload != null ? '/100' : ''}
            color={stats.averageWorkload != null ? workloadColor(stats.averageWorkload) : 'var(--muted)'}
            subtitle={`${stats.simTlxCount || 0} assessment${(stats.simTlxCount || 0) === 1 ? '' : 's'}`}
          />
        </section>
      )}

      <SimTlxTrend sessions={sessions} />

      <section className={styles.tableSection}>
        <div className={styles.tableHeader}>
          <h2 className={styles.tableTitle}>Sessions</h2>
          <span className={styles.totalLabel}>{total} total</span>
        </div>

        {loading ? (
          <LoadingSpinner />
        ) : sessions.length === 0 ? (
          <div className={styles.empty}>
            <p>No sessions found.</p>
            <p className={styles.emptySub}>Click <strong>Load Demo Data</strong> to populate with sample sessions.</p>
          </div>
        ) : (
          <div className={styles.tableWrap}>
            <table className={styles.table}>
              <thead>
                <tr>
                  <th>Session ID</th>
                  <th>Player ID</th>
                  <th>Date</th>
                  <th>Duration</th>
                  <th>Overall</th>
                  <th>Safety</th>
                  <th>Accuracy</th>
                  <th>Workload</th>
                  <th>Mission</th>
                </tr>
              </thead>
              <tbody>
                {sessions.map(s => (
                  <tr
                    key={s.sessionId}
                    className={styles.row}
                    onClick={() => router.push(`/session/${s.sessionId}`)}
                  >
                    <td className={styles.sessionId}>{s.sessionId}</td>
                    <td>{s.playerId || <span style={{ color: 'var(--muted)' }}>—</span>}</td>
                    <td>{formatDate(s.timestamp || s.createdAt)}</td>
                    <td>{formatTime(s.performance?.missionDuration)}</td>
                    <td>
                      <span style={{ color: scoreColor(s.performance?.overallScore) }}>
                        {scorePercent(s.performance?.overallScore)}%
                      </span>
                    </td>
                    <td>
                      <span style={{ color: scoreColor(s.performance?.safetyScore) }}>
                        {scorePercent(s.performance?.safetyScore)}%
                      </span>
                    </td>
                    <td>
                      <span style={{ color: scoreColor(s.performance?.accuracyScore) }}>
                        {scorePercent(s.performance?.accuracyScore)}%
                      </span>
                    </td>
                    <td>
                      {s.simTlx?.derived?.overallWorkload != null ? (
                        <span style={{ color: workloadColor(s.simTlx.derived.overallWorkload) }}>
                          {s.simTlx.derived.overallWorkload.toFixed(0)}
                        </span>
                      ) : (
                        <span style={{ color: 'var(--muted)' }}>—</span>
                      )}
                    </td>
                    <td>
                      <span className={s.performance?.missionSuccess ? styles.success : styles.fail}>
                        {s.performance?.missionSuccess ? 'SUCCESS' : 'FAIL'}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {totalPages > 1 && (
          <div className={styles.pagination}>
            <button
              className={styles.pageBtn}
              disabled={page <= 1 || loading}
              onClick={() => fetchPage(page - 1)}
            >
              ← Prev
            </button>
            <span className={styles.pageInfo}>Page {page} / {totalPages}</span>
            <button
              className={styles.pageBtn}
              disabled={page >= totalPages || loading}
              onClick={() => fetchPage(page + 1)}
            >
              Next →
            </button>
          </div>
        )}
      </section>
    </div>
  )
}
