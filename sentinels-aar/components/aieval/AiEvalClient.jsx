'use client'
import { useMemo, useState } from 'react'
import { useRouter } from 'next/navigation'
import { AI_EVAL_SCALES, AI_EVAL_ITEM_COUNT } from '@/lib/aiEval'
import { formatTime } from '@/lib/utils'
import styles from './AiEvalClient.module.css'

// Sessions the trainee explicitly skipped for the AI-Eval survey.
export const AIEVAL_DISMISSED_KEY = 'aieval-dismissed'

export function aiEvalDismissedIds() {
  try { return JSON.parse(localStorage.getItem(AIEVAL_DISMISSED_KEY) || '[]') }
  catch { return [] }
}
function dismissSession(id) {
  const ids = aiEvalDismissedIds()
  if (!ids.includes(id)) ids.push(id)
  try { localStorage.setItem(AIEVAL_DISMISSED_KEY, JSON.stringify(ids.slice(-50))) } catch {}
}

/**
 * Module-2 enemy-AI evaluation survey (Survey 2).
 * Standalone usage: <AiEvalClient session={s} />  → redirects to the AAR on submit.
 * Embedded usage (Option-A wrapper): pass onComplete()/onSkip()/onBack + submitLabel
 * to control navigation; the wrapper then advances between surveys.
 */
export default function AiEvalClient({ session, onComplete, onSkip, onExit, onBack, submitLabel }) {
  const router = useRouter()
  const perf = session.performance || {}
  const alreadyDone = Boolean(session.aiEval?.completedAt)
  const embedded = typeof onComplete === 'function'

  // step -1 = intro, 0..N-1 = each sub-scale, N = review/submit
  const [step, setStep] = useState(-1)
  const [ratings, setRatings] = useState({})  // { scaleKey: { itemKey: value } }
  const [submitting, setSubmitting] = useState(false)
  const [saved, setSaved] = useState(false)
  const [error, setError] = useState(null)

  const scales = AI_EVAL_SCALES
  const total = scales.length

  const setItem = (scaleKey, itemKey, value) =>
    setRatings(r => ({ ...r, [scaleKey]: { ...(r[scaleKey] || {}), [itemKey]: value } }))

  const scaleComplete = (scale) => {
    const a = ratings[scale.key] || {}
    return scale.items.every(i => typeof a[i.key] === 'number')
  }
  const allComplete = useMemo(() => scales.every(scaleComplete), [ratings]) // eslint-disable-line

  const handleSubmit = async () => {
    setSubmitting(true); setError(null)
    try {
      const res = await fetch(`/api/sessions/${session.sessionId}/aieval`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ ratings })
      })
      const data = await res.json()
      if (!res.ok || !data.success) throw new Error(data.error || 'Failed to save')
      if (embedded) { onComplete(); return }
      setSaved(true)
      setTimeout(() => router.push(`/session/${session.sessionId}`), 1600)
    } catch (err) { setError(err.message) }
    finally { setSubmitting(false) }
  }

  const handleSkip = () => {
    if (typeof onSkip === 'function') { onSkip(); return }
    dismissSession(session.sessionId)
    router.push('/')
  }

  if (alreadyDone && !saved && !embedded) {
    return (
      <div className={styles.page}><div className={styles.card}>
        <h1 className={styles.title}>Enemy-AI evaluation already recorded</h1>
        <p className={styles.introText}>
          Completed on {new Date(session.aiEval.completedAt).toLocaleString()}.
        </p>
        <div className={styles.navRow}>
          <button className={styles.primaryBtn} onClick={() => router.push(`/session/${session.sessionId}`)}>
            View Session AAR →
          </button>
        </div>
      </div></div>
    )
  }

  if (saved) {
    return (
      <div className={styles.page}><div className={styles.card}>
        <div className={styles.savedIcon}>✓</div>
        <h1 className={styles.title}>Responses saved</h1>
        <p className={styles.introText}>Redirecting to the After-Action Review…</p>
      </div></div>
    )
  }

  return (
    <div className={styles.page}>
      <div className={styles.card}>
        <header className={styles.header}>
          <div>
            <div className={styles.kicker}>Mission Debrief</div>
            <h1 className={styles.title}>Enemy-AI Evaluation</h1>
          </div>
          <div className={styles.missionMeta}>
            <div className={styles.metaField}>
              <span className={styles.metaFieldLabel}>Session</span>
              <span className={`${styles.metaFieldValue} ${styles.sessionId}`}>{session.sessionId}</span>
            </div>
            {session.npcLevel && (
              <div className={styles.metaField}>
                <span className={styles.metaFieldLabel}>AI Level</span>
                <span className={styles.metaFieldValue}>{String(session.npcLevel).toUpperCase()}</span>
              </div>
            )}
            {perf.missionDuration != null && (
              <div className={styles.metaField}>
                <span className={styles.metaFieldLabel}>Time</span>
                <span className={styles.metaFieldValue}>{formatTime(perf.missionDuration)}</span>
              </div>
            )}
          </div>
        </header>

        {step === -1 ? (
          <div className={styles.intro}>
            <p className={styles.introText}>
              Rate the <strong>enemy AI</strong> you just faced. You&apos;ll answer{' '}
              <strong>{AI_EVAL_ITEM_COUNT} quick questions</strong> across a few groups
              (experience, how intelligent the enemies felt, how life-like, and realism).
              There are no right or wrong answers — go by your impression of the mission
              you just played.
            </p>
            <div className={styles.navRow}>
              <button className={styles.skipBtn} onClick={handleSkip}>
                {embedded ? '← Back to Survey 1' : 'Skip for now'}
              </button>
              <div className={styles.navRight}>
                {typeof onExit === 'function' && (
                  <button className={styles.ghostBtn} onClick={onExit}>Skip survey →</button>
                )}
                <button className={styles.primaryBtn} onClick={() => setStep(0)}>Begin →</button>
              </div>
            </div>
          </div>
        ) : step < total ? (
          <ScaleStep
            scale={scales[step]}
            index={step}
            total={total}
            answers={ratings[scales[step].key] || {}}
            onSet={(itemKey, v) => setItem(scales[step].key, itemKey, v)}
            complete={scaleComplete(scales[step])}
            onBack={() => (step === 0 ? setStep(-1) : setStep(s => s - 1))}
            onNext={() => setStep(s => s + 1)}
          />
        ) : (
          <div className={styles.review}>
            <h2 className={styles.stepLabel}>Review</h2>
            <div className={styles.reviewList}>
              {scales.map((s, i) => (
                <button key={s.key} className={styles.reviewRow} onClick={() => setStep(i)} title="Edit">
                  <span className={styles.reviewLabel}>{s.label}</span>
                  <span className={styles.reviewValue}>{scaleComplete(s) ? '✓ done' : 'incomplete'}</span>
                </button>
              ))}
            </div>
            {error && <div className={styles.error}>{error}</div>}
            <div className={styles.navRow}>
              <button className={styles.ghostBtn} onClick={() => setStep(total - 1)}>← Back</button>
              <button className={styles.primaryBtn} disabled={!allComplete || submitting} onClick={handleSubmit}>
                {submitting ? 'Saving…' : (submitLabel || 'Submit')}
              </button>
            </div>
          </div>
        )}

        {step >= 0 && step < total && (
          <div className={styles.progressWrap}>
            <div className={styles.progressTrack}>
              <div className={styles.progressFill} style={{ width: `${((step + 1) / total) * 100}%` }} />
            </div>
            <span className={styles.progressLabel}>Group {step + 1} of {total}</span>
          </div>
        )}
      </div>
    </div>
  )
}

function ScaleStep({ scale, index, total, answers, onSet, complete, onBack, onNext }) {
  const points = Array.from({ length: scale.max - scale.min + 1 }, (_, i) => scale.min + i)
  return (
    <div className={styles.question}>
      <h2 className={styles.stepLabel}>{scale.label}</h2>
      <p className={styles.questionText}>{scale.prompt}</p>

      <div className={styles.itemList}>
        {scale.items.map(item => (
          <div key={item.key} className={styles.itemRow}>
            {scale.type === 'pair' ? (
              <>
                <span className={`${styles.anchor} ${styles.anchorLeft}`}>{item.left}</span>
                <span className={styles.scaleRadios}>
                  {points.map(p => (
                    <label key={p} className={styles.radioDot} title={String(p)}>
                      <input
                        type="radio"
                        name={`${scale.key}.${item.key}`}
                        checked={answers[item.key] === p}
                        onChange={() => onSet(item.key, p)}
                      />
                      <span className={styles.dot} />
                    </label>
                  ))}
                </span>
                <span className={`${styles.anchor} ${styles.anchorRight}`}>{item.right}</span>
              </>
            ) : (
              <>
                <span className={styles.statement}>{item.statement}</span>
                <span className={styles.scaleRadios}>
                  {points.map(p => (
                    <label key={p} className={styles.radioNum} title={String(p)}>
                      <input
                        type="radio"
                        name={`${scale.key}.${item.key}`}
                        checked={answers[item.key] === p}
                        onChange={() => onSet(item.key, p)}
                      />
                      <span className={styles.num}>{p}</span>
                    </label>
                  ))}
                </span>
              </>
            )}
          </div>
        ))}
        {scale.type === 'likert' && (
          <div className={styles.likertLegend}><span>1 = strongly disagree</span><span>5 = strongly agree</span></div>
        )}
      </div>

      <div className={styles.navRow}>
        <button className={styles.ghostBtn} onClick={onBack}>← Back</button>
        <button
          className={styles.primaryBtn}
          onClick={onNext}
          disabled={!complete}
          title={complete ? undefined : 'Answer every row to continue'}
        >
          {index === total - 1 ? 'Review →' : 'Next →'}
        </button>
      </div>
    </div>
  )
}
