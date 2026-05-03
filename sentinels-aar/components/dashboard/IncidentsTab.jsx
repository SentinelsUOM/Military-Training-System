'use client'
import { useState } from 'react'
import Badge from '@/components/ui/Badge'
import styles from './IncidentsTab.module.css'
import { formatTime } from '@/lib/utils'

const SEVERITIES = ['All', 'high', 'medium', 'low']

export default function IncidentsTab({ session }) {
  const incidents = session.incidents || []
  const [filter, setFilter] = useState('All')

  const filtered = filter === 'All'
    ? incidents
    : incidents.filter(i => i.severity === filter)

  const counts = {
    high:   incidents.filter(i => i.severity === 'high').length,
    medium: incidents.filter(i => i.severity === 'medium').length,
    low:    incidents.filter(i => i.severity === 'low').length,
  }

  return (
    <div className={styles.wrap}>
      <div className={styles.statsRow}>
        <div className={styles.statCard} style={{ borderColor: '#f87171' }}>
          <span className={styles.statNum} style={{ color: '#f87171' }}>{counts.high}</span>
          <span className={styles.statLabel}>High</span>
        </div>
        <div className={styles.statCard} style={{ borderColor: '#fbbf24' }}>
          <span className={styles.statNum} style={{ color: '#fbbf24' }}>{counts.medium}</span>
          <span className={styles.statLabel}>Medium</span>
        </div>
        <div className={styles.statCard} style={{ borderColor: '#34d399' }}>
          <span className={styles.statNum} style={{ color: '#34d399' }}>{counts.low}</span>
          <span className={styles.statLabel}>Low</span>
        </div>
        <div className={styles.statCard}>
          <span className={styles.statNum} style={{ color: 'var(--accent)' }}>{incidents.length}</span>
          <span className={styles.statLabel}>Total</span>
        </div>
      </div>

      <div className={styles.filterRow}>
        {SEVERITIES.map(sev => (
          <button
            key={sev}
            className={`${styles.filterBtn} ${filter === sev ? styles.active : ''}`}
            onClick={() => setFilter(sev)}
          >
            {sev === 'All' ? 'All' : sev.charAt(0).toUpperCase() + sev.slice(1)}
            {sev !== 'All' && <span className={styles.cnt}>{counts[sev]}</span>}
          </button>
        ))}
      </div>

      {filtered.length === 0 ? (
        <div className={styles.empty}>No incidents for this filter.</div>
      ) : (
        <div className={styles.list}>
          {filtered.map((inc, i) => (
            <div key={inc.incidentId || i} className={styles.incCard}>
              <div className={styles.incHeader}>
                <Badge label={inc.severity?.toUpperCase() || 'LOW'} variant={inc.severity || 'low'} />
                <span className={styles.incTime}>{formatTime(inc.timestamp)}</span>
                <span className={styles.incType}>{inc.incidentType}</span>
              </div>
              {inc.description && (
                <p className={styles.incDesc}>{inc.description}</p>
              )}
              <div className={styles.incMeta}>
                {inc.involvedActors?.length > 0 && (
                  <span>Actors: {inc.involvedActors.join(', ')}</span>
                )}
                {inc.roomId && <span>Room: {inc.roomId}</span>}
                {inc.incidentId && (
                  <span className={styles.incId}>{inc.incidentId}</span>
                )}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
