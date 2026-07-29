'use client'
import { useMemo } from 'react'
import {
  AreaChart, Area, Line, XAxis, YAxis, Tooltip,
  CartesianGrid, ResponsiveContainer, ScatterChart, Scatter, ReferenceLine
} from 'recharts'
import MetricCard from '@/components/ui/MetricCard'
import styles from './MovementTab.module.css'
import { formatTime } from '@/lib/utils'
import { useChartTheme } from '@/lib/useTheme'
import { evaluateMovementAgainstExperts, CITATIONS } from '@/lib/movementBenchmarks'

const CROUCH_THRESHOLD = 0.35

// Downsample to keep charts responsive on long sessions.
function downsample(samples, maxPoints = 400) {
  if (!samples || samples.length <= maxPoints) return samples || []
  const step = Math.ceil(samples.length / maxPoints)
  return samples.filter((_, i) => i % step === 0)
}

function ChartCard({ title, children }) {
  return (
    <div className={styles.card}>
      <h3 className={styles.cardTitle}>{title}</h3>
      <div className={styles.chartWrap}>{children}</div>
    </div>
  )
}

export default function MovementTab({ session }) {
  const chartTheme = useChartTheme()
  const axisProps = {
    stroke: chartTheme.label,
    tick: { fill: chartTheme.label, fontSize: 11 },
    tickLine: false,
  }
  const tooltipProps = {
    contentStyle: {
      background: chartTheme.tooltip,
      border: '1px solid var(--border)',
      borderRadius: 6,
      fontSize: 12,
    },
    labelFormatter: (t) => `t = ${formatTime(t)}`,
  }

  const track = session.movementTrack
  const samples = track?.samples || []
  const stats = track?.stats || {}
  const duration = session.performance?.missionDuration || (samples.length ? samples[samples.length - 1].t : 1)

  const chartData = useMemo(
    () => downsample(samples).map(s => ({
      t: s.t,
      headY: s.head?.y ?? 0,
      speed: s.speed ?? 0,
      angSpeed: s.angSpeed ?? 0,
      crouch: s.crouch ?? 0,
      hp: (s.hp ?? 1) * 100,
      pitch: s.pitch ?? 0,
    })),
    [samples]
  )

  if (!samples.length) {
    return (
      <div className={styles.wrap}>
        <div className={styles.card}>
          <h3 className={styles.cardTitle}>Movement Data</h3>
          <p className={styles.empty}>
            No movement data was recorded for this session. Sessions played with the
            Cognitive Tracking module active upload a full body-movement track here.
          </p>
        </div>
      </div>
    )
  }

  const weaponPct = duration > 0 ? Math.round(((stats.timeWeaponHeld || 0) / duration) * 100) : 0
  const movingPct = duration > 0 ? Math.round(((stats.timeMoving || 0) / duration) * 100) : 0
  const handTravel = (stats.leftHandDistance || 0) + (stats.rightHandDistance || 0)
  const bench = evaluateMovementAgainstExperts(session)

  return (
    <div className={styles.wrap}>
      <div className={styles.metricGrid}>
        <MetricCard
          label="Distance Moved"
          value={(stats.totalDistance || 0).toFixed(1)}
          unit=" m"
          color="var(--accent)"
          subtitle={`moving ${movingPct}% of mission`}
        />
        <MetricCard
          label="Avg Speed"
          value={(stats.avgSpeed || 0).toFixed(2)}
          unit=" m/s"
          color="var(--accent)"
          subtitle={`peak ${(stats.maxSpeed || 0).toFixed(2)} m/s`}
          benchmark={bench.avgSpeed}
        />
        <MetricCard
          label="Time Crouched"
          value={formatTime(stats.timeCrouched)}
          unit=""
          color="#d29922"
          subtitle={`${stats.crouchCount || 0} crouch events`}
          benchmark={bench.timeCrouched}
        />
        <MetricCard
          label="Head Scanning"
          value={Math.round(stats.totalHeadYawDeg || 0)}
          unit="°"
          color="#bc8cff"
          subtitle={`avg ${(stats.avgAngSpeed || 0).toFixed(0)}°/s · peak ${(stats.peakAngSpeed || 0).toFixed(0)}°/s`}
          benchmark={bench.headScanning}
        />
        <MetricCard
          label="Weapon In Hand"
          value={formatTime(stats.timeWeaponHeld)}
          unit=""
          color="#3fb950"
          subtitle={`${weaponPct}% of mission`}
          benchmark={bench.weaponHeldPct}
        />
        <MetricCard
          label="Hand Travel"
          value={handTravel.toFixed(1)}
          unit=" m"
          color="#58a6ff"
          subtitle={`L ${(stats.leftHandDistance || 0).toFixed(1)} m · R ${(stats.rightHandDistance || 0).toFixed(1)} m`}
          benchmark={bench.handTravel}
        />
      </div>

      <ReactionSection track={track} bench={bench} />

      <PathMap samples={samples} layout={session.layout} />

      <div className={styles.chartGrid}>
        <ChartCard title="Movement Speed (m/s)">
          <ResponsiveContainer width="100%" height={180}>
            <AreaChart data={chartData}>
              <CartesianGrid stroke={chartTheme.grid} strokeDasharray="3 3" vertical={false} />
              <XAxis dataKey="t" tickFormatter={formatTime} {...axisProps} />
              <YAxis width={36} {...axisProps} />
              <Tooltip {...tooltipProps} formatter={(v) => [`${v.toFixed(2)} m/s`, 'speed']} />
              <Area type="monotone" dataKey="speed" stroke={chartTheme.accent}
                    fill={chartTheme.accent} fillOpacity={0.25} strokeWidth={1.5} isAnimationActive={false} />
            </AreaChart>
          </ResponsiveContainer>
        </ChartCard>

        <ChartCard title="Posture — Head Height (m) & Crouch">
          <ResponsiveContainer width="100%" height={180}>
            <AreaChart data={chartData}>
              <CartesianGrid stroke={chartTheme.grid} strokeDasharray="3 3" vertical={false} />
              <XAxis dataKey="t" tickFormatter={formatTime} {...axisProps} />
              <YAxis yAxisId="h" width={36} domain={[0, 'auto']} {...axisProps} />
              <YAxis yAxisId="c" orientation="right" width={30} domain={[0, 1]} hide />
              <Tooltip {...tooltipProps}
                formatter={(v, name) => name === 'crouch'
                  ? [`${Math.round(v * 100)}%`, 'crouch']
                  : [`${v.toFixed(2)} m`, 'head height']} />
              <Area yAxisId="c" type="step" dataKey="crouch" stroke="#d29922"
                    fill="#d29922" fillOpacity={0.15} strokeWidth={1} isAnimationActive={false} />
              <Line yAxisId="h" type="monotone" dataKey="headY" stroke="#3fb950"
                    dot={false} strokeWidth={1.5} isAnimationActive={false} />
            </AreaChart>
          </ResponsiveContainer>
        </ChartCard>

        <ChartCard title="Head Scanning Speed (°/s)">
          <ResponsiveContainer width="100%" height={180}>
            <AreaChart data={chartData}>
              <CartesianGrid stroke={chartTheme.grid} strokeDasharray="3 3" vertical={false} />
              <XAxis dataKey="t" tickFormatter={formatTime} {...axisProps} />
              <YAxis width={36} {...axisProps} />
              <Tooltip {...tooltipProps} formatter={(v) => [`${v.toFixed(0)}°/s`, 'scanning']} />
              <Area type="monotone" dataKey="angSpeed" stroke="#bc8cff"
                    fill="#bc8cff" fillOpacity={0.2} strokeWidth={1.5} isAnimationActive={false} />
            </AreaChart>
          </ResponsiveContainer>
        </ChartCard>

        <ChartCard title="Health Over Time (%)">
          <ResponsiveContainer width="100%" height={180}>
            <AreaChart data={chartData}>
              <CartesianGrid stroke={chartTheme.grid} strokeDasharray="3 3" vertical={false} />
              <XAxis dataKey="t" tickFormatter={formatTime} {...axisProps} />
              <YAxis width={36} domain={[0, 100]} {...axisProps} />
              <Tooltip {...tooltipProps} formatter={(v) => [`${Math.round(v)}%`, 'health']} />
              <Area type="step" dataKey="hp" stroke="#f85149"
                    fill="#f85149" fillOpacity={0.2} strokeWidth={1.5} isAnimationActive={false} />
            </AreaChart>
          </ResponsiveContainer>
        </ChartCard>
      </div>

      <BenchmarkSources bench={bench} />
    </div>
  )
}

/** Lists only the citations actually backing a benchmark shown above. */
function BenchmarkSources({ bench }) {
  const usedIds = new Set()
  Object.values(bench).forEach(entry => (entry.citationIds || []).forEach(id => usedIds.add(id)))
  if (!usedIds.size) return null

  return (
    <div className={styles.card}>
      <h3 className={styles.cardTitle}>Expert Benchmark Sources</h3>
      <ul className={styles.sourceList}>
        {[...usedIds].map(id => {
          const c = CITATIONS[id]
          if (!c) return null
          return (
            <li key={id} className={styles.sourceItem}>
              <a href={c.url} target="_blank" rel="noopener noreferrer">
                {c.authors}{c.year ? ` (${c.year})` : ''} — {c.title}
              </a>
              <p className={styles.sourceFinding}>{c.finding}</p>
            </li>
          )
        })}
      </ul>
      <p className={styles.empty}>
        A = direct empirical match · B = literature proxy · C = no quantified literature (shown unvalidated).
        See MODULE4_MOVEMENT_VALIDATION_METHODOLOGY.md for the full derivation of every range.
      </p>
    </div>
  )
}

const CHANNEL_COLORS = {
  head:     '#bc8cff',
  hands:    '#58a6ff',
  movement: '#d29922',
  trigger:  '#3fb950',
}
const CHANNEL_LABELS = {
  head:     'Head turn',
  hands:    'Weapon raise',
  movement: 'Moved to cover',
  trigger:  'Fired back',
}

/**
 * Reaction time to threat stimuli. Each dot is one enemy stimulus (spotted /
 * shot at / engaged); its height is how long the trainee took to respond and
 * its color shows which body channel responded first.
 */
const STIMULUS_LABELS = {
  PlayerSeen: 'Spotted by enemy',
  GunshotHeard: 'Gunshot heard',
  TargetConfirmed: 'Target confirmed',
}

function ReactionTooltip({ active, payload }) {
  if (!active || !payload?.length) return null
  const p = payload[0].payload
  return (
    <div className={styles.reactionTooltip}>
      <div className={styles.reactionTooltipEvent}>{STIMULUS_LABELS[p.stimulus] || p.stimulus}</div>
      <div>{p.actor} · at {formatTime(p.t)}</div>
      <div>reacted in {p.rt.toFixed(2)} s ({CHANNEL_LABELS[p.channel] || p.channel})</div>
      {typeof p.angle === 'number' && <div>gaze offset {Math.round(p.angle)}°</div>}
    </div>
  )
}

const CHANNEL_BENCH_KEY = {
  head: 'reactionHead',
  hands: 'reactionHands',
  movement: 'reactionMovement',
  trigger: 'reactionTrigger',
}

function ReactionSection({ track, bench }) {
  const chartTheme = useChartTheme()
  const axisProps = {
    stroke: chartTheme.label,
    tick: { fill: chartTheme.label, fontSize: 11 },
    tickLine: false,
  }
  const reactions = track?.reactions || []
  const rs = track?.reactionStats || {}

  if (!reactions.length) {
    return (
      <div className={styles.card}>
        <h3 className={styles.cardTitle}>Reaction Time</h3>
        <p className={styles.empty}>
          No threat stimuli were recorded for this session, so no reaction times
          could be measured.
        </p>
      </div>
    )
  }

  const responded = reactions.filter(r => r.reactionTime >= 0)
  const byChannel = ['head', 'hands', 'movement', 'trigger'].map(ch => ({
    channel: ch,
    data: responded
      .filter(r => r.channel === ch)
      .map(r => ({ t: r.t, rt: r.reactionTime, stimulus: r.stimulus, actor: r.actor, angle: r.angleToThreat, channel: r.channel })),
  })).filter(g => g.data.length > 0)

  const channelCounts = [
    ['Head turn', rs.headResponses],
    ['Weapon raise', rs.handResponses],
    ['Moved', rs.moveResponses],
    ['Fired', rs.triggerResponses],
  ].filter(([, n]) => n > 0)
  const primary = channelCounts.sort((a, b) => b[1] - a[1])[0]

  return (
    <>
      <div className={styles.metricGrid}>
        <MetricCard
          label="Avg Reaction Time"
          value={(rs.avgReactionTime || 0).toFixed(2)}
          unit=" s"
          color="var(--accent)"
          subtitle={`median ${(rs.medianReactionTime || 0).toFixed(2)} s`}
          benchmark={bench?.reactionOverall}
        />
        <MetricCard
          label="Best Reaction"
          value={(rs.bestReactionTime || 0).toFixed(2)}
          unit=" s"
          color="#3fb950"
          subtitle={`slowest ${(rs.worstReactionTime || 0).toFixed(2)} s`}
        />
        <MetricCard
          label="Threats Responded"
          value={`${rs.respondedCount || 0}/${rs.stimulusCount || 0}`}
          unit=""
          color={rs.missedCount > 0 ? '#d29922' : '#3fb950'}
          subtitle={`${rs.missedCount || 0} missed · avg gaze offset ${Math.round(rs.avgAngleToThreat || 0)}°`}
          benchmark={bench?.threatsResponded}
        />
        <MetricCard
          label="Primary Response"
          value={primary ? primary[0] : '—'}
          unit=""
          color="#bc8cff"
          subtitle={channelCounts.map(([l, n]) => `${l} ${n}`).join(' · ')}
        />
      </div>

      {bench && <ChannelBenchmarkStrip byChannel={byChannel} bench={bench} />}

      <div className={styles.card}>
        <h3 className={styles.cardTitle}>
          Reaction Time per Threat
          <span className={styles.legend}>
            {Object.entries(CHANNEL_LABELS).map(([ch, label]) => (
              <span key={ch} className={styles.legendItem}>
                <i style={{ background: CHANNEL_COLORS[ch] }} /> {label}
              </span>
            ))}
          </span>
        </h3>
        <div className={styles.chartWrap}>
          <ResponsiveContainer width="100%" height={220}>
            <ScatterChart>
              <CartesianGrid stroke={chartTheme.grid} strokeDasharray="3 3" />
              <XAxis dataKey="t" name="time" type="number" domain={['dataMin', 'dataMax']}
                     tickFormatter={formatTime} {...axisProps} />
              <YAxis dataKey="rt" name="reaction" type="number" width={40}
                     unit="s" domain={[0, 'auto']} {...axisProps} />
              {rs.avgReactionTime > 0 && (
                <ReferenceLine y={rs.avgReactionTime} stroke={chartTheme.label}
                               strokeDasharray="4 4"
                               label={{ value: 'avg', fill: chartTheme.label, fontSize: 10, position: 'right' }} />
              )}
              <Tooltip
                cursor={{ strokeDasharray: '3 3' }}
                content={<ReactionTooltip />}
              />
              {byChannel.map(g => (
                <Scatter key={g.channel} name={CHANNEL_LABELS[g.channel]}
                         data={g.data} fill={CHANNEL_COLORS[g.channel]} isAnimationActive={false} />
              ))}
            </ScatterChart>
          </ResponsiveContainer>
        </div>
      </div>
    </>
  )
}

/** Per-channel reaction time vs. its literature-derived expert band. */
function ChannelBenchmarkStrip({ byChannel, bench }) {
  const rows = byChannel
    .map(g => {
      const key = CHANNEL_BENCH_KEY[g.channel]
      const b = bench[key]
      if (!b || b.severity === 'unvalidated') return null
      const avg = g.data.reduce((sum, d) => sum + d.rt, 0) / g.data.length
      return { channel: g.channel, avg, b }
    })
    .filter(Boolean)

  if (!rows.length) return null

  return (
    <div className={styles.card}>
      <h3 className={styles.cardTitle}>Reaction Time by Channel vs. Expert Benchmark</h3>
      <div className={styles.benchRowGrid}>
        {rows.map(({ channel, avg, b }) => (
          <div key={channel} className={styles.benchRow}>
            <span className={styles.benchRowLabel}>
              <i style={{ background: CHANNEL_COLORS[channel] }} /> {CHANNEL_LABELS[channel]}
            </span>
            <span className={styles.benchRowValue}>{avg.toFixed(2)} s</span>
            <span className={`${styles.benchRowVerdict} ${styles[b.severity === 'good' ? 'benchmarkWithin' : 'benchmarkOff']}`}>
              expert {b.rangeText} · {b.verdict}<sup>{b.tier}</sup>
            </span>
          </div>
        ))}
      </div>
    </div>
  )
}

/** Top-down movement path over the scenario floor plan. */
function PathMap({ samples, layout }) {
  const rooms = layout?.rooms || []
  const doors = layout?.doors || []

  const { minX, maxX, minZ, maxZ } = useMemo(() => {
    let minX = Infinity, maxX = -Infinity, minZ = Infinity, maxZ = -Infinity
    for (const r of rooms) {
      minX = Math.min(minX, r.centerX - r.width / 2)
      maxX = Math.max(maxX, r.centerX + r.width / 2)
      minZ = Math.min(minZ, r.centerZ - r.depth / 2)
      maxZ = Math.max(maxZ, r.centerZ + r.depth / 2)
    }
    for (const s of samples) {
      minX = Math.min(minX, s.head.x); maxX = Math.max(maxX, s.head.x)
      minZ = Math.min(minZ, s.head.z); maxZ = Math.max(maxZ, s.head.z)
    }
    if (!isFinite(minX)) { minX = -1; maxX = 1; minZ = -1; maxZ = 1 }
    return { minX, maxX, minZ, maxZ }
  }, [rooms, samples])

  const PAD = 2
  const w = Math.max(1, maxX - minX) + PAD * 2
  const h = Math.max(1, maxZ - minZ) + PAD * 2
  // World → SVG: x maps directly, z flips so +Z (north) points up.
  const sx = (x) => x - minX + PAD
  const sy = (z) => maxZ - z + PAD

  const pts = useMemo(() => downsample(samples, 800), [samples])
  const walkPts = []
  const crouchSegs = []
  let seg = null
  for (const s of pts) {
    walkPts.push(`${sx(s.head.x).toFixed(2)},${sy(s.head.z).toFixed(2)}`)
    if ((s.crouch ?? 0) > CROUCH_THRESHOLD) {
      if (!seg) seg = []
      seg.push(`${sx(s.head.x).toFixed(2)},${sy(s.head.z).toFixed(2)}`)
    } else if (seg) {
      if (seg.length > 1) crouchSegs.push(seg)
      seg = null
    }
  }
  if (seg && seg.length > 1) crouchSegs.push(seg)

  const start = pts[0], end = pts[pts.length - 1]
  const strokeW = Math.max(w, h) / 220

  return (
    <div className={styles.card}>
      <h3 className={styles.cardTitle}>
        Movement Path
        <span className={styles.legend}>
          <span className={styles.legendItem}><i style={{ background: '#1f6feb' }} /> walking</span>
          <span className={styles.legendItem}><i style={{ background: '#d29922' }} /> crouched</span>
          <span className={styles.legendItem}><i style={{ background: '#3fb950' }} /> start</span>
          <span className={styles.legendItem}><i style={{ background: '#f85149' }} /> end</span>
        </span>
      </h3>
      <div className={styles.mapWrap}>
        <svg viewBox={`0 0 ${w} ${h}`} className={styles.mapSvg} preserveAspectRatio="xMidYMid meet">
          {rooms.map((r, i) => (
            <rect key={i}
              x={sx(r.centerX - r.width / 2)} y={sy(r.centerZ + r.depth / 2)}
              width={r.width} height={r.depth}
              fill="rgba(88, 166, 255, 0.05)" stroke="var(--border)" strokeWidth={strokeW * 0.8} />
          ))}
          {doors.map((d, i) => (
            <circle key={i} cx={sx(d.x)} cy={sy(d.z)} r={strokeW * 2}
              fill="none" stroke="var(--muted)" strokeWidth={strokeW * 0.6} />
          ))}
          {walkPts.length > 1 && (
            <polyline points={walkPts.join(' ')} fill="none"
              stroke="#1f6feb" strokeWidth={strokeW * 1.6}
              strokeLinejoin="round" strokeLinecap="round" opacity={0.9} />
          )}
          {crouchSegs.map((s, i) => (
            <polyline key={i} points={s.join(' ')} fill="none"
              stroke="#d29922" strokeWidth={strokeW * 2.2}
              strokeLinejoin="round" strokeLinecap="round" />
          ))}
          {start && (
            <circle cx={sx(start.head.x)} cy={sy(start.head.z)} r={strokeW * 3}
              fill="#3fb950" stroke="var(--bg)" strokeWidth={strokeW * 0.8} />
          )}
          {end && (
            <circle cx={sx(end.head.x)} cy={sy(end.head.z)} r={strokeW * 3}
              fill="#f85149" stroke="var(--bg)" strokeWidth={strokeW * 0.8} />
          )}
        </svg>
      </div>
    </div>
  )
}
