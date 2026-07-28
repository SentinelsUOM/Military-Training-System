import { NextResponse } from 'next/server'
import { connectDB } from '@/lib/mongodb'
import Session from '@/lib/models/Session'
import { computeAiEvalDerived, AI_EVAL_SCALES } from '@/lib/aiEval'

const corsHeaders = {
  'Access-Control-Allow-Origin': '*',
  'Access-Control-Allow-Methods': 'GET, POST, OPTIONS',
  'Access-Control-Allow-Headers': 'Content-Type'
}

export async function OPTIONS() {
  return NextResponse.json({}, { headers: corsHeaders })
}

// Saves the completed Module-2 enemy-AI questionnaire (Survey 2).
// Body: { ratings: { <scaleKey>: { <itemKey>: number } } }.
// Per-sub-scale means are computed server-side so stored data can't disagree with input.
export async function POST(request, { params }) {
  try {
    await connectDB()
    const body = await request.json()

    // Rebuild ratings strictly from the known scale/item keys (ignore anything extra).
    const ratings = {}
    for (const scale of AI_EVAL_SCALES) {
      ratings[scale.key] = {}
      for (const item of scale.items) {
        ratings[scale.key][item.key] = Number(body?.ratings?.[scale.key]?.[item.key])
      }
    }

    let derived
    try {
      derived = computeAiEvalDerived(ratings)
    } catch (validationErr) {
      return NextResponse.json(
        { success: false, error: validationErr.message },
        { status: 400, headers: corsHeaders }
      )
    }

    const session = await Session.findOneAndUpdate(
      { sessionId: params.id },
      { $set: { aiEval: { completedAt: new Date(), ratings, derived } } },
      { new: true, runValidators: true }
    )

    if (!session) {
      return NextResponse.json(
        { success: false, error: 'Session not found' },
        { status: 404, headers: corsHeaders }
      )
    }

    return NextResponse.json(
      { success: true, sessionId: session.sessionId, aiEval: session.aiEval },
      { headers: corsHeaders }
    )
  } catch (err) {
    console.error('[POST /api/sessions/:id/aieval]', err)
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
      .select('sessionId aiEval playerId npcLevel performance.missionSuccess')
      .lean()

    if (!session) {
      return NextResponse.json(
        { error: 'Session not found' },
        { status: 404, headers: corsHeaders }
      )
    }

    return NextResponse.json(
      {
        sessionId: session.sessionId,
        playerId:  session.playerId || null,
        npcLevel:  session.npcLevel || null,
        aiEval:    session.aiEval || null
      },
      { headers: corsHeaders }
    )
  } catch (err) {
    console.error('[GET /api/sessions/:id/aieval]', err)
    return NextResponse.json(
      { error: err.message },
      { status: 500, headers: corsHeaders }
    )
  }
}
