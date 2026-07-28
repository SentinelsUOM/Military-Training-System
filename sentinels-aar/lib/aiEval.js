// Module-2 Enemy-AI evaluation questionnaire (Survey 2).
// Administered on the dashboard after each scenario, alongside the SIM-TLX (Survey 1).
// It combines three validated instruments plus a small author-defined realism block:
//
//   • UEQ-S  — User Experience Questionnaire, short form (Schrepp, Hinderks &
//              Thomaschewski 2017). 8 semantic-differential items, 7-point, two
//              meta-scales: Pragmatic Quality + Hedonic Quality. Store raw 1..7 so
//              the official UEQ Data Analysis Tool can be used directly.
//   • Godspeed — Perceived Intelligence (5) + Animacy (6) sub-scales (Bartneck,
//              Kulic, Croft & Zoghbi 2009). 5-point semantic differential. The
//              Likeability / Perceived Safety / Anthropomorphism sub-scales are
//              intentionally omitted (they assume a friendly agent, not an enemy).
//   • Realism — 3 author-defined items grounded in the training-validity framework
//              (Harris et al. 2020, construct validity). 5-point agreement.
//
// Every sub-scale is scored as the MEAN of its items per participant; UEQ items are
// additionally reported on the standard -3..+3 scale (raw − 4) for interpretation.

// Item types: 'pair' = semantic differential (pick between left/right anchors);
//             'likert' = agreement with a statement.

export const AI_EVAL_SCALES = [
  {
    key: 'ueqPragmatic',
    label: 'Ease of use (UEQ-S — Pragmatic Quality)',
    instrument: 'UEQ-S',
    type: 'pair',
    min: 1, max: 7,
    prompt: 'Fighting these enemies felt…',
    items: [
      { key: 'obstructive_supportive', left: 'Obstructive',    right: 'Supportive' },
      { key: 'complicated_easy',       left: 'Complicated',    right: 'Easy'       },
      { key: 'inefficient_efficient',  left: 'Inefficient',    right: 'Efficient'  },
      { key: 'confusing_clear',        left: 'Confusing',      right: 'Clear'      },
    ],
  },
  {
    key: 'ueqHedonic',
    label: 'Engagement (UEQ-S — Hedonic Quality)',
    instrument: 'UEQ-S',
    type: 'pair',
    min: 1, max: 7,
    prompt: 'Fighting these enemies felt…',
    items: [
      { key: 'boring_exciting',          left: 'Boring',          right: 'Exciting'      },
      { key: 'notinteresting_interesting', left: 'Not interesting', right: 'Interesting' },
      { key: 'conventional_inventive',   left: 'Conventional',    right: 'Inventive'     },
      { key: 'usual_leadingedge',        left: 'Usual',           right: 'Leading edge'  },
    ],
  },
  {
    key: 'perceivedIntelligence',
    label: 'Perceived Intelligence (Godspeed)',
    instrument: 'Godspeed',
    type: 'pair',
    min: 1, max: 5,
    prompt: 'The enemy AI seemed…',
    items: [
      { key: 'incompetent_competent',       left: 'Incompetent',   right: 'Competent'    },
      { key: 'ignorant_knowledgeable',      left: 'Ignorant',      right: 'Knowledgeable' },
      { key: 'irresponsible_responsible',   left: 'Irresponsible', right: 'Responsible'  },
      { key: 'unintelligent_intelligent',   left: 'Unintelligent', right: 'Intelligent'  },
      { key: 'foolish_sensible',            left: 'Foolish',       right: 'Sensible'     },
    ],
  },
  {
    key: 'animacy',
    label: 'Animacy / life-likeness (Godspeed)',
    instrument: 'Godspeed',
    type: 'pair',
    min: 1, max: 5,
    prompt: 'The enemy AI seemed…',
    items: [
      { key: 'dead_alive',            left: 'Dead',        right: 'Alive'       },
      { key: 'stagnant_lively',       left: 'Stagnant',    right: 'Lively'      },
      { key: 'mechanical_organic',    left: 'Mechanical',  right: 'Organic'     },
      { key: 'artificial_lifelike',   left: 'Artificial',  right: 'Lifelike'    },
      { key: 'inert_interactive',     left: 'Inert',       right: 'Interactive' },
      { key: 'apathetic_responsive',  left: 'Apathetic',   right: 'Responsive'  },
    ],
  },
  {
    key: 'realism',
    label: 'Tactical realism (author-defined)',
    instrument: 'author-defined',
    type: 'likert',
    min: 1, max: 5,
    prompt: 'Rate your agreement (1 = strongly disagree, 5 = strongly agree):',
    items: [
      { key: 'reactedRealistically',  statement: 'The enemies reacted to being shot at in a realistic way.' },
      { key: 'searchedRealistically', statement: 'The way they searched for me when I hid was realistic.'   },
      { key: 'behaviourRealistic',    statement: 'Overall, their behaviour matched how real hostiles would act.' },
    ],
  },
]

// Flat list of every item key, for validation.
export const AI_EVAL_ITEM_KEYS = AI_EVAL_SCALES.flatMap(s => s.items.map(i => `${s.key}.${i.key}`))

// Total number of individual questions (used by the intro screen / progress).
export const AI_EVAL_ITEM_COUNT = AI_EVAL_ITEM_KEYS.length

const round1 = (n) => Math.round(n * 10) / 10

/**
 * Validate a ratings object of shape { scaleKey: { itemKey: value } } and return
 * the derived per-sub-scale means. UEQ scales also get a *_ueq value on the
 * standard -3..+3 scale (raw − 4). Throws a readable error on bad/missing input.
 */
export function computeAiEvalDerived(ratings) {
  if (!ratings || typeof ratings !== 'object') throw new Error('ratings object is required')

  const derived = {}
  for (const scale of AI_EVAL_SCALES) {
    const answers = ratings[scale.key]
    if (!answers || typeof answers !== 'object') {
      throw new Error(`missing answers for sub-scale "${scale.key}"`)
    }
    let sum = 0
    for (const item of scale.items) {
      const v = answers[item.key]
      if (typeof v !== 'number' || Number.isNaN(v) || v < scale.min || v > scale.max) {
        throw new Error(`"${scale.key}.${item.key}" must be a number between ${scale.min} and ${scale.max}`)
      }
      sum += v
    }
    const mean = sum / scale.items.length
    derived[scale.key] = round1(mean)
    if (scale.instrument === 'UEQ-S') derived[`${scale.key}_ueq`] = round1(mean - 4) // -3..+3
  }
  return derived
}
