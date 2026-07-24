import { notFound } from 'next/navigation'
import SimTlxClient from '@/components/simtlx/SimTlxClient'

async function fetchSession(id) {
  const base = process.env.NEXT_PUBLIC_APP_URL || 'http://localhost:3000'
  try {
    const res = await fetch(`${base}/api/sessions/${id}`, { cache: 'no-store' })
    if (!res.ok) return null
    return res.json()
  } catch {
    return null
  }
}

export async function generateMetadata({ params }) {
  return { title: `Workload Assessment — ${params.id} | Sentinels` }
}

export default async function SimTlxPage({ params }) {
  const session = await fetchSession(params.id)
  if (!session || session.error) notFound()

  return <SimTlxClient session={session} />
}
