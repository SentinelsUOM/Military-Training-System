import { NextResponse } from 'next/server'
import { connectDB } from '@/lib/mongodb'
import Session from '@/lib/models/Session'

const corsHeaders = {
  'Access-Control-Allow-Origin': '*',
  'Access-Control-Allow-Methods': 'GET, OPTIONS',
  'Access-Control-Allow-Headers': 'Content-Type'
}

export async function OPTIONS() {
  return NextResponse.json({}, { headers: corsHeaders })
}

export async function GET() {
  try {
    await connectDB()

    const [agg] = await Session.aggregate([
      {
        $group: {
          _id: null,
          totalSessions:       { $sum: 1 },
          averageOverallScore: { $avg: '$performance.overallScore' },
          averageSafetyScore:  { $avg: '$performance.safetyScore' },
          averageAccuracyScore:{ $avg: '$performance.accuracyScore' },
          // Speed + Operator Safety are Module 4's other two composite scores (see
          // PerformanceCalculator.cs) — computed per session already, just never
          // surfaced as a dashboard-wide average until now. Operator Safety is only
          // present on sessions recorded after that feature shipped; $avg silently
          // ignores missing/null values, same as averageWorkload does for SIM-TLX.
          averageSpeedScore:         { $avg: '$performance.speedScore' },
          averageOperatorSafetyScore:{ $avg: '$performance.operatorSafetyScore' },
          successCount:        { $sum: { $cond: ['$performance.missionSuccess', 1, 0] } },
          averageReactionTime: { $avg: '$cognitiveSummary.averageReactionTime' },
          // SIM-TLX aggregates — $avg ignores sessions without a questionnaire
          simTlxCount:               { $sum: { $cond: [{ $ifNull: ['$simTlx.completedAt', false] }, 1, 0] } },
          averageWorkload:           { $avg: '$simTlx.derived.overallWorkload' },
          averageMentalPhysical:     { $avg: '$simTlx.derived.mentalPhysical' },
          averageTemporalFrustration:{ $avg: '$simTlx.derived.temporalFrustration' },
          averageComplexityStress:   { $avg: '$simTlx.derived.complexityStress' }
        }
      },
      {
        $project: {
          _id: 0,
          totalSessions:       1,
          averageOverallScore: { $round: ['$averageOverallScore',  3] },
          averageSafetyScore:  { $round: ['$averageSafetyScore',   3] },
          averageAccuracyScore:{ $round: ['$averageAccuracyScore', 3] },
          averageSpeedScore:   { $round: ['$averageSpeedScore', 3] },
          averageOperatorSafetyScore: {
            $cond: [{ $eq: ['$averageOperatorSafetyScore', null] }, null, { $round: ['$averageOperatorSafetyScore', 3] }]
          },
          missionSuccessRate:  {
            $cond: [
              { $eq: ['$totalSessions', 0] },
              0,
              { $round: [{ $divide: ['$successCount', '$totalSessions'] }, 3] }
            ]
          },
          averageReactionTime: { $round: ['$averageReactionTime', 3] },
          simTlxCount:                1,
          averageWorkload:            { $round: ['$averageWorkload', 1] },
          averageMentalPhysical:      { $round: ['$averageMentalPhysical', 1] },
          averageTemporalFrustration: { $round: ['$averageTemporalFrustration', 1] },
          averageComplexityStress:    { $round: ['$averageComplexityStress', 1] }
        }
      }
    ])

    const stats = agg || {
      totalSessions: 0,
      averageOverallScore: 0,
      averageSafetyScore: 0,
      averageAccuracyScore: 0,
      averageSpeedScore: 0,
      averageOperatorSafetyScore: null,
      missionSuccessRate: 0,
      averageReactionTime: 0,
      simTlxCount: 0,
      averageWorkload: null,
      averageMentalPhysical: null,
      averageTemporalFrustration: null,
      averageComplexityStress: null
    }

    return NextResponse.json(stats, { headers: corsHeaders })
  } catch (err) {
    console.error('[GET /api/stats]', err)
    return NextResponse.json({ error: err.message }, { status: 500, headers: corsHeaders })
  }
}
