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
const MovementTrackSchema = new mongoose.Schema(
  { stats: MovementStatsSchema, samples: [MovementSampleSchema] },
  { _id: false }
)

const SessionSchema = new mongoose.Schema(
  {
    sessionId:       { type: String, required: true, unique: true, index: true },
    scenarioId:      { type: String, required: true },
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
      friendlyFireCount: Number
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
    movementTrack: MovementTrackSchema
  },
  { timestamps: true }
)

// The sessions list is sorted newest-first (sort: { createdAt: -1 }). Without an index
// on createdAt, MongoDB loads and sorts every document in memory and aborts once the
// sort exceeds 32MB ("QueryExceededMemoryLimitNoDiskUseAllowed"). This index lets the
// sort be served straight from the index instead.
SessionSchema.index({ createdAt: -1 })

export default mongoose.models.Session || mongoose.model('Session', SessionSchema)
