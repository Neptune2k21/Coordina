export type AuthMode = "login" | "register"

export type CurrentUser = {
  id: string
  email: string
  name: string
}

export type AuthSession = {
  accessToken: string
  expiresAt: string
  user: CurrentUser
}

export type AuthFormValues = {
  name: string
  email: string
  password: string
}
