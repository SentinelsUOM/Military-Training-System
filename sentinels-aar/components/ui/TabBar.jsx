'use client'
import styles from './TabBar.module.css'

export default function TabBar({ tabs, active, onChange }) {
  return (
    <div className={styles.bar}>
      {tabs.map(tab => (
        <button
          key={tab.id}
          className={`${styles.tab} ${active === tab.id ? styles.active : ''}`}
          onClick={() => onChange(tab.id)}
        >
          {tab.label}
          {tab.count != null && (
            <span className={styles.count}>{tab.count}</span>
          )}
        </button>
      ))}
    </div>
  )
}
