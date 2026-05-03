'use client'
import { useEffect, useRef } from 'react'
import styles from './ScoreBar.module.css'

export default function ScoreBar({ label, value, color }) {
  const fillRef = useRef(null)
  const pct = Math.round((value || 0) * 100)

  useEffect(() => {
    if (!fillRef.current) return
    const raf = requestAnimationFrame(() => {
      fillRef.current.style.width = `${pct}%`
    })
    return () => cancelAnimationFrame(raf)
  }, [pct])

  return (
    <div className={styles.row}>
      <span className={styles.label}>{label}</span>
      <div className={styles.track}>
        <div
          ref={fillRef}
          className={styles.fill}
          style={{ backgroundColor: color || 'var(--accent)', width: '0%' }}
        />
      </div>
      <span className={styles.pct}>{pct}%</span>
    </div>
  )
}
