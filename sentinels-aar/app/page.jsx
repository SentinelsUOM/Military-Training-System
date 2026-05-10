import HomeClient from '@/components/HomeClient'

async function fetchSessions() {
  const base = process.env.NEXT_PUBLIC_APP_URL || 'http://localhost:3000'
  try {
    const res = await fetch(`${base}/api/sessions?page=1&limit=20`, {
      cache: 'no-store'
    })
    if (!res.ok) return { sessions: [], total: 0 }
    return res.json()
  } catch {
    return { sessions: [], total: 0 }
  }
}

async function fetchStats() {
  const base = process.env.NEXT_PUBLIC_APP_URL || 'http://localhost:3000'
  try {
    const res = await fetch(`${base}/api/stats`, { cache: 'no-store' })
    if (!res.ok) return null
    return res.json()
  } catch {
    return null
  }
}

export default async function HomePage() {
  const [{ sessions, total }, stats] = await Promise.all([
    fetchSessions(),
    fetchStats()
  ])

  return (
    <HomeClient
      initialSessions={sessions || []}
      initialStats={stats}
      total={total || 0}
    />
  )
}
