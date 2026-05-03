import styles from './MetricCard.module.css'

export default function MetricCard({ label, value, unit = '%', color, subtitle }) {
  return (
    <div className={styles.card}>
      <div className={styles.label}>{label}</div>
      <div className={styles.value} style={{ color: color || 'var(--accent)' }}>
        {value}
        {unit && <span className={styles.unit}>{unit}</span>}
      </div>
      {subtitle && <div className={styles.subtitle}>{subtitle}</div>}
    </div>
  )
}
