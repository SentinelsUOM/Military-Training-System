'use client'
import {
  RadarChart, Radar, PolarGrid, PolarAngleAxis, PolarRadiusAxis,
  BarChart, Bar, XAxis, YAxis, Tooltip, ResponsiveContainer, Cell
} from 'recharts'
import ScoreBar from '@/components/ui/ScoreBar'
import styles from './SummaryTab.module.css'
import { scoreColor, scorePercent, chartTheme } from '@/lib/utils'

export default function SummaryTab({ session }) {
  const perf = session.performance || {}
  const cog  = session.cognitiveSummary || {}
  const cfg  = session.scenarioConfig || {}

  const radarData = [
    { axis: 'Overall',  value: Math.round((perf.overallScore  || 0) * 100) },
    { axis: 'Safety',   value: Math.round((perf.safetyScore   || 0) * 100) },
    { axis: 'Accuracy', value: Math.round((perf.accuracyScore || 0) * 100) },
    { axis: 'Speed',    value: Math.round((perf.speedScore    || 0) * 100) },
  ]

  const cogBarData = [
    { name: 'Reaction Time', value: parseFloat((cog.averageReactionTime || 0).toFixed(2)) },
    { name: 'Cognitive Load', value: parseFloat((cog.peakCognitiveLoad  || 0).toFixed(2)) },
    { name: 'Stress Index',   value: parseFloat((cog.stressIndex        || 0).toFixed(2)) },
    { name: 'Focus Score',    value: parseFloat((cog.focusScore         || 0).toFixed(2)) },
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
            <ScoreBar label="Overall"  value={perf.overallScore}  color={scoreColor(perf.overallScore)} />
            <ScoreBar label="Safety"   value={perf.safetyScore}   color={scoreColor(perf.safetyScore)} />
            <ScoreBar label="Accuracy" value={perf.accuracyScore} color={scoreColor(perf.accuracyScore)} />
            <ScoreBar label="Speed"    value={perf.speedScore}    color={scoreColor(perf.speedScore)} />
          </div>

          <div className={styles.combatGrid}>
            <CombatStat label="Shots Fired"      value={perf.totalShotsFired ?? '—'} />
            <CombatStat label="Shots Hit"         value={perf.shotsHit ?? '—'} />
            <CombatStat label="Friendly Fire"     value={perf.friendlyFireCount ?? 0} warn={perf.friendlyFireCount > 0} />
            <CombatStat label="Terrorists Down"   value={perf.terroristsNeutralized ?? '—'} />
            <CombatStat label="Hostages Saved"    value={perf.hostagesSaved ?? '—'} />
            <CombatStat label="Mission"
              value={perf.missionSuccess ? 'SUCCESS' : 'FAIL'}
              color={perf.missionSuccess ? '#34d399' : '#f87171'}
            />
          </div>
        </div>
      </div>

      <div className={styles.botGrid}>
        <div className={styles.card}>
          <h3 className={styles.cardTitle}>Cognitive Metrics</h3>
          <div className={styles.cogRow}>
            <CogStat label="Load Level"      value={cog.cognitiveLoadLabel} />
            <CogStat label="Stress Level"    value={cog.stressLabel} />
            <CogStat label="Avg RT"          value={cog.averageReactionTime != null ? `${cog.averageReactionTime.toFixed(2)}s` : '—'} />
            <CogStat label="Focus Score"     value={cog.focusScore != null ? `${(cog.focusScore * 100).toFixed(0)}%` : '—'} />
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

function CogStat({ label, value }) {
  return (
    <div>
      <div style={{ fontSize: 10, color: 'var(--muted)', textTransform: 'uppercase', letterSpacing: '0.06em', marginBottom: 2 }}>{label}</div>
      <div style={{ fontSize: 14, fontWeight: 500, color: 'var(--text)' }}>{value ?? '—'}</div>
    </div>
  )
}
