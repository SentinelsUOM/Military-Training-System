import styles from './LoadingSpinner.module.css'

export default function LoadingSpinner({ size = 32, label = 'Loading…' }) {
  return (
    <div className={styles.wrap}>
      <div className={styles.ring} style={{ width: size, height: size }} />
      {label && <span className={styles.label}>{label}</span>}
    </div>
  )
}
