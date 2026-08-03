'use client'
import Link from 'next/link'
import {
  RadarChart, Radar, PolarGrid, PolarAngleAxis, PolarRadiusAxis,
  BarChart, Bar, XAxis, YAxis, Tooltip, ResponsiveContainer, Cell
} from 'recharts'
import ScoreBar from '@/components/ui/ScoreBar'
import styles from './SummaryTab.module.css'
import { scoreColor, scorePercent } from '@/lib/utils'
import { useChartTheme } from '@/lib/useTheme'
import { deriveCognitiveScores } from '@/lib/cognitiveDerived'
import { benchmarkPos, hitsByDistanceBand } from '@/lib/expertBenchmarks'
import { hostageBreakdown, accuracyBreakdown, speedBreakdown } from '@/lib/scoreBreakdowns'

export default function SummaryTab({ session }) {
  const chartTheme = useChartTheme()
  const perf = session.performance || {}
  const cog  = session.cognitiveSummary || {}
  const cfg  = session.scenarioConfig || {}
  const events = session.events || []

  // Derive terrorist kill count from events (no dedicated field in PerformanceSummary)
  const terroristsDown = events.filter(e => e.eventType === 'TerroristDown').length

  // Real sessions never populate stabilityScore/attentionScore (Module 3's feed
  // isn't wired up during live play — see lib/cognitiveDerived.js), so fall back
  // to proxies computed from movement/reaction telemetry when that happens.
  const cogScores = deriveCognitiveScores(session)
  const isEstimated = cogScores.source === 'derived'

  // Full sub-parameter breakdowns (like Operator Safety) for the three outcome scores.
  // Each is decomposed into research-grounded components + an expert benchmark. Computed
  // client-side from session data — see lib/scoreBreakdowns.js.
  const hostageBox  = hostageBreakdown(session)
  const accuracyBox = accuracyBreakdown(session)
  const speedBox    = speedBreakdown(session)

  // Where a trainee's hits landed by NYPD SOP-9 distance band, with the expert/novice
  // hit-rate context at each range — see lib/expertBenchmarks.js for why this is a hit
  // distribution, not a computed rate per band (only hits carry a recorded distance).
  const distanceBands = hitsByDistanceBand(session)

  // Operator (player) safety is only present on sessions recorded after the feature shipped.
  const hasOp = perf.operatorSafetyScore != null

  const radarData = [
    { axis: 'Overall',  value: Math.round((perf.overallScore  || 0) * 100) },
    { axis: 'Hostage',  value: Math.round((perf.safetyScore   || 0) * 100) },
    { axis: 'Accuracy', value: Math.round((perf.accuracyScore || 0) * 100) },
    { axis: 'Speed',    value: Math.round((perf.speedScore    || 0) * 100) },
    ...(hasOp ? [{ axis: 'Operator', value: Math.round((perf.operatorSafetyScore || 0) * 100) }] : []),
  ]

  const cogBarData = [
    { name: 'Avg RT (s)',    value: parseFloat((cog.averageReactionTime || 0).toFixed(2)) },
    { name: 'Peak RT (s)',   value: parseFloat((cog.peakReactionTime    || 0).toFixed(2)) },
    { name: 'Stability',     value: parseFloat((cogScores.stabilityScore  || 0).toFixed(2)) },
    { name: 'Attention',     value: parseFloat((cogScores.attentionScore  || 0).toFixed(2)) },
  ]

  return (
    <div className={styles.wrap}>
      <div className={styles.topGrid}>
        <div className={styles.card}>
          <h3 className={styles.cardTitle}>Performance Radar</h3>
          <ResponsiveContainer width="100%" height={260}>
            <RadarChart data={radarData}>
              <PolarGrid stroke={chartTheme.grid} />
              <PolarAngleAxis dataKey="axis" tick={{ fill: chartTheme.label, fontSize: 12 }} />
              <PolarRadiusAxis domain={[0, 100]} tick={false} axisLine={false} />
              <Radar
                dataKey="value"
                stroke={chartTheme.accent}
                fill={chartTheme.accent}
                fillOpacity={0.25}
                dot={{ r: 3, fill: chartTheme.accent }}
              />
            </RadarChart>
          </ResponsiveContainer>
        </div>

        <div className={styles.card}>
          <h3 className={styles.cardTitle}>Score Breakdown</h3>
          <div className={styles.scoreBars}>
            <ScoreBar label="Overall"        value={perf.overallScore}  color={scoreColor(perf.overallScore)} />
            <ScoreBar label="Hostage Safety" value={perf.safetyScore}   color={scoreColor(perf.safetyScore)} />
            {hasOp && (
              <ScoreBar label="Operator Safety" value={perf.operatorSafetyScore} color={scoreColor(perf.operatorSafetyScore)} />
            )}
            <ScoreBar label="Accuracy"       value={perf.accuracyScore} color={scoreColor(perf.accuracyScore)} />
            <ScoreBar label="Speed"          value={perf.speedScore}    color={scoreColor(perf.speedScore)} />
          </div>

          <div className={styles.combatGrid}>
            <CombatStat label="Shots Fired"      value={perf.totalShots ?? 0} />
            <CombatStat label="Shots Hit"         value={perf.hits ?? 0} />
            <CombatStat label="Friendly Fire"     value={perf.friendlyFireCount ?? 0} warn={perf.friendlyFireCount > 0} />
            <CombatStat label="Terrorists Down"   value={terroristsDown} />
            <CombatStat label="Hostages Saved"    value={`${perf.hostagesSaved ?? 0} / ${perf.hostagesTotal ?? 0}`} />
            <CombatStat label="Mission"
              value={perf.missionSuccess ? 'SUCCESS' : 'FAIL'}
              color={perf.missionSuccess ? '#34d399' : '#f87171'}
            />
          </div>
        </div>
      </div>

      <ScoreBox {...hostageBox} title="Hostage Safety — was the hostage protected?" />

      {hasOp && (
        <div className={styles.card} style={{ marginBottom: 16 }}>
          <h3 className={styles.cardTitle}>Operator Safety — how safely you conducted yourself</h3>
          <p style={{ color: 'var(--muted)', fontSize: 13, lineHeight: 1.5, margin: '2px 0 12px' }}>
            Different from <strong>Hostage Safety</strong> (was the hostage protected). This measures whether
            <strong> you</strong> stayed safe: did you survive, keep out of the enemy's line of sight, handle your
            weapon safely, and react quickly. You can rescue the hostage and still score low here — a sign you'd
            have been hit in reality.
          </p>
          <div className={styles.scoreBars}>
            <ScoreBar label="Operator Safety (overall)" value={perf.operatorSafetyScore} color={scoreColor(perf.operatorSafetyScore)} />
            <ScoreBar label="Survivability — health kept"        value={perf.opSurvivability}    color={scoreColor(perf.opSurvivability)} />
            <ScoreBar label="Exposure Control — time unseen"     value={perf.opExposureControl}  color={scoreColor(perf.opExposureControl)} />
            <ScoreBar
              label={`Weapon Discipline — friendly fire + negligent discharge${perf.opNegligentDischargeRate != null ? `  ·  ${Math.round(perf.opNegligentDischargeRate * 100)}% wild shots` : ''}`}
              value={perf.opWeaponDiscipline} color={scoreColor(perf.opWeaponDiscipline)} />
            <ScoreBar label="Threat Response — reaction speed"   value={perf.opThreatResponse}   color={scoreColor(perf.opThreatResponse)} />
          </div>
          <div className={styles.combatGrid} style={{ marginTop: 12 }}>
            <CombatStat label="Final Health" value={perf.opFinalHealth != null && perf.opFinalHealth >= 0 ? `${perf.opFinalHealth} HP` : '—'}
              warn={perf.opFinalHealth != null && perf.opFinalHealth <= 30} />
            <CombatStat label="Time Exposed" value={perf.opExposedSeconds != null ? `${perf.opExposedSeconds.toFixed(0)} s` : '—'} />
            <CombatStat label="Negligent Discharges"
              value={perf.opNegligentDischarges != null ? perf.opNegligentDischarges : '—'}
              warn={perf.opNegligentDischarges > 0} />
          </div>
          <p style={{ fontSize: 11, color: 'var(--muted)', fontStyle: 'italic', margin: '8px 0 0', lineHeight: 1.4 }}>
            Negligent discharge = a round fired with no terrorist visible at all. Benchmark: expert ≈17% of shots, novice ≈61% (qualification-failure studies) — so some wild shots under stress is normal.
          </p>
        </div>
      )}

      <ScoreBox {...accuracyBox} title="Accuracy — how well you shot" distanceBands={distanceBands} />

      <ScoreBox {...speedBox} title="Speed — how quickly you worked" />

      <div className={styles.botGrid}>
        <div className={styles.card}>
          <div className={styles.cardHeaderRow}>
            <h3 className={styles.cardTitle}>Cognitive Metrics</h3>
            {isEstimated && (
              <span
                className={styles.estimateNote}
                title="Module 3's live cognitive feed isn't connected during gameplay, so Stability and Attention are estimated from movement and reaction-time telemetry recorded for this session."
              >
                Stability / Attention estimated
              </span>
            )}
          </div>
          <div className={styles.cogRow}>
            <CogStat label="Load Level"      value={cog.estimatedCognitiveLoad} />
            <CogStat label="Stress Level"    value={cog.estimatedStressLevel} />
            <CogStat label="Avg RT"          value={cog.averageReactionTime != null ? `${cog.averageReactionTime.toFixed(2)}s` : '—'} />
            <CogStat
              label="Stability"
              value={cogScores.stabilityScore != null ? `${(cogScores.stabilityScore * 100).toFixed(0)}%` : '—'}
              estimated={isEstimated}
            />
            <CogStat
              label="Attention"
              value={cogScores.attentionScore != null ? `${(cogScores.attentionScore * 100).toFixed(0)}%` : '—'}
              estimated={isEstimated}
            />
          </div>
          <ResponsiveContainer width="100%" height={160}>
            <BarChart data={cogBarData} margin={{ top: 4, right: 8, left: -20, bottom: 0 }}>
              <XAxis dataKey="name" tick={{ fill: chartTheme.label, fontSize: 11 }} />
              <YAxis tick={{ fill: chartTheme.label, fontSize: 11 }} />
              <Tooltip
                contentStyle={{ background: chartTheme.tooltip, border: `1px solid ${chartTheme.grid}`, borderRadius: 6 }}
                labelStyle={{ color: chartTheme.label }}
              />
              <Bar dataKey="value" radius={[3, 3, 0, 0]}>
                {cogBarData.map((_, i) => (
                  <Cell key={i} fill={chartTheme.colors[i % chartTheme.colors.length]} />
                ))}
              </Bar>
            </BarChart>
          </ResponsiveContainer>
        </div>

        <div className={styles.card}>
          <h3 className={styles.cardTitle}>Scenario Configuration</h3>
          <div className={styles.cfgTable}>
            {Object.entries(cfg).map(([k, v]) => (
              <div key={k} className={styles.cfgRow}>
                <span className={styles.cfgKey}>{k}</span>
                <span className={styles.cfgVal}>
                  {typeof v === 'object' ? JSON.stringify(v) : String(v)}
                </span>
              </div>
            ))}
            {Object.keys(cfg).length === 0 && (
              <span className={styles.cfgEmpty}>No scenario config recorded.</span>
            )}
          </div>

          <div className={styles.cfgActions}>
            {session.aiEval?.completedAt ? (
              <Link href={`/aieval/${session.sessionId}`} className={styles.aiEvalViewLink}>
                <span className={styles.aiEvalDoneBadge}>✓</span> Module 2 questionnaire completed — view responses →
              </Link>
            ) : (
              <Link href={`/aieval/${session.sessionId}`} className={styles.aiEvalFillBtn}>
                Complete Module 2 Questionnaire →
              </Link>
            )}
          </div>
        </div>
      </div>
    </div>
  )
}

// A full score box mirroring the Operator Safety box: title + explainer, the overall score
// and its sub-parameters as bars, then a real-world expert comparison at the bottom.
function ScoreBox({ title, subtitle, overall, overallLabel, subs = [], expert, emptyNote, expertNote, distanceBands }) {
  const shown = subs.filter(s => s.value != null)
  return (
    <div className={styles.card} style={{ marginBottom: 16 }}>
      <h3 className={styles.cardTitle}>{title}</h3>
      {subtitle && (
        <p style={{ color: 'var(--muted)', fontSize: 13, lineHeight: 1.5, margin: '2px 0 12px' }}>{subtitle}</p>
      )}

      <div className={styles.scoreBars}>
        <ScoreBar label={overallLabel} value={overall ?? 0} color={scoreColor(overall ?? 0)} />
        {shown.map(s => (
          <ScoreBar
            key={s.label}
            label={s.hint ? `${s.label}  ·  ${s.hint}` : s.label}
            value={s.value}
            color={scoreColor(s.value)}
          />
        ))}
      </div>

      {emptyNote && (
        <p style={{ fontSize: 12, color: 'var(--muted)', fontStyle: 'italic', margin: '10px 0 0' }}>{emptyNote}</p>
      )}

      {distanceBands && <DistanceBandTable data={distanceBands} />}

      {expert && <ExpertCompare {...expert} />}

      {expertNote && (
        <p style={{ fontSize: 11.5, color: 'var(--muted)', fontStyle: 'italic', margin: '12px 0 0', lineHeight: 1.4,
          paddingTop: 10, borderTop: '1px solid var(--border)' }}>
          {expertNote}
        </p>
      )}
    </div>
  )
}

// Where a trainee's hits landed by NYPD SOP-9 distance band, with the expert/novice hit-rate
// at each range shown for context. NOT a hit-rate computed per band — only hits carry a
// recorded distance (a miss never reaches NPCHitBox), so this is a hit distribution, and
// says so plainly rather than implying a rate this session's data can't actually support.
function DistanceBandTable({ data }) {
  const { bands, exact, totalHits } = data
  if (!bands.length) {
    return (
      <div style={{ marginTop: 14, paddingTop: 12, borderTop: '1px solid var(--border)' }}>
        <div style={{ fontSize: 11, textTransform: 'uppercase', letterSpacing: '0.06em', color: 'var(--muted)', marginBottom: 4 }}>
          Hits by engagement range
        </div>
        <div style={{ fontSize: 12, color: 'var(--muted)', fontStyle: 'italic' }}>
          {totalHits === 0 ? 'No hits this session — nothing to break down by range.' : 'No per-hit distance recorded for this session.'}
        </div>
      </div>
    )
  }

  return (
    <div style={{ marginTop: 14, paddingTop: 12, borderTop: '1px solid var(--border)' }}>
      <div style={{ fontSize: 11, textTransform: 'uppercase', letterSpacing: '0.06em', color: 'var(--muted)', marginBottom: 6 }}>
        Hits by engagement range
      </div>
      <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12.5 }}>
        <thead>
          <tr>
            <th style={{ textAlign: 'left', fontWeight: 600, color: 'var(--muted)', fontSize: 11, paddingBottom: 5 }}>Range</th>
            <th style={{ textAlign: 'right', fontWeight: 600, color: 'var(--muted)', fontSize: 11, paddingBottom: 5 }}>Your hits</th>
            <th style={{ textAlign: 'right', fontWeight: 600, color: 'var(--muted)', fontSize: 11, paddingBottom: 5 }}>Expert rate here</th>
            <th style={{ textAlign: 'right', fontWeight: 600, color: 'var(--muted)', fontSize: 11, paddingBottom: 5 }}>Novice rate here</th>
          </tr>
        </thead>
        <tbody>
          {bands.map(b => (
            <tr key={b.label}>
              <td style={{ padding: '3px 0', color: 'var(--text)' }}>{b.label}</td>
              <td style={{ padding: '3px 0', textAlign: 'right', color: 'var(--text)', fontVariantNumeric: 'tabular-nums' }}>
                {b.count} <span style={{ color: 'var(--muted)' }}>({Math.round(b.pct * 100)}%)</span>
              </td>
              <td style={{ padding: '3px 0', textAlign: 'right', color: '#34d399', fontVariantNumeric: 'tabular-nums' }}>
                {Math.round(b.expert * 100)}%
              </td>
              <td style={{ padding: '3px 0', textAlign: 'right', color: '#f87171', fontVariantNumeric: 'tabular-nums' }}>
                {Math.round(b.novice * 100)}%
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      <div style={{ fontSize: 10.5, color: 'var(--muted)', marginTop: 6, lineHeight: 1.4, opacity: 0.85 }}>
        {exact ? 'Measured' : 'Estimated from replay'} distance per hit, {totalHits} hit{totalHits === 1 ? '' : 's'} total.
        Shows WHERE this session's hits landed by range — a miss doesn't record a distance, so this
        isn't a hit-rate per band, just where the successful shots came from, next to how hard a real
        officer finds each range (NYPD SOP-9).
      </div>
    </div>
  )
}

// The "vs real-world expert" footer of a score box: trainee value on a novice→expert gradient,
// with a verdict (Expert / Proficient / Developing / Novice) and the cited source.
function ExpertCompare({ label, value, valueText, b, note }) {
  const pos = benchmarkPos(value, b)
  return (
    <div style={{ marginTop: 14, paddingTop: 12, borderTop: '1px solid var(--border)' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline', marginBottom: 6 }}>
        <span style={{ fontSize: 12, color: 'var(--muted)' }}>Compared to real-world expert — {label}</span>
        {pos && (
          <span style={{ fontSize: 10.5, fontWeight: 700, color: pos.color, border: `1px solid ${pos.color}`, borderRadius: 4, padding: '1px 8px' }}>
            {pos.verdict}
          </span>
        )}
      </div>

      <div style={{ display: 'flex', alignItems: 'baseline', gap: 8, marginBottom: 6 }}>
        <span style={{ fontSize: 22, fontWeight: 700, color: pos ? pos.color : 'var(--text)' }}>{valueText}</span>
        <span style={{ fontSize: 12, color: 'var(--muted)' }}>you</span>
      </div>

      <div style={{ position: 'relative', height: 7, borderRadius: 4, background: 'linear-gradient(90deg,#f87171,#fbbf24,#34d399)', opacity: 0.45 }}>
        {pos && (
          <div style={{
            position: 'absolute', top: -3, left: `calc(${(pos.pct * 100).toFixed(0)}% - 2px)`,
            width: 4, height: 13, borderRadius: 2, background: pos.color, boxShadow: '0 0 0 2px var(--bg)',
          }} />
        )}
      </div>
      <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10.5, color: 'var(--muted)', marginTop: 4 }}>
        <span>Novice {b.fmt(b.novice)}</span>
        <span>Expert {b.fmt(b.expert)}</span>
      </div>
      <div style={{ fontSize: 10.5, color: 'var(--muted)', marginTop: 2, opacity: 0.7 }}>{b.source}</div>

      {note && <div style={{ fontSize: 11, color: 'var(--muted)', marginTop: 7, fontStyle: 'italic', lineHeight: 1.45 }}>{note}</div>}
    </div>
  )
}

function CombatStat({ label, value, warn, color }) {
  return (
    <div>
      <div style={{ fontSize: 10, color: 'var(--muted)', textTransform: 'uppercase', letterSpacing: '0.06em', marginBottom: 2 }}>{label}</div>
      <div style={{ fontSize: 18, fontWeight: 600, color: warn ? '#f87171' : (color || 'var(--text)') }}>{value}</div>
    </div>
  )
}

function CogStat({ label, value, estimated }) {
  return (
    <div>
      <div style={{ fontSize: 10, color: 'var(--muted)', textTransform: 'uppercase', letterSpacing: '0.06em', marginBottom: 2 }}>{label}</div>
      <div style={{ fontSize: 14, fontWeight: 500, color: 'var(--text)' }}>
        {value ?? '—'}
        {estimated && value !== '—' && (
          <span style={{ fontSize: 10, color: 'var(--muted)', fontWeight: 400, marginLeft: 4 }}>est.</span>
        )}
      </div>
    </div>
  )
}
