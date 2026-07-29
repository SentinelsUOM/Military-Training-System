import { Rajdhani } from 'next/font/google'
import './globals.css'

const rajdhani = Rajdhani({
  subsets: ['latin'],
  weight: ['400', '500', '600'],
  variable: '--font-rajdhani',
  display: 'swap'
})

export const metadata = {
  title: 'Sentinels AAR',
  description: 'After-Action Review Dashboard — Sentinels VR Military Training'
}

// Runs before first paint so the page never flashes the wrong theme: reads the
// saved preference (or falls back to the OS setting on a first visit) and stamps
// data-theme onto <html> before React hydrates. ThemeToggle re-applies it on click.
const THEME_INIT_SCRIPT = `(function(){try{var t=localStorage.getItem('sentinels-theme');if(!t){t=window.matchMedia('(prefers-color-scheme: light)').matches?'light':'dark';}document.documentElement.setAttribute('data-theme',t);}catch(e){}})();`

export default function RootLayout({ children }) {
  return (
    <html lang="en" className={rajdhani.variable}>
      <head>
        <script dangerouslySetInnerHTML={{ __html: THEME_INIT_SCRIPT }} />
      </head>
      <body>{children}</body>
    </html>
  )
}
