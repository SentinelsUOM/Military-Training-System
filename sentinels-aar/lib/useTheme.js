'use client'
import { useEffect, useState } from 'react'
import { getChartTheme } from '@/lib/utils'

export const THEME_STORAGE_KEY = 'sentinels-theme'

// Reads the current theme off <html data-theme="...">, kept in sync by ThemeToggle
// (which also dispatches 'sentinels-themechange' so every mounted consumer updates
// together, not just the tab that clicked the toggle).
export function useTheme() {
  const [theme, setTheme] = useState('dark')

  useEffect(() => {
    const read = () => setTheme(document.documentElement.getAttribute('data-theme') || 'dark')
    read()
    window.addEventListener('sentinels-themechange', read)
    return () => window.removeEventListener('sentinels-themechange', read)
  }, [])

  return theme
}

// Convenience wrapper for chart-heavy components: returns the live grid/label/
// tooltip/accent/colors object for Recharts props, updating on theme change.
export function useChartTheme() {
  const theme = useTheme()
  return getChartTheme(theme)
}

export function applyTheme(theme) {
  document.documentElement.setAttribute('data-theme', theme)
  try { localStorage.setItem(THEME_STORAGE_KEY, theme) } catch { /* storage unavailable */ }
  window.dispatchEvent(new Event('sentinels-themechange'))
}
