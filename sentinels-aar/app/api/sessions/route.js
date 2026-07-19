import { NextResponse } from 'next/server'
import { connectDB } from '@/lib/mongodb'
import Session from '@/lib/models/Session'

const corsHeaders = {
  'Access-Control-Allow-Origin': '*',
  'Access-Control-Allow-Methods': 'GET, POST, OPTIONS',
  'Access-Control-Allow-Headers': 'Content-Type'
}

export async function OPTIONS() {
  return NextResponse.json({}, { headers: corsHeaders })
}

export async function POST(request) {
  try {
    await connectDB()
    const body = await request.json()

    if (!body.sessionId || !body.scenarioId) {
      return NextResponse.json(
        { success: false, error: 'sessionId and scenarioId are required' },
        { status: 400, headers: corsHeaders }
      )
    }

    const session = await Session.findOneAndUpdate(
      { sessionId: body.sessionId },
      body,
      { upsert: true, new: true, runValidators: true }
    )

    return NextResponse.json(
      { success: true, sessionId: session.sessionId },
      { status: 200, headers: corsHeaders }
    )
  } catch (err) {
    console.error('[POST /api/sessions]', err)
    return NextResponse.json(
      { success: false, error: err.message },
      { status: 500, headers: corsHeaders }
    )
  }
}

export async function GET(request) {
  try {
    await connectDB()
    const { searchParams } = new URL(request.url)
    const page  = Math.max(1, parseInt(searchParams.get('page')  || '1'))
    const limit = Math.min(100, parseInt(searchParams.get('limit') || '20'))
    const skip  = (page - 1) * limit

    const [sessions, total] = await Promise.all([
      Session.find({})
        .select('-events -replayFrames -npcStateChanges -hostageHistory -incidents -movementTrack')
        // Sort by _id, not createdAt: _id is ALWAYS indexed (default index) and ObjectIds
        // are time-ordered, so this gives the same newest-first order but is served from an
        // index — it can never hit the 32MB in-memory-sort limit that createdAt (unindexed)
        // did. No migration / index build required.
        .sort({ _id: -1 })
        .allowDiskUse(true)
        .skip(skip)
        .limit(limit)
        .lean(),
      Session.countDocuments()
    ])

    return NextResponse.json({ sessions, total, page, limit }, { headers: corsHeaders })
  } catch (err) {
    console.error('[GET /api/sessions]', err)
    return NextResponse.json(
      { error: err.message },
      { status: 500, headers: corsHeaders }
    )
  }
}
