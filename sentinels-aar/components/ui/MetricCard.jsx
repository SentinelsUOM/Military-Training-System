import styles from './MetricCard.module.css'

const VERDICT_LABEL = {
  within: 'within expert range',
  below: 'below expert range',
  above: 'above expert range',
}

const SEVERITY_CLASS = {
  good: 'benchmarkWithin',
  watch: 'benchmarkOff',
}

/**
 * `benchmark` (optional): { rangeText, verdict, severity, tier } from
 * lib/movementBenchmarks.js's evaluateMovementAgainstExperts(). Omit it and
 * this renders exactly as before. `severity` (not `verdict`) drives color,
 * since for some metrics (reaction time, scanning speed) being outside the
 * range in the favorable direction is a good result, not a warning. Tier-C
 * metrics with no literature backing (severity 'unvalidated') render nothing.
 */
export default function MetricCard({ label, value, unit = '%', color, subtitle, benchmark }) {
  const showBenchmark = benchmark && benchmark.severity !== 'unvalidated'
  return (
    <div className={styles.card}>
      <div className={styles.label}>{label}</div>
      <div className={styles.value} style={{ color: color || 'var(--accent)' }}>
        {value}
        {unit && <span className={styles.unit}>{unit}</span>}
      </div>
      {subtitle && <div className={styles.subtitle}>{subtitle}</div>}
      {showBenchmark && (
        <div className={`${styles.benchmark} ${styles[SEVERITY_CLASS[benchmark.severity]]}`}>
          expert: {benchmark.rangeText} · {VERDICT_LABEL[benchmark.verdict]}
          <sup className={styles.benchmarkTier}>{benchmark.tier}</sup>
        </div>
      )}
    </div>
  )
}
