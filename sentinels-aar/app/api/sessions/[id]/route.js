import { NextResponse } from 'next/server'
import { connectDB } from '@/lib/mongodb'
import Session from '@/lib/models/Session'

const corsHeaders = {
  'Access-Control-Allow-Origin': '*',
  'Access-Control-Allow-Methods': 'GET, DELETE, OPTIONS',
  'Access-Control-Allow-Headers': 'Content-Type'
}

export async function OPTIONS() {
  return NextResponse.json({}, { headers: corsHeaders })
}

export async function GET(request, { params }) {
  try {
    await connectDB()
    const session = await Session.findOne({ sessionId: params.id }).lean()

    if (!session) {
      return NextResponse.json(
        { error: 'Session not found' },
        { status: 404, headers: corsHeaders }
      )
    }

    return NextResponse.json(session, { headers: corsHeaders })
  } catch (err) {
    console.error('[GET /api/sessions/:id]', err)
    return NextResponse.json(
      { error: err.message },
      { status: 500, headers: corsHeaders }
    )
  }
}

export async function DELETE(request, { params }) {
  try {
    await connectDB()
    const result = await Session.deleteOne({ sessionId: params.id })

    if (result.deletedCount === 0) {
      return NextResponse.json(
        { success: false, error: 'Session not found' },
        { status: 404, headers: corsHeaders }
      )
    }

    return NextResponse.json({ success: true }, { headers: corsHeaders })
  } catch (err) {
    console.error('[DELETE /api/sessions/:id]', err)
    return NextResponse.json(
      { success: false, error: err.message },
      { status: 500, headers: corsHeaders }
    )
  }
}
