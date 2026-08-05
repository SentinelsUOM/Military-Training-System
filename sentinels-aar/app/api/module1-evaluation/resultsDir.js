import path from 'path'

// Where the Module 1 study output lives. Shared by the data route and the figure
// route so the two can never disagree about which results directory is being read.
//
// The batch runner writes into the Unity project, which sits one level above the Next
// app. Overridable so the dashboard can be pointed at an exported copy of the results
// (or run from outside the Unity repo) without editing code.
export const RESULTS_DIR = process.env.MODULE1_RESULTS_DIR
  || path.join(process.cwd(), '..', 'Assets', 'Module1_DataModels_and_IO', 'Output', 'EvaluationResults')

export const FIGURES_SUBDIR = 'figures'
export const TABLES_SUBDIR = 'tables'
