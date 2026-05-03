import mongoose from 'mongoose'

const Vec3Schema = new mongoose.Schema(
  { x: Number, y: Number, z: Number },
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
    }]
  },
  { timestamps: true }
)

export default mongoose.models.Session || mongoose.model('Session', SessionSchema)
