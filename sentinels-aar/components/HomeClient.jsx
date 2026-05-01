'use client'
import { useState, useCallback } from 'react'
import { useRouter } from 'next/navigation'
import MetricCard from '@/components/ui/MetricCard'
import LoadingSpinner from '@/components/ui/LoadingSpinner'
import styles from './HomeClient.module.css'
import { formatDate, formatTime, scoreColor, scorePercent } from '@/lib/utils'

export default function HomeClient({ initialSessions, initialStats, total: initialTotal }) {
  const router = useRouter()
  const [sessions, setSessions] = useState(initialSessions)
  const [stats, setStats] = useState(initialStats)
  const [total, setTotal] = useState(initialTotal)
  const [page, setPage] = useState(1)
  const [loading, setLoading] = useState(false)
  const [seeding, setSeeding] = useState(false)

  const LIMIT = 20

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
        <button
          className={styles.seedBtn}
          onClick={handleSeed}
          disabled={seeding}
        >
          {seeding ? 'Seeding…' : 'Load Demo Data'}
        </button>
      </header>

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
            label="Avg Reaction Time"
            value={stats.averageReactionTime != null ? stats.averageReactionTime.toFixed(2) : '—'}
            unit="s"
            color="var(--muted)"
          />
        </section>
      )}

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
                  <th>Date</th>
                  <th>Duration</th>
                  <th>Overall</th>
                  <th>Safety</th>
                  <th>Accuracy</th>
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
                    <td>{formatDate(s.startTime)}</td>
                    <td>{formatTime(s.missionDuration)}</td>
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
