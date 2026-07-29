import mongoose from 'mongoose'

const Vec3Schema = new mongoose.Schema(
  { x: Number, y: Number, z: Number },
  { _id: false }
)

// Explicit sub-schemas for the layout. Defining these inline (layout: { rooms: [{...}] })
// makes Mongoose misparse the nested array-of-objects as an array of strings, which throws
// a CastError on save. Declaring real sub-schemas is the reliable way to nest them.
const RoomBoxSchema = new mongoose.Schema(
  { id: String, type: String, centerX: Number, centerZ: Number, width: Number, depth: Number, height: Number },
  { _id: false }
)
const DoorMarkSchema = new mongoose.Schema(
  { x: Number, z: Number, wallSide: String, isExterior: Boolean },
  { _id: false }
)
const LayoutSchema = new mongoose.Schema(
  { rooms: [RoomBoxSchema], doors: [DoorMarkSchema] },
  { _id: false }
)

// Body-movement track uploaded by Unity's CognitiveMovementRecorder: fixed-rate
// timestamped samples of head/hand tracking plus session-level aggregates.
// Declared as real sub-schemas for the same reason as LayoutSchema above.
const MovementSampleSchema = new mongoose.Schema(
  {
    t:        Number,     // seconds since session start (same clock as events)
    head:     Vec3Schema, // world-space positions, same space as replayFrames
    lHand:    Vec3Schema,
    rHand:    Vec3Schema,
    yaw:      Number,     // head heading, degrees
    pitch:    Number,     // head pitch, degrees (positive = up)
    speed:    Number,     // horizontal head speed m/s
    angSpeed: Number,     // head yaw angular speed deg/s
    crouch:   Number,     // 0 standing → 1 fully crouched
    gun:      Number,     // bitmask: 1 = left hand weapon, 2 = right
    hp:       Number      // health fraction 0–1
  },
  { _id: false }
)
const MovementStatsSchema = new mongoose.Schema(
  {
    sampleRateHz:      Number,
    totalDistance:     Number,
    avgSpeed:          Number,
    maxSpeed:          Number,
    timeMoving:        Number,
    timeStill:         Number,
    timeCrouched:      Number,
    crouchCount:       Number,
    avgHeadHeight:     Number,
    minHeadHeight:     Number,
    totalHeadYawDeg:   Number,
    avgAngSpeed:       Number,
    peakAngSpeed:      Number,
    leftHandDistance:  Number,
    rightHandDistance: Number,
    timeWeaponHeld:    Number
  },
  { _id: false }
)
// One threat stimulus → trainee response measurement from ReactionTimeTracker.
const ReactionSampleSchema = new mongoose.Schema(
  {
    t:             Number, // stimulus time, seconds since session start
    stimulus:      String, // PlayerSeen | GunshotHeard | TargetConfirmed
    actor:         String, // enemy that caused it
    angleToThreat: Number, // gaze offset to threat at stimulus, degrees
    reactionTime:  Number, // seconds; -1 = no response within window
    channel:       String  // head | hands | movement | trigger | none
  },
  { _id: false }
)
const ReactionStatsSchema = new mongoose.Schema(
  {
    stimulusCount:      Number,
    respondedCount:     Number,
    missedCount:        Number,
    avgReactionTime:    Number,
    medianReactionTime: Number,
    bestReactionTime:   Number,
    worstReactionTime:  Number,
    avgAngleToThreat:   Number,
    headResponses:      Number,
    handResponses:      Number,
    moveResponses:      Number,
    triggerResponses:   Number
  },
  { _id: false }
)
const MovementTrackSchema = new mongoose.Schema(
  {
    stats:         MovementStatsSchema,
    samples:       [MovementSampleSchema],
    reactionStats: ReactionStatsSchema,
    reactions:     [ReactionSampleSchema]
  },
  { _id: false }
)

// Post-mission SIM-TLX questionnaire (Harris, Wilson & Vine 2020) filled in on
// the dashboard after the session uploads. ratings are the raw 0-20 answers on
// the nine validated subscales; derived holds the 0-100 composite indices
// computed server-side in /api/sessions/[id]/simtlx (see lib/simTlx.js).
const SimTlxSchema = new mongoose.Schema(
  {
    completedAt: Date,
    ratings: {
      mentalDemands:     Number,
      physicalDemands:   Number,
      temporalDemands:   Number,
      frustration:       Number,
      taskComplexity:    Number,
      situationalStress: Number,
      distraction:       Number,
      perceptualStrain:  Number,
      taskControl:       Number
    },
    derived: {
      mentalPhysical:      Number, // Mental & Physical Demands, 0-100
      temporalFrustration: Number, // Temporal Demands & Frustration, 0-100
      complexityStress:    Number, // Task Complexity & Situational Stress, 0-100
      overallWorkload:     Number, // mean of all nine subscales, 0-100
      workloadLevel:       String  // Low | Moderate | High | Very High
    }
  },
  { _id: false }
)

// Module-2 enemy-AI evaluation questionnaire (Survey 2), filled on the dashboard
// after the session — the counterpart to simTlx. `ratings` holds the raw per-item
// answers ({ scaleKey: { itemKey: value } }); `derived` holds the per-sub-scale means
// computed in /api/sessions/[id]/aieval (see lib/aiEval.js). Mixed because the rating
// tree is nested/variable. Null until the trainee completes it.
const AiEvalSchema = new mongoose.Schema(
  {
    completedAt: Date,
    ratings:     mongoose.Schema.Types.Mixed,
    derived:     mongoose.Schema.Types.Mixed
  },
  { _id: false }
)

const SessionSchema = new mongoose.Schema(
  {
    sessionId:       { type: String, required: true, unique: true, index: true },
    scenarioId:      { type: String, required: true },

    // Evaluation grouping keys (Module-2 ablation study). playerId links the same
    // participant's Dumb/Medium/Full plays; npcLevel is the AI tier that scenario ran.
    // Uploaded by Unity when set on the scenario form; null on non-study sessions.
    playerId:        { type: String, index: true },
    npcLevel:        String,   // dumb | medium | full
    // Top-level "timestamp" sent by Unity (DateTime when EndSession ran).
    // Distinct from Mongoose-managed createdAt/updatedAt.
    timestamp:       Date,

    performance: {
      safetyScore:       Number,
      accuracyScore:     Number,
      speedScore:        Number,
      overallScore:      Number,
      missionSuccess:    Boolean,
      totalShots:        Number,
      hits:              Number,
      misses:            Number,
      hostagesSaved:     Number,
      hostagesTotal:     Number,
      missionDuration:   Number,
      friendlyFireCount: Number,

      // Operator (player) safety — how safely the trainee conducted themselves,
      // distinct from safetyScore (the hostage's safety). See PLAYER_SAFETY_SCORE.md.
      operatorSafetyScore: Number,
      opSurvivability:     Number,
      opExposureControl:   Number,
      opWeaponDiscipline:  Number,
      opThreatResponse:    Number,
      opExposedSeconds:    Number,
      opFinalHealth:       Number
    },

    cognitiveSummary: {
      averageReactionTime:     Number,
      peakReactionTime:        Number,
      movementInitiationScore: Number,
      stabilityScore:          Number,
      attentionScore:          Number,
      estimatedCognitiveLoad:  String,
      estimatedStressLevel:    String
    },

    scenarioConfig: {
      topology:          String,
      difficulty:        Number,
      floors:            Number,
      rooms:             Number,
      hostagesTotal:     Number,
      terroristsTotal:   Number,
      placementStrategy: String,
      seed:              String
    },

    events: [{
      _id:          false,
      eventType:    String,
      timestamp:    Number,
      sourceActorId:String,
      targetActorId:String,
      roomId:       String,
      tags:         [String]
    }],

    npcStateChanges: [{
      _id:          false,
      actorId:      String,
      actorType:    String,
      previousState:String,
      newState:     String,
      triggerEvent: String,
      timestamp:    Number
    }],

    hostageHistory: [{
      _id:         false,
      hostageId:   String,
      timestamp:   Number,
      state:       String,
      triggerEvent:String,
      distressScore:Number
    }],

    incidents: [{
      _id:           false,
      incidentId:    String,
      incidentType:  String,
      timestamp:     Number,
      description:   String,
      involvedActors:[String],
      severity:      String
    }],

    replayFrames: [{
      _id:        false,
      timestamp:  Number,
      actorId:    String,
      position:   Vec3Schema,
      rotation:   Vec3Schema,
      currentState:String,
      annotations: [String]
    }],

    // Top-down building geometry of the played scenario (rooms + doors), used to
    // draw walls behind the 2D dot map and to build the 3D fly-around replay.
    // Mongoose strict mode drops unknown fields on save, so this must be declared
    // or the layout Unity uploads would be silently discarded. Null on old sessions.
    layout: LayoutSchema,

    // Timestamped body-movement track (head/hands/crouch/scanning) recorded by
    // the CognitiveTracking module. Must be declared or strict mode drops it on
    // save, like layout above. Null on sessions recorded before this existed.
    movementTrack: MovementTrackSchema,

    // Post-mission subjective workload questionnaire. Null until the trainee
    // completes it on the dashboard; its absence is what marks a session as
    // "pending SIM-TLX" for the auto-redirect flow.
    simTlx: SimTlxSchema,

    // Post-mission enemy-AI evaluation questionnaire (Survey 2). Null until completed.
    aiEval: AiEvalSchema
  },
  { timestamps: true }
)

// The sessions list is sorted newest-first (sort: { createdAt: -1 }). Without an index
// on createdAt, MongoDB loads and sorts every document in memory and aborts once the
// sort exceeds 32MB ("QueryExceededMemoryLimitNoDiskUseAllowed"). This index lets the
// sort be served straight from the index instead.
SessionSchema.index({ createdAt: -1 })

export default mongoose.models.Session || mongoose.model('Session', SessionSchema)
