'use client'
import {
  RadarChart, Radar, PolarGrid, PolarAngleAxis, PolarRadiusAxis,
  BarChart, Bar, XAxis, YAxis, Tooltip, ResponsiveContainer, Cell
} from 'recharts'
import ScoreBar from '@/components/ui/ScoreBar'
import styles from './SummaryTab.module.css'
import { scoreColor, scorePercent, chartTheme } from '@/lib/utils'
import { deriveCognitiveScores } from '@/lib/cognitiveDerived'

export default function SummaryTab({ session }) {
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
            <ScoreBar label="Weapon Discipline — no friendly fire" value={perf.opWeaponDiscipline} color={scoreColor(perf.opWeaponDiscipline)} />
            <ScoreBar label="Threat Response — reaction speed"   value={perf.opThreatResponse}   color={scoreColor(perf.opThreatResponse)} />
          </div>
          <div className={styles.combatGrid} style={{ marginTop: 12 }}>
            <CombatStat label="Final Health" value={perf.opFinalHealth != null && perf.opFinalHealth >= 0 ? `${perf.opFinalHealth} HP` : '—'}
              warn={perf.opFinalHealth != null && perf.opFinalHealth <= 30} />
            <CombatStat label="Time Exposed" value={perf.opExposedSeconds != null ? `${perf.opExposedSeconds.toFixed(0)} s` : '—'} />
          </div>
        </div>
      )}

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
        </div>
      </div>
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
