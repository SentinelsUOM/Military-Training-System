import { NextResponse } from 'next/server'
import { connectDB } from '@/lib/mongodb'
import Session from '@/lib/models/Session'
import { computeSimTlxDerived } from '@/lib/simTlx'

const corsHeaders = {
  'Access-Control-Allow-Origin': '*',
  'Access-Control-Allow-Methods': 'POST, OPTIONS',
  'Access-Control-Allow-Headers': 'Content-Type'
}

export async function OPTIONS() {
  return NextResponse.json({}, { headers: corsHeaders })
}

export async function POST() {
  try {
    await connectDB()

    const sessions = [
      // ─────────────────────────────────────────────────────────────
      // Session 1 — SUCCESS, Grid, High difficulty
      // ─────────────────────────────────────────────────────────────
      {
        sessionId:       'SEN-2026-0045',
        scenarioId:      'GRID-3F-HIGH',
        timestamp:       new Date('2026-04-20T09:23:00Z'),
        performance: {
          safetyScore:      0.91,
          accuracyScore:    0.74,
          speedScore:       0.69,
          overallScore:     0.83,
          missionSuccess:   true,
          totalShots:       17,
          hits:             13,
          misses:           4,
          hostagesSaved:    2,
          hostagesTotal:    2,
          missionDuration:  134,
          friendlyFireCount:0
        },
        cognitiveSummary: {
          averageReactionTime:     0.72,
          peakReactionTime:        1.43,
          movementInitiationScore: 0.69,
          stabilityScore:          0.81,
          attentionScore:          0.76,
          estimatedCognitiveLoad:  'Medium',
          estimatedStressLevel:    'Medium'
        },
        scenarioConfig: {
          topology:          'Grid',
          difficulty:        4,
          floors:            3,
          rooms:             6,
          hostagesTotal:     2,
          terroristsTotal:   3,
          placementStrategy: 'Clustered',
          seed:              '490218'
        },
        events: [
          { eventType:'DoorOpened',            timestamp:3,   roomId:'Entry',    sourceActorId:'trainee_01', tags:[] },
          { eventType:'RoomBreached',           timestamp:8,   roomId:'Hallway',  sourceActorId:'trainee_01', tags:[] },
          { eventType:'PlayerSeen',             timestamp:12,  roomId:'Hallway',  sourceActorId:'terrorist_01', targetActorId:'trainee_01', tags:[] },
          { eventType:'ShotFired',              timestamp:15,  roomId:'Hallway',  sourceActorId:'trainee_01', tags:[] },
          { eventType:'GunshotHeard',           timestamp:16,  roomId:'Room2',    sourceActorId:'terrorist_02', tags:[] },
          { eventType:'TerroristHit',           timestamp:18,  roomId:'Hallway',  sourceActorId:'trainee_01', targetActorId:'terrorist_01', tags:[] },
          { eventType:'TerroristDown',          timestamp:22,  roomId:'Hallway',  targetActorId:'terrorist_01', tags:[] },
          { eventType:'DoorOpened',             timestamp:30,  roomId:'Room2',    sourceActorId:'trainee_01', tags:[] },
          { eventType:'RoomBreached',           timestamp:35,  roomId:'Room2',    sourceActorId:'trainee_01', tags:[] },
          { eventType:'HostageContactStarted',  timestamp:38,  roomId:'Room2',    sourceActorId:'trainee_01', targetActorId:'hostage_01', tags:[] },
          { eventType:'ShotFired',              timestamp:44,  roomId:'Room2',    sourceActorId:'trainee_01', tags:[] },
          { eventType:'TerroristHit',           timestamp:47,  roomId:'Room2',    sourceActorId:'trainee_01', targetActorId:'terrorist_02', tags:[] },
          { eventType:'TerroristDown',          timestamp:52,  roomId:'Room2',    targetActorId:'terrorist_02', tags:[] },
          { eventType:'HostageFreed',           timestamp:58,  roomId:'Room2',    targetActorId:'hostage_01', tags:[] },
          { eventType:'DoorOpened',             timestamp:70,  roomId:'Room5',    sourceActorId:'trainee_01', tags:[] },
          { eventType:'PlayerSeen',             timestamp:75,  roomId:'Room5',    sourceActorId:'terrorist_03', targetActorId:'trainee_01', tags:[] },
          { eventType:'ShotFired',              timestamp:78,  roomId:'Room5',    sourceActorId:'trainee_01', tags:[] },
          { eventType:'TerroristHit',           timestamp:82,  roomId:'Room5',    sourceActorId:'trainee_01', targetActorId:'terrorist_03', tags:[] },
          { eventType:'TerroristDown',          timestamp:90,  roomId:'Room5',    targetActorId:'terrorist_03', tags:[] },
          { eventType:'HostageFreed',           timestamp:110, roomId:'Room5',    targetActorId:'hostage_02', tags:[] }
        ],
        npcStateChanges: [
          { actorId:'terrorist_01', actorType:'Terrorist', previousState:'Idle',        newState:'Suspicious',  triggerEvent:'NearbyNoise',    timestamp:12 },
          { actorId:'terrorist_01', actorType:'Terrorist', previousState:'Suspicious',  newState:'Alert',       triggerEvent:'PlayerConfirmed', timestamp:13 },
          { actorId:'terrorist_01', actorType:'Terrorist', previousState:'Alert',       newState:'Engage',      triggerEvent:'ShotFired',       timestamp:15 },
          { actorId:'terrorist_01', actorType:'Terrorist', previousState:'Engage',      newState:'Neutralized', triggerEvent:'TerroristDown',   timestamp:22 },
          { actorId:'terrorist_02', actorType:'Terrorist', previousState:'Idle',        newState:'Alert',       triggerEvent:'GunshotHeard',    timestamp:16 },
          { actorId:'terrorist_02', actorType:'Terrorist', previousState:'Alert',       newState:'Engage',      triggerEvent:'PlayerSeen',      timestamp:44 },
          { actorId:'terrorist_02', actorType:'Terrorist', previousState:'Engage',      newState:'Neutralized', triggerEvent:'TerroristDown',   timestamp:52 },
          { actorId:'terrorist_03', actorType:'Terrorist', previousState:'Idle',        newState:'Suspicious',  triggerEvent:'RadioAlert',      timestamp:75 },
          { actorId:'terrorist_03', actorType:'Terrorist', previousState:'Suspicious',  newState:'Alert',       triggerEvent:'PlayerSeen',      timestamp:76 },
          { actorId:'terrorist_03', actorType:'Terrorist', previousState:'Alert',       newState:'Engage',      triggerEvent:'ShotFired',       timestamp:78 },
          { actorId:'terrorist_03', actorType:'Terrorist', previousState:'Engage',      newState:'Neutralized', triggerEvent:'TerroristDown',   timestamp:90 },
          { actorId:'hostage_01',   actorType:'Hostage',   previousState:'Calm',        newState:'Fearful',     triggerEvent:'GunshotHeard',    timestamp:44 },
          { actorId:'hostage_01',   actorType:'Hostage',   previousState:'Fearful',     newState:'Follow',      triggerEvent:'HostageFreed',    timestamp:58 },
          { actorId:'hostage_02',   actorType:'Hostage',   previousState:'Calm',        newState:'Fearful',     triggerEvent:'RadioAlert',      timestamp:75 },
          { actorId:'hostage_02',   actorType:'Hostage',   previousState:'Fearful',     newState:'Panic',       triggerEvent:'ShotFired',       timestamp:78 },
          { actorId:'hostage_02',   actorType:'Hostage',   previousState:'Panic',       newState:'Follow',      triggerEvent:'HostageFreed',    timestamp:110 }
        ],
        hostageHistory: [
          { hostageId:'hostage_01', timestamp:0,   state:'Calm',   triggerEvent:'SessionStart', distressScore:0 },
          { hostageId:'hostage_01', timestamp:44,  state:'Fearful',triggerEvent:'GunshotHeard', distressScore:0.33 },
          { hostageId:'hostage_01', timestamp:58,  state:'Follow', triggerEvent:'HostageFreed', distressScore:0.1 },
          { hostageId:'hostage_02', timestamp:0,   state:'Calm',   triggerEvent:'SessionStart', distressScore:0 },
          { hostageId:'hostage_02', timestamp:75,  state:'Fearful',triggerEvent:'RadioAlert',   distressScore:0.33 },
          { hostageId:'hostage_02', timestamp:78,  state:'Panic',  triggerEvent:'ShotFired',    distressScore:0.66 },
          { hostageId:'hostage_02', timestamp:110, state:'Follow', triggerEvent:'HostageFreed', distressScore:0.1 }
        ],
        incidents: [
          { incidentId:'i1-01', incidentType:'FirstContact',        timestamp:12,  description:'Terrorist 01 spotted trainee in Hallway.',              involvedActors:['terrorist_01','trainee_01'], severity:'low' },
          { incidentId:'i1-02', incidentType:'FirstShot',           timestamp:15,  description:'First shot fired by trainee in Hallway.',                involvedActors:['trainee_01'],                severity:'low' },
          { incidentId:'i1-03', incidentType:'AlertCascade',        timestamp:16,  description:'3 NPCs entered Alert within 5 s of opening shots.',      involvedActors:['terrorist_01','terrorist_02'], severity:'medium' },
          { incidentId:'i1-04', incidentType:'TerroristNeutralized',timestamp:22,  description:'Terrorist 01 neutralized in Hallway.',                   involvedActors:['terrorist_01'],              severity:'low' },
          { incidentId:'i1-05', incidentType:'TerroristNeutralized',timestamp:52,  description:'Terrorist 02 neutralized in Room2.',                     involvedActors:['terrorist_02'],              severity:'low' },
          { incidentId:'i1-06', incidentType:'HostageRescued',      timestamp:58,  description:'Hostage 01 following trainee.',                          involvedActors:['hostage_01'],                severity:'low' },
          { incidentId:'i1-07', incidentType:'HostageEndangered',   timestamp:78,  description:'Hostage 02 entered Panic state during Room5 engagement.', involvedActors:['hostage_02'],                severity:'high' },
          { incidentId:'i1-08', incidentType:'TerroristNeutralized',timestamp:90,  description:'Terrorist 03 neutralized in Room5.',                     involvedActors:['terrorist_03'],              severity:'low' },
          { incidentId:'i1-09', incidentType:'HostageRescued',      timestamp:110, description:'Hostage 02 following trainee.',                          involvedActors:['hostage_02'],                severity:'low' },
          { incidentId:'i1-10', incidentType:'MissionEnd',          timestamp:134, description:'Mission completed. All hostages recovered.',             involvedActors:['trainee_01'],                severity:'low' }
        ]
      },

      // ─────────────────────────────────────────────────────────────
      // Session 2 — SUCCESS, Linear, Medium difficulty
      // ─────────────────────────────────────────────────────────────
      {
        sessionId:       'SEN-2026-0046',
        scenarioId:      'LINEAR-2F-MED',
        timestamp:       new Date('2026-04-21T14:45:00Z'),
        performance: {
          safetyScore:      0.75,
          accuracyScore:    0.68,
          speedScore:       0.67,
          overallScore:     0.71,
          missionSuccess:   true,
          totalShots:       12,
          hits:             8,
          misses:           4,
          hostagesSaved:    1,
          hostagesTotal:    2,
          missionDuration:  98,
          friendlyFireCount:0
        },
        cognitiveSummary: {
          averageReactionTime:     0.91,
          peakReactionTime:        1.82,
          movementInitiationScore: 0.61,
          stabilityScore:          0.65,
          attentionScore:          0.58,
          estimatedCognitiveLoad:  'High',
          estimatedStressLevel:    'High'
        },
        scenarioConfig: {
          topology:          'Linear',
          difficulty:        3,
          floors:            2,
          rooms:             4,
          hostagesTotal:     2,
          terroristsTotal:   2,
          placementStrategy: 'Spread',
          seed:              '112847'
        },
        events: [
          { eventType:'DoorOpened',           timestamp:5,  roomId:'Entry',       sourceActorId:'trainee_01', tags:[] },
          { eventType:'RoomBreached',         timestamp:10, roomId:'Corridor',    sourceActorId:'trainee_01', tags:[] },
          { eventType:'PlayerSeen',           timestamp:18, roomId:'Corridor',    sourceActorId:'terrorist_01', targetActorId:'trainee_01', tags:[] },
          { eventType:'ShotFired',            timestamp:22, roomId:'Corridor',    sourceActorId:'trainee_01', tags:[] },
          { eventType:'GunshotHeard',         timestamp:23, roomId:'StoreRoom',   sourceActorId:'terrorist_02', tags:[] },
          { eventType:'TerroristHit',         timestamp:25, roomId:'Corridor',    targetActorId:'terrorist_01', tags:[] },
          { eventType:'ShotFired',            timestamp:28, roomId:'Corridor',    sourceActorId:'trainee_01', tags:[] },
          { eventType:'TerroristDown',        timestamp:30, roomId:'Corridor',    targetActorId:'terrorist_01', tags:[] },
          { eventType:'DoorOpened',           timestamp:38, roomId:'HoldingArea', sourceActorId:'trainee_01', tags:[] },
          { eventType:'HostageContactStarted',timestamp:42, roomId:'HoldingArea', targetActorId:'hostage_01', tags:[] },
          { eventType:'RoomBreached',         timestamp:55, roomId:'StoreRoom',   sourceActorId:'trainee_01', tags:[] },
          { eventType:'PlayerSeen',           timestamp:60, roomId:'StoreRoom',   sourceActorId:'terrorist_02', targetActorId:'trainee_01', tags:[] },
          { eventType:'ShotFired',            timestamp:64, roomId:'StoreRoom',   sourceActorId:'trainee_01', tags:[] },
          { eventType:'TerroristHit',         timestamp:66, roomId:'StoreRoom',   targetActorId:'terrorist_02', tags:[] },
          { eventType:'ShotFired',            timestamp:70, roomId:'StoreRoom',   sourceActorId:'trainee_01', tags:[] },
          { eventType:'TerroristHit',         timestamp:72, roomId:'StoreRoom',   targetActorId:'terrorist_02', tags:[] },
          { eventType:'TerroristDown',        timestamp:75, roomId:'StoreRoom',   targetActorId:'terrorist_02', tags:[] },
          { eventType:'HostageFreed',         timestamp:80, roomId:'HoldingArea', targetActorId:'hostage_01', tags:[] }
        ],
        npcStateChanges: [
          { actorId:'terrorist_01', actorType:'Terrorist', previousState:'Idle',       newState:'Suspicious',  triggerEvent:'Footsteps',      timestamp:15 },
          { actorId:'terrorist_01', actorType:'Terrorist', previousState:'Suspicious', newState:'Alert',       triggerEvent:'PlayerSeen',     timestamp:18 },
          { actorId:'terrorist_01', actorType:'Terrorist', previousState:'Alert',      newState:'Engage',      triggerEvent:'ShotFired',      timestamp:22 },
          { actorId:'terrorist_01', actorType:'Terrorist', previousState:'Engage',     newState:'Neutralized', triggerEvent:'TerroristDown',  timestamp:30 },
          { actorId:'terrorist_02', actorType:'Terrorist', previousState:'Idle',       newState:'Alert',       triggerEvent:'GunshotHeard',   timestamp:23 },
          { actorId:'terrorist_02', actorType:'Terrorist', previousState:'Alert',      newState:'Engage',      triggerEvent:'PlayerSeen',     timestamp:60 },
          { actorId:'terrorist_02', actorType:'Terrorist', previousState:'Engage',     newState:'Neutralized', triggerEvent:'TerroristDown',  timestamp:75 },
          { actorId:'hostage_01',   actorType:'Hostage',   previousState:'Calm',       newState:'Fearful',     triggerEvent:'GunshotHeard',   timestamp:23 },
          { actorId:'hostage_01',   actorType:'Hostage',   previousState:'Fearful',    newState:'Follow',      triggerEvent:'HostageFreed',   timestamp:80 },
          { actorId:'hostage_02',   actorType:'Hostage',   previousState:'Calm',       newState:'Fearful',     triggerEvent:'GunshotHeard',   timestamp:23 },
          { actorId:'hostage_02',   actorType:'Hostage',   previousState:'Fearful',    newState:'Panic',       triggerEvent:'NearbyGunfire',  timestamp:64 }
        ],
        hostageHistory: [
          { hostageId:'hostage_01', timestamp:0,  state:'Calm',   triggerEvent:'SessionStart', distressScore:0 },
          { hostageId:'hostage_01', timestamp:23, state:'Fearful',triggerEvent:'GunshotHeard', distressScore:0.33 },
          { hostageId:'hostage_01', timestamp:80, state:'Follow', triggerEvent:'HostageFreed', distressScore:0.1 },
          { hostageId:'hostage_02', timestamp:0,  state:'Calm',   triggerEvent:'SessionStart', distressScore:0 },
          { hostageId:'hostage_02', timestamp:23, state:'Fearful',triggerEvent:'GunshotHeard', distressScore:0.33 },
          { hostageId:'hostage_02', timestamp:64, state:'Panic',  triggerEvent:'NearbyGunfire',distressScore:0.66 }
        ],
        incidents: [
          { incidentId:'i2-01', incidentType:'FirstContact',        timestamp:18, description:'Terrorist 01 spotted trainee in Corridor.',       involvedActors:['terrorist_01','trainee_01'], severity:'low' },
          { incidentId:'i2-02', incidentType:'FirstShot',           timestamp:22, description:'First shot fired in Corridor.',                    involvedActors:['trainee_01'],                severity:'low' },
          { incidentId:'i2-03', incidentType:'TerroristNeutralized',timestamp:30, description:'Terrorist 01 neutralized in Corridor.',            involvedActors:['terrorist_01'],              severity:'low' },
          { incidentId:'i2-04', incidentType:'HostageEndangered',   timestamp:64, description:'Hostage 02 entered Panic during StoreRoom fight.',  involvedActors:['hostage_02'],                severity:'high' },
          { incidentId:'i2-05', incidentType:'TerroristNeutralized',timestamp:75, description:'Terrorist 02 neutralized in StoreRoom.',           involvedActors:['terrorist_02'],              severity:'low' },
          { incidentId:'i2-06', incidentType:'HostageRescued',      timestamp:80, description:'Hostage 01 following trainee.',                    involvedActors:['hostage_01'],                severity:'low' },
          { incidentId:'i2-07', incidentType:'MissionEnd',          timestamp:98, description:'Mission ended. Hostage 02 not recovered.',         involvedActors:['trainee_01'],                severity:'medium' }
        ]
      },

      // ─────────────────────────────────────────────────────────────
      // Session 3 — FAILED, HubAndSpoke, Max difficulty
      // ─────────────────────────────────────────────────────────────
      {
        sessionId:       'SEN-2026-0047',
        scenarioId:      'HUB-4F-HIGH',
        timestamp:       new Date('2026-04-22T11:10:00Z'),
        performance: {
          safetyScore:      0.3,
          accuracyScore:    0.51,
          speedScore:       0.78,
          overallScore:     0.42,
          missionSuccess:   false,
          totalShots:       21,
          hits:             11,
          misses:           10,
          hostagesSaved:    0,
          hostagesTotal:    2,
          missionDuration:  67,
          friendlyFireCount:1
        },
        cognitiveSummary: {
          averageReactionTime:     1.21,
          peakReactionTime:        2.34,
          movementInitiationScore: 0.44,
          stabilityScore:          0.38,
          attentionScore:          0.41,
          estimatedCognitiveLoad:  'High',
          estimatedStressLevel:    'High'
        },
        scenarioConfig: {
          topology:          'HubAndSpoke',
          difficulty:        5,
          floors:            4,
          rooms:             8,
          hostagesTotal:     2,
          terroristsTotal:   4,
          placementStrategy: 'Clustered',
          seed:              '773621'
        },
        events: [
          { eventType:'DoorOpened',   timestamp:4,  roomId:'MainHub',    sourceActorId:'trainee_01', tags:[] },
          { eventType:'PlayerSeen',   timestamp:9,  roomId:'MainHub',    sourceActorId:'terrorist_01', targetActorId:'trainee_01', tags:[] },
          { eventType:'ShotFired',    timestamp:11, roomId:'MainHub',    sourceActorId:'trainee_01', tags:[] },
          { eventType:'GunshotHeard', timestamp:11, roomId:'UpperHub',   sourceActorId:'terrorist_02', tags:[] },
          { eventType:'TerroristHit', timestamp:13, roomId:'MainHub',    targetActorId:'terrorist_01', tags:[] },
          { eventType:'ShotFired',    timestamp:16, roomId:'MainHub',    sourceActorId:'trainee_01', tags:[] },
          { eventType:'TerroristDown',timestamp:18, roomId:'MainHub',    targetActorId:'terrorist_01', tags:[] },
          { eventType:'DoorOpened',   timestamp:22, roomId:'UpperHub',   sourceActorId:'trainee_01', tags:[] },
          { eventType:'PlayerSeen',   timestamp:25, roomId:'UpperHub',   sourceActorId:'terrorist_02', targetActorId:'trainee_01', tags:[] },
          { eventType:'ShotFired',    timestamp:27, roomId:'UpperHub',   sourceActorId:'trainee_01', tags:['friendly_fire'] },
          { eventType:'ShotFired',    timestamp:29, roomId:'UpperHub',   sourceActorId:'trainee_01', tags:[] },
          { eventType:'ShotFired',    timestamp:31, roomId:'UpperHub',   sourceActorId:'trainee_01', tags:[] },
          { eventType:'TerroristHit', timestamp:33, roomId:'UpperHub',   targetActorId:'terrorist_02', tags:[] },
          { eventType:'TerroristDown',timestamp:35, roomId:'UpperHub',   targetActorId:'terrorist_02', tags:[] },
          { eventType:'RoomBreached', timestamp:38, roomId:'HoldingCell',sourceActorId:'trainee_01', tags:[] },
          { eventType:'PlayerSeen',   timestamp:42, roomId:'HoldingCell',sourceActorId:'terrorist_03', targetActorId:'trainee_01', tags:[] },
          { eventType:'ShotFired',    timestamp:44, roomId:'HoldingCell',sourceActorId:'trainee_01', tags:[] },
          { eventType:'ShotFired',    timestamp:46, roomId:'HoldingCell',sourceActorId:'trainee_01', tags:[] },
          { eventType:'ShotFired',    timestamp:48, roomId:'HoldingCell',sourceActorId:'trainee_01', tags:[] },
          { eventType:'PlayerSeen',   timestamp:50, roomId:'HoldingCell',sourceActorId:'terrorist_04', targetActorId:'trainee_01', tags:[] },
          { eventType:'ShotFired',    timestamp:52, roomId:'HoldingCell',sourceActorId:'trainee_01', tags:[] },
          { eventType:'ShotFired',    timestamp:54, roomId:'HoldingCell',sourceActorId:'trainee_01', tags:[] },
          { eventType:'AllyDownSeen', timestamp:57, roomId:'HoldingCell',sourceActorId:'trainee_01', tags:[] }
        ],
        npcStateChanges: [
          { actorId:'terrorist_01', actorType:'Terrorist', previousState:'Idle',    newState:'Alert',       triggerEvent:'PlayerSeen',     timestamp:9 },
          { actorId:'terrorist_01', actorType:'Terrorist', previousState:'Alert',   newState:'Engage',      triggerEvent:'ShotFired',      timestamp:11 },
          { actorId:'terrorist_01', actorType:'Terrorist', previousState:'Engage',  newState:'Neutralized', triggerEvent:'TerroristDown',  timestamp:18 },
          { actorId:'terrorist_02', actorType:'Terrorist', previousState:'Idle',    newState:'Alert',       triggerEvent:'GunshotHeard',   timestamp:11 },
          { actorId:'terrorist_02', actorType:'Terrorist', previousState:'Alert',   newState:'Engage',      triggerEvent:'PlayerSeen',     timestamp:25 },
          { actorId:'terrorist_02', actorType:'Terrorist', previousState:'Engage',  newState:'Neutralized', triggerEvent:'TerroristDown',  timestamp:35 },
          { actorId:'terrorist_03', actorType:'Terrorist', previousState:'Idle',    newState:'Alert',       triggerEvent:'GunshotHeard',   timestamp:16 },
          { actorId:'terrorist_03', actorType:'Terrorist', previousState:'Alert',   newState:'Engage',      triggerEvent:'PlayerSeen',     timestamp:42 },
          { actorId:'terrorist_04', actorType:'Terrorist', previousState:'Idle',    newState:'Alert',       triggerEvent:'GunshotHeard',   timestamp:29 },
          { actorId:'terrorist_04', actorType:'Terrorist', previousState:'Alert',   newState:'Engage',      triggerEvent:'PlayerSeen',     timestamp:50 },
          { actorId:'hostage_01',   actorType:'Hostage',   previousState:'Calm',    newState:'Fearful',     triggerEvent:'GunshotHeard',   timestamp:11 },
          { actorId:'hostage_01',   actorType:'Hostage',   previousState:'Fearful', newState:'Panic',       triggerEvent:'NearbyGunfire',  timestamp:27 },
          { actorId:'hostage_01',   actorType:'Hostage',   previousState:'Panic',   newState:'Freeze',      triggerEvent:'Overwhelmed',    timestamp:44 },
          { actorId:'hostage_02',   actorType:'Hostage',   previousState:'Calm',    newState:'Fearful',     triggerEvent:'GunshotHeard',   timestamp:11 },
          { actorId:'hostage_02',   actorType:'Hostage',   previousState:'Fearful', newState:'Panic',       triggerEvent:'FriendlyFire',   timestamp:27 }
        ],
        hostageHistory: [
          { hostageId:'hostage_01', timestamp:0,  state:'Calm',   triggerEvent:'SessionStart',  distressScore:0 },
          { hostageId:'hostage_01', timestamp:11, state:'Fearful',triggerEvent:'GunshotHeard',  distressScore:0.33 },
          { hostageId:'hostage_01', timestamp:27, state:'Panic',  triggerEvent:'NearbyGunfire', distressScore:0.66 },
          { hostageId:'hostage_01', timestamp:44, state:'Freeze', triggerEvent:'Overwhelmed',   distressScore:0.72 },
          { hostageId:'hostage_02', timestamp:0,  state:'Calm',   triggerEvent:'SessionStart',  distressScore:0 },
          { hostageId:'hostage_02', timestamp:11, state:'Fearful',triggerEvent:'GunshotHeard',  distressScore:0.33 },
          { hostageId:'hostage_02', timestamp:27, state:'Panic',  triggerEvent:'FriendlyFire',  distressScore:0.66 }
        ],
        incidents: [
          { incidentId:'i3-01', incidentType:'FirstContact',       timestamp:9,  description:'Immediate hostile contact entering MainHub.',                  involvedActors:['terrorist_01','trainee_01'],                severity:'low' },
          { incidentId:'i3-02', incidentType:'AlertCascade',       timestamp:11, description:'3+ NPCs entered Alert state within 5 s of first shots.',        involvedActors:['terrorist_01','terrorist_02','terrorist_03'], severity:'medium' },
          { incidentId:'i3-03', incidentType:'HostageEndangered',  timestamp:27, description:'Friendly fire — Hostage 02 entered Panic state.',               involvedActors:['hostage_02','trainee_01'],                   severity:'high' },
          { incidentId:'i3-04', incidentType:'HostageEndangered',  timestamp:27, description:'Hostage 01 entered Panic due to nearby gunfire.',               involvedActors:['hostage_01'],                               severity:'high' },
          { incidentId:'i3-05', incidentType:'TerroristNeutralized',timestamp:18,'description':'Terrorist 01 neutralized in MainHub.',                        involvedActors:['terrorist_01'],                             severity:'low' },
          { incidentId:'i3-06', incidentType:'TerroristNeutralized',timestamp:35,'description':'Terrorist 02 neutralized in UpperHub.',                       involvedActors:['terrorist_02'],                             severity:'low' },
          { incidentId:'i3-07', incidentType:'HostageEndangered',  timestamp:44, description:'Hostage 01 frozen — unresponsive to trainee.',                  involvedActors:['hostage_01'],                               severity:'high' },
          { incidentId:'i3-08', incidentType:'MissionEnd',         timestamp:67, description:'Mission failed. Trainee overwhelmed. Hostages not recovered.',   involvedActors:['trainee_01'],                               severity:'high' }
        ]
      }
    ]

    // Attach completed SIM-TLX questionnaires to every seed session. Besides
    // making the workload charts demo-able, this keeps seeded sessions out of
    // the pending-simtlx poll (a fresh session without simTlx auto-redirects
    // the dashboard into the questionnaire).
    const seedRatings = {
      // success, medium difficulty — moderate, balanced load
      'SEN-2026-0045': { mentalDemands: 11, physicalDemands: 8,  temporalDemands: 9,  frustration: 6,  taskComplexity: 10, situationalStress: 9,  distraction: 5, perceptualStrain: 4, taskControl: 5 },
      // smoother run — lighter load
      'SEN-2026-0046': { mentalDemands: 7,  physicalDemands: 6,  temporalDemands: 5,  frustration: 3,  taskComplexity: 6,  situationalStress: 5,  distraction: 3, perceptualStrain: 3, taskControl: 4 },
      // failed mission, trainee overwhelmed — heavy load, high frustration
      'SEN-2026-0047': { mentalDemands: 17, physicalDemands: 12, temporalDemands: 16, frustration: 18, taskComplexity: 15, situationalStress: 17, distraction: 12, perceptualStrain: 8, taskControl: 10 }
    }
    for (const s of sessions) {
      const ratings = seedRatings[s.sessionId]
      if (!ratings) continue
      s.simTlx = {
        completedAt: new Date(new Date(s.timestamp).getTime() + 5 * 60 * 1000),
        ratings,
        derived: computeSimTlxDerived(ratings)
      }
    }

    try {
      await Session.insertMany(sessions, { ordered: false })
    } catch (err) {
      // BulkWriteError code 11000 = duplicate key — acceptable if re-seeding
      const isDupOnly = err.code === 11000 ||
        (err.writeErrors && err.writeErrors.every(e => e.code === 11000))
      if (!isDupOnly) throw err
    }

    return NextResponse.json(
      { success: true, message: 'Demo data seeded (or already present)' },
      { headers: corsHeaders }
    )
  } catch (err) {
    console.error('[POST /api/sessions/seed]', err)
    return NextResponse.json(
      { success: false, error: err.message },
      { status: 500, headers: corsHeaders }
    )
  }
}
