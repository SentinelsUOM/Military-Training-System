import { NextResponse } from 'next/server'
import { connectDB } from '@/lib/mongodb'
import Session from '@/lib/models/Session'

const corsHeaders = {
  'Access-Control-Allow-Origin': '*',
  'Access-Control-Allow-Methods': 'GET, OPTIONS',
  'Access-Control-Allow-Headers': 'Content-Type'
}

// Only fresh sessions hijack the dashboard (mirrors pending-simtlx).
const PENDING_WINDOW_MS = 30 * 60 * 1000

export async function OPTIONS() {
  return NextResponse.json({}, { headers: corsHeaders })
}

// Returns the newest fresh session that is missing EITHER post-mission survey
// (SIM-TLX or the enemy-AI evaluation). The home page polls this to auto-redirect
// the trainee into the combined Post-Mission Survey wrapper.
export async function GET() {
  try {
    await connectDB()

    const cutoff = new Date(Date.now() - PENDING_WINDOW_MS)
    const session = await Session.findOne({
      createdAt: { $gte: cutoff },
      $or: [
        { 'simTlx.completedAt': { $exists: false } },
        { 'aiEval.completedAt': { $exists: false } }
      ]
    })
      .sort({ _id: -1 })
      .select('sessionId createdAt simTlx.completedAt aiEval.completedAt performance.missionSuccess')
      .lean()

    return NextResponse.json(
      session
        ? {
            sessionId: session.sessionId,
            createdAt: session.createdAt,
            tlxDone: Boolean(session.simTlx?.completedAt),
            aiEvalDone: Boolean(session.aiEval?.completedAt),
            missionSuccess: session.performance?.missionSuccess ?? null
          }
        : { sessionId: null },
      { headers: corsHeaders }
    )
  } catch (err) {
    console.error('[GET /api/sessions/pending-survey]', err)
    return NextResponse.json(
      { error: err.message },
      { status: 500, headers: corsHeaders }
    )
  }
}
