'use client'
import { useMemo, useState } from 'react'
import { useRouter } from 'next/navigation'
import { SIM_TLX_DIMENSIONS, SIM_TLX_SCALE_MAX } from '@/lib/simTlx'
import { formatTime } from '@/lib/utils'
import styles from './SimTlxClient.module.css'

// Sessions the trainee explicitly skipped — the home page reads this to stop
// re-redirecting into the questionnaire for the same session.
export const SIMTLX_DISMISSED_KEY = 'simtlx-dismissed'

export function dismissedSessionIds() {
  try {
    return JSON.parse(localStorage.getItem(SIMTLX_DISMISSED_KEY) || '[]')
  } catch {
    return []
  }
}

function dismissSession(sessionId) {
  const ids = dismissedSessionIds()
  if (!ids.includes(sessionId)) ids.push(sessionId)
  try {
    localStorage.setItem(SIMTLX_DISMISSED_KEY, JSON.stringify(ids.slice(-50)))
  } catch { /* storage unavailable — redirect loop is the worst case, acceptable */ }
}

export default function SimTlxClient({ session }) {
  const router = useRouter()
  const perf = session.performance || {}
  const alreadyDone = Boolean(session.simTlx?.completedAt)

  // step -1 = intro, 0..8 = the nine dimensions, 9 = review/submit
  const [step, setStep] = useState(-1)
  const [ratings, setRatings] = useState({})
  const [touched, setTouched] = useState({})
  const [submitting, setSubmitting] = useState(false)
  const [saved, setSaved] = useState(false)
  const [error, setError] = useState(null)

  const total = SIM_TLX_DIMENSIONS.length
  const answeredCount = useMemo(
    () => SIM_TLX_DIMENSIONS.filter(d => touched[d.key]).length,
    [touched]
  )
  const allAnswered = answeredCount === total

  const setRating = (key, value) => {
    setRatings(r => ({ ...r, [key]: value }))
    setTouched(t => ({ ...t, [key]: true }))
  }

  const handleSubmit = async () => {
    setSubmitting(true)
    setError(null)
    try {
      const res = await fetch(`/api/sessions/${session.sessionId}/simtlx`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ ratings })
      })
      const data = await res.json()
      if (!res.ok || !data.success) throw new Error(data.error || 'Failed to save')
      setSaved(true)
      // Session is saved & closed — hand the trainee back to the AAR.
      setTimeout(() => router.push(`/session/${session.sessionId}`), 1800)
    } catch (err) {
      setError(err.message)
    } finally {
      setSubmitting(false)
    }
  }

  const handleSkip = () => {
    dismissSession(session.sessionId)
    router.push('/')
  }

  if (alreadyDone && !saved) {
    return (
      <div className={styles.page}>
        <div className={styles.card}>
          <h1 className={styles.title}>Workload Assessment already recorded</h1>
          <p className={styles.introText}>
            This session&apos;s workload questionnaire was completed on{' '}
            {new Date(session.simTlx.completedAt).toLocaleString()}.
          </p>
          <div className={styles.navRow}>
            <button className={styles.primaryBtn} onClick={() => router.push(`/session/${session.sessionId}`)}>
              View Session AAR →
            </button>
          </div>
        </div>
      </div>
    )
  }

  if (saved) {
    return (
      <div className={styles.page}>
        <div className={styles.card}>
          <div className={styles.savedIcon}>✓</div>
          <h1 className={styles.title}>Responses saved — session closed</h1>
          <p className={styles.introText}>Redirecting to the After-Action Review…</p>
        </div>
      </div>
    )
  }

  return (
    <div className={styles.page}>
      <div className={styles.card}>
        <header className={styles.header}>
          <div>
            <div className={styles.kicker}>Mission Debrief</div>
            <h1 className={styles.title}>Post-Mission Workload Assessment</h1>
          </div>
          <div className={styles.missionMeta}>
            <div className={styles.metaField}>
              <span className={styles.metaFieldLabel}>Session ID</span>
              <span className={`${styles.metaFieldValue} ${styles.sessionId}`}>{session.sessionId}</span>
            </div>
            <div className={styles.metaField}>
              <span className={styles.metaFieldLabel}>Mission Status</span>
              <span className={`${styles.metaFieldValue} ${perf.missionSuccess ? styles.success : styles.fail}`}>
                {perf.missionSuccess ? 'SUCCESS' : 'FAILED'}
              </span>
            </div>
            {perf.missionDuration != null && (
              <div className={styles.metaField}>
                <span className={styles.metaFieldLabel}>Time Spent</span>
                <span className={`${styles.metaFieldValue} ${styles.duration}`}>{formatTime(perf.missionDuration)}</span>
              </div>
            )}
          </div>
        </header>

        {step === -1 ? (
          <div className={styles.intro}>
            <p className={styles.introText}>
              Before this session is closed, rate the workload you just experienced.
              You will answer <strong>{total} questions</strong> from a validated
              post-mission workload survey (SIM-TLX, Harris et&nbsp;al.&nbsp;2020),
              each on a scale from <strong>Low (0)</strong> to <strong>High (20)</strong>.
            </p>
            <p className={styles.introText}>
              Answer based on the mission you just completed — there are no right or
              wrong answers.
            </p>
            <div className={styles.navRow}>
              <button className={styles.skipBtn} onClick={handleSkip}>Skip for now</button>
              <button className={styles.primaryBtn} onClick={() => setStep(0)}>
                Begin Assessment →
              </button>
            </div>
          </div>
        ) : step < total ? (
          <QuestionStep
            dim={SIM_TLX_DIMENSIONS[step]}
            index={step}
            total={total}
            value={ratings[SIM_TLX_DIMENSIONS[step].key]}
            isTouched={Boolean(touched[SIM_TLX_DIMENSIONS[step].key])}
            onChange={v => setRating(SIM_TLX_DIMENSIONS[step].key, v)}
            onBack={() => setStep(s => s - 1)}
            onNext={() => setStep(s => s + 1)}
          />
        ) : (
          <div className={styles.review}>
            <h2 className={styles.stepLabel}>Review your ratings</h2>
            <div className={styles.reviewList}>
              {SIM_TLX_DIMENSIONS.map((d, i) => (
                <button
                  key={d.key}
                  className={styles.reviewRow}
                  onClick={() => setStep(i)}
                  title="Edit this answer"
                >
                  <span className={styles.reviewLabel}>{d.label}</span>
                  <span className={styles.reviewTrack}>
                    <span
                      className={styles.reviewFill}
                      style={{ width: `${(ratings[d.key] / SIM_TLX_SCALE_MAX) * 100}%` }}
                    />
                  </span>
                  <span className={styles.reviewValue}>{ratings[d.key]} / {SIM_TLX_SCALE_MAX}</span>
                </button>
              ))}
            </div>
            {error && <div className={styles.error}>{error}</div>}
            <div className={styles.navRow}>
              <button className={styles.ghostBtn} onClick={() => setStep(total - 1)}>← Back</button>
              <button
                className={styles.primaryBtn}
                disabled={!allAnswered || submitting}
                onClick={handleSubmit}
              >
                {submitting ? 'Saving…' : 'Submit & Close Session'}
              </button>
            </div>
          </div>
        )}

        {step >= 0 && step < total && (
          <div className={styles.progressWrap}>
            <div className={styles.progressTrack}>
              <div
                className={styles.progressFill}
                style={{ width: `${((step + (touched[SIM_TLX_DIMENSIONS[step].key] ? 1 : 0)) / total) * 100}%` }}
              />
            </div>
            <span className={styles.progressLabel}>
              Question {step + 1} of {total}
            </span>
          </div>
        )}
      </div>
    </div>
  )
}

function QuestionStep({ dim, index, total, value, isTouched, onChange, onBack, onNext }) {
  const display = value ?? Math.floor(SIM_TLX_SCALE_MAX / 2)

  return (
    <div className={styles.question}>
      <h2 className={styles.stepLabel}>{dim.label}</h2>
      <p className={styles.questionText}>{dim.question}</p>
      <p className={styles.hint}>{dim.hint}</p>

      <div className={styles.sliderBlock}>
        <div className={styles.valueReadout} data-touched={isTouched || undefined}>
          {isTouched ? display : '—'}
        </div>
        <input
          type="range"
          min={0}
          max={SIM_TLX_SCALE_MAX}
          step={1}
          value={display}
          onChange={e => onChange(Number(e.target.value))}
          onPointerUp={e => onChange(Number(e.currentTarget.value))}
          onKeyUp={e => onChange(Number(e.currentTarget.value))}
          className={styles.slider}
          aria-label={`${dim.label}: ${dim.question}`}
        />
        <div className={styles.ticks}>
          {Array.from({ length: SIM_TLX_SCALE_MAX + 1 }, (_, i) => (
            <span key={i} className={styles.tick} />
          ))}
        </div>
        <div className={styles.anchors}>
          <span>Low</span>
          <span>High</span>
        </div>
      </div>

      <div className={styles.navRow}>
        <button className={styles.ghostBtn} onClick={onBack} disabled={index === 0}>
          ← Back
        </button>
        <button
          className={styles.primaryBtn}
          onClick={onNext}
          disabled={!isTouched}
          title={isTouched ? undefined : 'Move the slider to record your rating'}
        >
          {index === total - 1 ? 'Review →' : 'Next →'}
        </button>
      </div>
    </div>
  )
}
