import Link from 'next/link'

export default function NotFound() {
  return (
    <div style={{
      minHeight: '100vh',
      display: 'flex',
      flexDirection: 'column',
      alignItems: 'center',
      justifyContent: 'center',
      gap: '1rem',
      textAlign: 'center',
      padding: '2rem'
    }}>
      <div style={{
        fontFamily: 'var(--font-display)',
        fontSize: '4rem',
        fontWeight: 600,
        color: 'var(--accent)',
        letterSpacing: '0.1em'
      }}>404</div>
      <div style={{ fontSize: '1.1rem', color: 'var(--muted)' }}>
        Session or page not found
      </div>
      <Link href="/" style={{
        marginTop: '1rem',
        padding: '8px 20px',
        background: 'var(--accent)',
        color: '#fff',
        borderRadius: 'var(--radius)',
        fontWeight: 600,
        fontSize: '0.9rem'
      }}>
        ← Back to Sessions
      </Link>
    </div>
  )
}
