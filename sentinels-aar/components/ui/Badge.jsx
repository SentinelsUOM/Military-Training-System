import styles from './Badge.module.css'

const VARIANTS = {
  high:    { bg: '#3d0e0e', color: '#f87171' },
  medium:  { bg: '#2d1e00', color: '#fbbf24' },
  low:     { bg: '#0e2d1a', color: '#34d399' },
  info:    { bg: '#0e1e3d', color: '#60a5fa' },
  default: { bg: 'var(--surface)', color: 'var(--muted)' },
}

export default function Badge({ label, variant = 'default' }) {
  const { bg, color } = VARIANTS[variant] || VARIANTS.default
  return (
    <span className={styles.badge} style={{ background: bg, color }}>
      {label}
    </span>
  )
}
