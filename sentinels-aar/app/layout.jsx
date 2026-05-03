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

export default function RootLayout({ children }) {
  return (
    <html lang="en" className={rajdhani.variable}>
      <body>{children}</body>
    </html>
  )
}
