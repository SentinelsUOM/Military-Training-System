// SIM-TLX — Simulation Task Load Index.
// Harris, Wilson & Vine (2020), "Development and validation of a simulation
// workload measure: the simulation task load index (SIM-TLX)", Virtual Reality
// 24:557-566. https://doi.org/10.1007/s10055-019-00422-9
//
// The validated instrument has 9 subscales (the paper's 10th, "presence", was
// excluded after validation), each rated on a 21-point scale (0-20) anchored
// low -> high. This project reports the raw (unweighted) ratings — the standard
// "Raw TLX" administration (Hart 2006) — and derives three paired constructs
// tracked by the research plan, plus an overall workload index.

export const SIM_TLX_SCALE_MAX = 20 // 21-point scale: 0..20

// Question wording is verbatim from the paper (§2.1, dimension list).
export const SIM_TLX_DIMENSIONS = [
  {
    key: 'mentalDemands',
    label: 'Mental Demands',
    question: 'How mentally fatiguing was the task?',
    hint: 'Thinking, deciding, remembering, searching — the cognitive effort the mission required.'
  },
  {
    key: 'physicalDemands',
    label: 'Physical Demands',
    question: 'How physically fatiguing was the task?',
    hint: 'Moving, crouching, aiming, holding the weapon — the bodily effort the mission required.'
  },
  {
    key: 'temporalDemands',
    label: 'Temporal Demands',
    question: 'How hurried or rushed did you feel during the task?',
    hint: 'Time pressure felt due to the pace of the mission.'
  },
  {
    key: 'frustration',
    label: 'Frustration',
    question: 'How insecure, discouraged, irritated, stressed or annoyed were you?',
    hint: 'Negative feelings experienced while executing the mission.'
  },
  {
    key: 'taskComplexity',
    label: 'Task Complexity',
    question: 'How complex was the task?',
    hint: 'Perceived difficulty of the mission objectives and decisions.'
  },
  {
    key: 'situationalStress',
    label: 'Situational Stress',
    question: 'How stressed did you feel while performing the task?',
    hint: 'Pressure experienced in the combat / patrol situation itself.'
  },
  {
    key: 'distraction',
    label: 'Distraction',
    question: 'How distracting was the task environment?',
    hint: 'Sounds, movement or events that pulled attention away from the objective.'
  },
  {
    key: 'perceptualStrain',
    label: 'Perceptual Strain',
    question: 'How uncomfortable/irritating were the visual and auditory aspects of the task?',
    hint: 'Discomfort from the headset display, visuals or audio.'
  },
  {
    key: 'taskControl',
    label: 'Task Control',
    question: 'How difficult was the task to control/navigate?',
    hint: 'Difficulty operating the VR controllers, moving and navigating.'
  }
]

export const SIM_TLX_KEYS = SIM_TLX_DIMENSIONS.map(d => d.key)

// Categorical colors validated (dataviz six-checks) against the dashboard's
// dark surface #161b22. Order matters — do not re-order or cycle.
export const SIM_TLX_COLORS = {
  mentalPhysical:      '#3987e5', // blue
  temporalFrustration: '#d95926', // orange
  complexityStress:    '#199e70', // aqua
  overall:             '#c98500'  // yellow
}

// The three paired constructs tracked by the study, mapped onto SIM-TLX subscales.
export const SIM_TLX_COMPOSITES = [
  {
    key: 'mentalPhysical',
    label: 'Mental & Physical Demands',
    short: 'Mental + Physical',
    dims: ['mentalDemands', 'physicalDemands'],
    color: SIM_TLX_COLORS.mentalPhysical,
    description: 'Cognitive fatigue and physical exertion required by the simulation task.'
  },
  {
    key: 'temporalFrustration',
    label: 'Temporal Demands & Frustration',
    short: 'Temporal + Frustration',
    dims: ['temporalDemands', 'frustration'],
    color: SIM_TLX_COLORS.temporalFrustration,
    description: 'Time-pressure urgency and stress or annoyance during execution.'
  },
  {
    key: 'complexityStress',
    label: 'Task Complexity & Situational Stress',
    short: 'Complexity + Stress',
    dims: ['taskComplexity', 'situationalStress'],
    color: SIM_TLX_COLORS.complexityStress,
    description: 'Perceived difficulty and pressure experienced in the combat / patrol scenario.'
  }
]

export const workloadLevel = (score) =>
  score < 25 ? 'Low' : score < 50 ? 'Moderate' : score < 75 ? 'High' : 'Very High'

export const workloadColor = (score) =>
  score < 25 ? 'var(--success)' : score < 50 ? 'var(--accent)' : score < 75 ? 'var(--warning)' : 'var(--danger)'

const toPct = (mean0to20) => Math.round((mean0to20 / SIM_TLX_SCALE_MAX) * 1000) / 10

// Validates a ratings object ({ dimensionKey: 0..20 }) and returns the derived
// composite scores on a 0-100 scale. Throws with a readable message on bad input.
export function computeSimTlxDerived(ratings) {
  if (!ratings || typeof ratings !== 'object') throw new Error('ratings object is required')
  for (const key of SIM_TLX_KEYS) {
    const v = ratings[key]
    if (typeof v !== 'number' || Number.isNaN(v) || v < 0 || v > SIM_TLX_SCALE_MAX) {
      throw new Error(`rating "${key}" must be a number between 0 and ${SIM_TLX_SCALE_MAX}`)
    }
  }

  const mean = (keys) => keys.reduce((sum, k) => sum + ratings[k], 0) / keys.length

  const derived = {}
  for (const c of SIM_TLX_COMPOSITES) derived[c.key] = toPct(mean(c.dims))
  derived.overallWorkload = toPct(mean(SIM_TLX_KEYS))
  derived.workloadLevel = workloadLevel(derived.overallWorkload)
  return derived
}
