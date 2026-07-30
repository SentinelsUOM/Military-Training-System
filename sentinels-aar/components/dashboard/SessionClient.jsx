'use client'
import { useState } from 'react'
import { useRouter } from 'next/navigation'
import TabBar from '@/components/ui/TabBar'
import ThemeToggle from '@/components/ui/ThemeToggle'
import SummaryTab from './SummaryTab'
import TimelineTab from './TimelineTab'
import IncidentsTab from './IncidentsTab'
import HostageTab from './HostageTab'
import ReplayTab from './ReplayTab'
import MovementTab from './MovementTab'
import SimTlxTab from './SimTlxTab'
import styles from './SessionClient.module.css'
import { formatDate, formatTime, scoreColor, scorePercent } from '@/lib/utils'

const TABS = [
  { id: 'summary',   label: 'Summary' },
  { id: 'timeline',  label: 'Timeline' },
  { id: 'incidents', label: 'Incidents' },
  { id: 'hostage',   label: 'Hostage' },
  { id: 'movement',  label: 'Movement' },
  { id: 'simtlx',    label: 'Workload' },
  { id: 'replay',    label: 'Replay' },
]

export default function SessionClient({ session }) {
  const router = useRouter()
  const [activeTab, setActiveTab] = useState('summary')
  const [deleting, setDeleting] = useState(false)

  const s = session
  const perf = s.performance || {}

  const handleDelete = async () => {
    if (!confirm(`Delete session ${s.sessionId}?`)) return
    setDeleting(true)
    try {
      const res = await fetch(`/api/sessions/${s.sessionId}`, { method: 'DELETE' })
      if (res.ok) router.push('/')
    } finally {
      setDeleting(false)
    }
  }

  const tabsWithCount = TABS.map(t => {
    if (t.id === 'incidents') return { ...t, count: s.incidents?.length || 0 }
    if (t.id === 'timeline')  return { ...t, count: s.events?.length || 0 }
    if (t.id === 'movement')  return { ...t, count: s.movementTrack?.samples?.length || 0 }
    return t
  })

  return (
    <div className={styles.page}>
      <div className={styles.stickyHeader}>
        <div className={styles.headerTop}>
          <div className={styles.breadcrumb}>
            <button className={styles.back} onClick={() => router.push('/')}>← Sessions</button>
            <span className={styles.sep}>/</span>
            <span className={styles.sessionId}>{s.sessionId}</span>
          </div>
          <div className={styles.headerRight}>
            <ThemeToggle />
            <button
              className={styles.deleteBtn}
              onClick={handleDelete}
              disabled={deleting}
            >
              {deleting ? 'Deleting…' : 'Delete'}
            </button>
          </div>
        </div>

        <div className={styles.meta}>
          <div className={styles.metaBlock}>
            <span className={styles.metaLabel}>Date</span>
            <span>{formatDate(s.timestamp || s.createdAt)}</span>
          </div>
          <div className={styles.metaBlock}>
            <span className={styles.metaLabel}>Duration</span>
            <span>{formatTime(perf.missionDuration)}</span>
          </div>
          <div className={styles.metaBlock}>
            <span className={styles.metaLabel}>Scenario</span>
            <span className={styles.mono}>{s.scenarioId}</span>
          </div>
          <div className={styles.metaBlock}>
            <span className={styles.metaLabel}>Overall</span>
            <span style={{ color: scoreColor(perf.overallScore), fontWeight: 600 }}>
              {scorePercent(perf.overallScore)}%
            </span>
          </div>
          <div className={styles.metaBlock}>
            <span className={styles.metaLabel}>Mission</span>
            <span className={perf.missionSuccess ? styles.success : styles.fail}>
              {perf.missionSuccess ? 'SUCCESS' : 'FAIL'}
            </span>
          </div>
        </div>

        <TabBar tabs={tabsWithCount} active={activeTab} onChange={setActiveTab} />
      </div>

      <div className={styles.content}>
        {activeTab === 'summary'   && <SummaryTab session={s} />}
        {activeTab === 'timeline'  && <TimelineTab session={s} />}
        {activeTab === 'incidents' && <IncidentsTab session={s} />}
        {activeTab === 'hostage'   && <HostageTab session={s} />}
        {activeTab === 'movement'  && <MovementTab session={s} />}
        {activeTab === 'simtlx'    && <SimTlxTab session={s} />}
        {activeTab === 'replay'    && <ReplayTab session={s} />}
      </div>
    </div>
  )
}
