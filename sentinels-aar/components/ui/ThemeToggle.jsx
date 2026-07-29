'use client'
import { useTheme, applyTheme } from '@/lib/useTheme'
import styles from './ThemeToggle.module.css'

// Sun/moon pill toggle. Reads the live theme via useTheme() (kept in sync with the
// no-FOUC script in layout.jsx + any other toggle instance) and flips data-theme
// on <html> + persists to localStorage on click.
export default function ThemeToggle({ className = '' }) {
  const theme = useTheme()
  const isLight = theme === 'light'

  return (
    <button
      type="button"
      className={`${styles.toggle} ${className}`}
      onClick={() => applyTheme(isLight ? 'dark' : 'light')}
      aria-label={isLight ? 'Switch to dark mode' : 'Switch to light mode'}
      title={isLight ? 'Switch to dark mode' : 'Switch to light mode'}
    >
      <span className={`${styles.icon} ${isLight ? styles.hidden : ''}`} aria-hidden="true">
        {/* moon */}
        <svg width="15" height="15" viewBox="0 0 24 24" fill="none">
          <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79Z" fill="currentColor" />
        </svg>
      </span>
      <span className={`${styles.icon} ${isLight ? '' : styles.hidden}`} aria-hidden="true">
        {/* sun */}
        <svg width="15" height="15" viewBox="0 0 24 24" fill="none">
          <circle cx="12" cy="12" r="4.5" fill="currentColor" />
          <g stroke="currentColor" strokeWidth="1.8" strokeLinecap="round">
            <path d="M12 2v2.2M12 19.8V22M4.2 4.2l1.5 1.5M18.3 18.3l1.5 1.5M2 12h2.2M19.8 12H22M4.2 19.8l1.5-1.5M18.3 5.7l1.5-1.5" />
          </g>
        </svg>
      </span>
      <span className={styles.label}>{isLight ? 'Light' : 'Dark'}</span>
    </button>
  )
}
