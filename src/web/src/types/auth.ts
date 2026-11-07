import type { components } from "@/types/api-contract"

type Schemas = components["schemas"]

export type AuthMode = "login" | "register"

export type CurrentUser = Schemas["CurrentUserResponse"]
export type AuthSession = Schemas["AuthResponse"]

export type AuthFormValues = {
  name: string
  email: string
  password: string
}
