// Mirrors Module 1's ScenarioEnums.cs.
// Wire-format strings (snake_case) match what ScenarioConfigLoader expects.
// Edit only when the C# enums change.

export const MISSION_TYPES = [
  { value: 'hostage_rescue', label: 'Hostage Rescue' },
]

export const ROOM_SIZES = [
  { value: 'small',  label: 'Small (4x4 m)'  },
  { value: 'medium', label: 'Medium (6x6 m)' },
  { value: 'large',  label: 'Large (8x8 m)'  },
]

export const LAYOUT_TYPES = [
  { value: 'linear',        label: 'Linear'      },
  { value: 'branching',     label: 'Branching'   },
  { value: 'hub_and_spoke', label: 'Hub & Spoke' },
  { value: 'loop',          label: 'Loop'        },
]

export const ENTRY_TYPES = [
  { value: 'single',   label: 'Single'   },
  { value: 'multiple', label: 'Multiple' },
]

export const PLACEMENT_STRATEGIES = [
  { value: 'clustered',    label: 'Clustered'    },
  { value: 'dispersed',    label: 'Dispersed'    },
  { value: 'front_loaded', label: 'Front-Loaded' },
  { value: 'deep',         label: 'Deep'         },
]

export const HOSTAGE_RISK_LEVELS = [
  { value: 'low',    label: 'Low'    },
  { value: 'medium', label: 'Medium' },
  { value: 'high',   label: 'High'   },
]

export const RANDOMNESS_LEVELS = [
  { value: 'low',    label: 'Low'    },
  { value: 'medium', label: 'Medium' },
  { value: 'high',   label: 'High'   },
]

export const DIFFICULTY_LABELS = {
  1: '1 (Easiest)',
  2: '2 (Easy)',
  3: '3 (Moderate)',
  4: '4 (Hard)',
  5: '5 (Hardest)',
}

export const DEFAULT_CONFIG = {
  schemaVersion: '1.0.0',
  missionStructure: {
    missionType: 'hostage_rescue',
    roomCount:   { min: 4, max: 4 },
    roomSize:    'medium',
    layoutType:  'branching',
    entryType:   'single',
  },
  entityConfiguration: {
    hostageCount:      1,
    terroristCount:    4,
    placementStrategy: 'dispersed',
    hostageRiskLevel:  'medium',
  },
  executionControls: {
    difficultyLevel: 3,
    randomnessLevel: 'medium',
    seed:            null,
    timeLimit:       null,
    customLabel:     null,
  },
}
