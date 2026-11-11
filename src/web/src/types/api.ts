export type ApiValidationErrors = Record<string, string[]>

export type ApiProblem = {
  title?: string
  /** What happened, written for the user, such as how long to wait after a 429. */
  detail?: string
  message?: string
  errors?: ApiValidationErrors
}
