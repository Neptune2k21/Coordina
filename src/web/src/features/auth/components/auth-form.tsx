import { ArrowRight, CircleNotch, ShieldCheck } from "@phosphor-icons/react"
import { zodResolver } from "@hookform/resolvers/zod"
import { useMemo } from "react"
import { useForm } from "react-hook-form"

import { Button } from "@/components/ui/button"
import { FieldError, FieldGroup } from "@/components/ui/field"
import { ApiError } from "@/features/auth/auth-api"
import { loginSchema, registerSchema } from "@/features/auth/auth-schemas"
import type {
  AuthFormValues,
  AuthMode,
  AuthSession,
} from "@/features/auth/auth-types"
import { AuthTextField } from "@/features/auth/components/auth-text-field"

type AuthFormProps = {
  mode: AuthMode
  isSubmitting: boolean
  onSubmit: (values: AuthFormValues) => Promise<AuthSession>
}

const defaultValues: AuthFormValues = {
  name: "",
  email: "",
  password: "",
}

export function AuthForm({ mode, isSubmitting, onSubmit }: AuthFormProps) {
  const schema = useMemo(
    () => (mode === "register" ? registerSchema : loginSchema),
    [mode]
  )
  const form = useForm<AuthFormValues>({
    resolver: zodResolver(schema),
    defaultValues,
    mode: "onTouched",
  })

  async function handleSubmit(values: AuthFormValues) {
    try {
      form.clearErrors("root")
      await onSubmit(values)
      form.reset(defaultValues)
    } catch (error) {
      if (error instanceof ApiError) {
        applyApiErrors(error)
        return
      }

      form.setError("root", {
        message: "We could not reach Coordina right now.",
      })
    }
  }

  function applyApiErrors(error: ApiError) {
    if (error.errors) {
      Object.entries(error.errors).forEach(([field, messages]) => {
        const fieldName = field.toLowerCase() as keyof AuthFormValues

        if (fieldName in defaultValues) {
          form.setError(fieldName, { message: messages[0] })
        }
      })
    }

    form.setError("root", { message: error.message })
  }

  const isRegister = mode === "register"

  return (
    <form
      className="grid gap-5"
      onSubmit={form.handleSubmit(handleSubmit)}
      noValidate
    >
      <FieldGroup>
        {isRegister ? (
          <AuthTextField
            control={form.control}
            name="name"
            label="Full name"
            placeholder="Maya Chen"
            autoComplete="name"
          />
        ) : null}
        <AuthTextField
          control={form.control}
          name="email"
          label="Email"
          type="email"
          placeholder="you@company.com"
          autoComplete="email"
        />
        <AuthTextField
          control={form.control}
          name="password"
          label="Password"
          type="password"
          placeholder="At least 8 characters"
          autoComplete={isRegister ? "new-password" : "current-password"}
        />
      </FieldGroup>

      {form.formState.errors.root ? (
        <FieldError>{form.formState.errors.root.message}</FieldError>
      ) : null}

      <Button
        type="submit"
        disabled={isSubmitting}
        className="h-11 rounded-md bg-zinc-950 text-sm text-white hover:bg-zinc-800 dark:bg-white dark:text-zinc-950 dark:hover:bg-zinc-100"
      >
        {isSubmitting ? (
          <CircleNotch className="size-4 animate-spin" />
        ) : (
          <ShieldCheck className="size-4" weight="bold" />
        )}
        {isRegister ? "Create workspace account" : "Sign in to Coordina"}
        <ArrowRight className="size-4" />
      </Button>
    </form>
  )
}
