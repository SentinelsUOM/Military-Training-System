import fs from 'fs/promises'
import path from 'path'
import { FIGURE_FILES } from '@/lib/module1Results'
import { RESULTS_DIR, FIGURES_SUBDIR } from '../../resultsDir'

export const dynamic = 'force-dynamic'

// Serves one published study figure (PNG) out of the Unity results directory.
//
// The figures live outside the Next app, next to the CSVs the sibling route reads, so
// they can't be served from public/ — they'd have to be copied, and a copy silently
// goes stale the moment the study is re-run.
//
// SECURITY: the requested name is matched against FIGURE_FILES by exact equality and
// the matched entry (never the user's string) is what gets joined onto the path. A
// request for "../../../.env" simply fails the allowlist. Decoding first matters —
// without it "%2e%2e%2f" would arrive as a literal that no allowlist entry equals,
// which is safe, but the allowlist is what guarantees that, not the encoding.
export async function GET(_request, { params }) {
  let requested
  try {
    requested = decodeURIComponent(params.file)
  } catch {
    return new Response('Bad figure name', { status: 400 })
  }

  const match = FIGURE_FILES.find(f => f === requested)
  if (!match) return new Response('Unknown figure', { status: 404 })

  try {
    const body = await fs.readFile(path.join(RESULTS_DIR, FIGURES_SUBDIR, match))
    return new Response(body, {
      headers: {
        'Content-Type': 'image/png',
        'Content-Length': String(body.length),
        // Short cache: the figures only change when the study is re-run, but when that
        // happens the page should pick them up without a hard refresh.
        'Cache-Control': 'private, max-age=60, must-revalidate',
      },
    })
  } catch (err) {
    if (err.code === 'ENOENT') return new Response('Figure not committed', { status: 404 })
    console.error('[GET /api/module1-evaluation/figure]', err)
    return new Response('Could not read figure', { status: 500 })
  }
}
