import { NextResponse } from 'next/server'
import zlib from 'zlib'
import { promisify } from 'util'
import { connectDB } from '@/lib/mongodb'
import Session from '@/lib/models/Session'

const gunzip = promisify(zlib.gunzip)

const corsHeaders = {
  'Access-Control-Allow-Origin': '*',
  'Access-Control-Allow-Methods': 'GET, POST, OPTIONS',
  'Access-Control-Allow-Headers': 'Content-Type, Content-Encoding'
}

export async function OPTIONS() {
  return NextResponse.json({}, { headers: corsHeaders })
}

// Read the JSON body, transparently gunzipping it when the client sent
// `Content-Encoding: gzip` (Unity's DashboardUploader compresses large session
// payloads so they transmit reliably). Falls back to plain parsing so existing
// non-gzip clients keep working, and tolerates an upstream proxy having already
// decompressed the body.
async function readJsonBody(request) {
  const enc = (request.headers.get('content-encoding') || '').toLowerCase()
  const raw = Buffer.from(await request.arrayBuffer())
  if (enc.includes('gzip')) {
    try {
      return JSON.parse((await gunzip(raw)).toString('utf-8'))
    } catch {
      // Already-decompressed upstream — parse the raw bytes as text.
      return JSON.parse(raw.toString('utf-8'))
    }
  }
  return JSON.parse(raw.toString('utf-8'))
}

export async function POST(request) {
  try {
    await connectDB()
    const body = await readJsonBody(request)

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
