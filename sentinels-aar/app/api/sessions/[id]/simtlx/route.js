import { NextResponse } from 'next/server'
import { connectDB } from '@/lib/mongodb'
import Session from '@/lib/models/Session'
import { computeSimTlxDerived, SIM_TLX_KEYS } from '@/lib/simTlx'

const corsHeaders = {
  'Access-Control-Allow-Origin': '*',
  'Access-Control-Allow-Methods': 'GET, POST, OPTIONS',
  'Access-Control-Allow-Headers': 'Content-Type'
}

export async function OPTIONS() {
  return NextResponse.json({}, { headers: corsHeaders })
}

// Saves the completed questionnaire. Body: { ratings: { <dimension>: 0-20 ×9 } }.
// Derived composites are always computed server-side so stored data cannot
// disagree with the ratings it came from.
export async function POST(request, { params }) {
  try {
    await connectDB()
    const body = await request.json()

    const ratings = {}
    for (const key of SIM_TLX_KEYS) ratings[key] = Number(body?.ratings?.[key])

    let derived
    try {
      derived = computeSimTlxDerived(ratings)
    } catch (validationErr) {
      return NextResponse.json(
        { success: false, error: validationErr.message },
        { status: 400, headers: corsHeaders }
      )
    }

    const session = await Session.findOneAndUpdate(
      { sessionId: params.id },
      { $set: { simTlx: { completedAt: new Date(), ratings, derived } } },
      { new: true, runValidators: true }
    )

    if (!session) {
      return NextResponse.json(
        { success: false, error: 'Session not found' },
        { status: 404, headers: corsHeaders }
      )
    }

    return NextResponse.json(
      { success: true, sessionId: session.sessionId, simTlx: session.simTlx },
      { headers: corsHeaders }
    )
  } catch (err) {
    console.error('[POST /api/sessions/:id/simtlx]', err)
    return NextResponse.json(
      { success: false, error: err.message },
      { status: 500, headers: corsHeaders }
    )
  }
}

export async function GET(request, { params }) {
  try {
    await connectDB()
    const session = await Session.findOne({ sessionId: params.id })
      .select('sessionId simTlx performance.missionSuccess')
      .lean()

    if (!session) {
      return NextResponse.json(
        { error: 'Session not found' },
        { status: 404, headers: corsHeaders }
      )
    }

    return NextResponse.json(
      { sessionId: session.sessionId, simTlx: session.simTlx || null },
      { headers: corsHeaders }
    )
  } catch (err) {
    console.error('[GET /api/sessions/:id/simtlx]', err)
    return NextResponse.json(
      { error: err.message },
      { status: 500, headers: corsHeaders }
    )
  }
}
