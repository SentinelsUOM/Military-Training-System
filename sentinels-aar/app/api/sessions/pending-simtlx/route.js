import { NextResponse } from 'next/server'
import { connectDB } from '@/lib/mongodb'
import Session from '@/lib/models/Session'

const corsHeaders = {
  'Access-Control-Allow-Origin': '*',
  'Access-Control-Allow-Methods': 'GET, OPTIONS',
  'Access-Control-Allow-Headers': 'Content-Type'
}

// A session only counts as "pending" while it is fresh — old sessions that were
// never rated should not hijack the dashboard weeks later.
const PENDING_WINDOW_MS = 30 * 60 * 1000

export async function OPTIONS() {
  return NextResponse.json({}, { headers: corsHeaders })
}

// Returns the newest recently-uploaded session that has no SIM-TLX yet.
// The dashboard home page polls this to auto-redirect the trainee into the
// questionnaire right after a mission completes or fails.
export async function GET() {
  try {
    await connectDB()

    const cutoff = new Date(Date.now() - PENDING_WINDOW_MS)
    const session = await Session.findOne({
      createdAt: { $gte: cutoff },
      'simTlx.completedAt': { $exists: false }
    })
      .sort({ _id: -1 })
      .select('sessionId createdAt performance.missionSuccess')
      .lean()

    return NextResponse.json(
      session
        ? {
            sessionId: session.sessionId,
            createdAt: session.createdAt,
            missionSuccess: session.performance?.missionSuccess ?? null
          }
        : { sessionId: null },
      { headers: corsHeaders }
    )
  } catch (err) {
    console.error('[GET /api/sessions/pending-simtlx]', err)
    return NextResponse.json(
      { error: err.message },
      { status: 500, headers: corsHeaders }
    )
  }
}
