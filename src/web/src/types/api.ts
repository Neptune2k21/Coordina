export type ApiValidationErrors = Record<string, string[]>

export type ApiProblem = {
  title?: string
  message?: string
  errors?: ApiValidationErrors
}
