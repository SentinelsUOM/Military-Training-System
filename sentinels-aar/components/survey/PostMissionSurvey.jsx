'use client'
import { useState } from 'react'
import { useRouter } from 'next/navigation'
import SimTlxClient, { SIMTLX_DISMISSED_KEY } from '@/components/simtlx/SimTlxClient'
import AiEvalClient from '@/components/aieval/AiEvalClient'

/**
 * Combined post-mission survey (Option A). Runs the two surveys back-to-back with
 * skip/back navigation, then returns the trainee to the session AAR:
 *
 *   Survey 1 — SIM-TLX (workload, Module 3)   → "Save & Continue →" / "Skip to Survey 2 →"
 *   Survey 2 — Enemy-AI evaluation (Module 2)  → "Submit & Close Session" / "← Back to Survey 1"
 *
 * Each survey still saves independently to its own endpoint; this wrapper only
 * decides which one is on screen. The standalone /simtlx/[id] and /aieval/[id]
 * pages are unaffected (they render the same components without these props).
 */
export default function PostMissionSurvey({ session }) {
  const router = useRouter()

  // Always present Survey 1 (workload) first — it's the intended order. The trainee
  // can "Skip to Survey 2 →" if they've already done it.
  const [stage, setStage] = useState('tlx')

  const finish = () => router.push(`/session/${session.sessionId}`)

  // "Skip survey →" from Survey 2: dismiss this session so the home page stops
  // auto-redirecting into the survey, then go to the scenario data summary (AAR).
  const exitToSummary = () => {
    try {
      const ids = JSON.parse(localStorage.getItem(SIMTLX_DISMISSED_KEY) || '[]')
      if (!ids.includes(session.sessionId)) ids.push(session.sessionId)
      localStorage.setItem(SIMTLX_DISMISSED_KEY, JSON.stringify(ids.slice(-50)))
    } catch { /* storage unavailable — worst case the poll re-redirects, acceptable */ }
    router.push(`/session/${session.sessionId}`)
  }

  if (stage === 'tlx') {
    return (
      <SimTlxClient
        session={session}
        submitLabel="Save & Continue →"
        skipLabel="Skip to Survey 2 →"
        onComplete={() => setStage('aieval')}
        onSkip={() => setStage('aieval')}
      />
    )
  }

  return (
    <AiEvalClient
      session={session}
      submitLabel="Submit & Close Session"
      onComplete={finish}
      onSkip={() => setStage('tlx')}
      onExit={exitToSummary}
    />
  )
}
