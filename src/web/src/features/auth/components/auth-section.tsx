import {
  CheckCircle,
  Clock,
  Command,
  Kanban,
  SignOut,
  UsersThree,
} from "@phosphor-icons/react"
import { useEffect, useState } from "react"

import { Button } from "@/components/ui/button"
import { getCurrentUser, login, register } from "@/features/auth/auth-api"
import type {
  AuthFormValues,
  AuthMode,
  AuthSession,
} from "@/features/auth/auth-types"
import { AuthForm } from "@/features/auth/components/auth-form"

const storageKey = "coordina.auth.session"

const workspaceSignals = [
  "Plan launches with your team",
  "Track decisions beside the work",
  "Keep delivery moving in one view",
] as const

const previewCards = [
  {
    title: "Sprint planning",
    meta: "12 tasks",
    icon: Kanban,
  },
  {
    title: "Client launch",
    meta: "4 teammates",
    icon: UsersThree,
  },
  {
    title: "Review window",
    meta: "Today 14:30",
    icon: Clock,
  },
] as const

export function AuthSection() {
  const [mode, setMode] = useState<AuthMode>("login")
  const [session, setSession] = useState<AuthSession | null>(() =>
    readStoredSession()
  )
  const [isSubmitting, setIsSubmitting] = useState(false)

  useEffect(() => {
    const storedSession = readStoredSession()

    if (!storedSession) {
      return
    }

    void getCurrentUser(storedSession.accessToken)
      .then((user) => setSession({ ...storedSession, user }))
      .catch(() => {
        localStorage.removeItem(storageKey)
        setSession(null)
      })
  }, [])

  async function handleSubmit(values: AuthFormValues) {
    setIsSubmitting(true)

    try {
      const nextSession =
        mode === "register"
          ? await register(values)
          : await login({
              email: values.email,
              password: values.password,
            })

      localStorage.setItem(storageKey, JSON.stringify(nextSession))
      setSession(nextSession)
      return nextSession
    } finally {
      setIsSubmitting(false)
    }
  }

  function signOut() {
    localStorage.removeItem(storageKey)
    setSession(null)
  }

  return (
    <section
      id="auth"
      className="min-h-svh bg-white text-zinc-950 dark:bg-zinc-950 dark:text-white"
    >
      <div className="grid min-h-svh lg:grid-cols-[1.08fr_0.92fr]">
        <div className="relative hidden overflow-hidden bg-zinc-950 p-10 text-white lg:flex lg:flex-col lg:justify-between">
          <div className="absolute inset-0 bg-[radial-gradient(circle_at_18%_18%,rgba(20,184,166,0.22),transparent_30%),radial-gradient(circle_at_82%_12%,rgba(244,114,182,0.16),transparent_26%)]" />
          <div className="relative flex items-center gap-3">
            <span className="grid size-11 place-items-center rounded-md bg-white text-zinc-950 shadow-[0_18px_50px_rgba(0,0,0,0.28)]">
              <Command className="size-5" weight="bold" />
            </span>
            <div>
              <p className="text-base leading-none font-semibold">Coordina</p>
              <p className="mt-1 text-xs font-medium text-white/55">
                Team workspace
              </p>
            </div>
          </div>

          <div className="relative max-w-2xl">
            <p className="text-sm font-semibold tracking-[0.18em] text-teal-200 uppercase">
              Work already in motion
            </p>
            <h1 className="mt-5 max-w-xl text-6xl leading-[0.95] font-semibold tracking-normal">
              Welcome back to Coordina
            </h1>
            <p className="mt-6 max-w-lg text-base leading-8 text-white/68">
              Pick up sprint plans, launch dates and decisions exactly where
              your team left them.
            </p>
            <div className="mt-8 grid max-w-xl gap-3">
              {workspaceSignals.map((signal) => (
                <span
                  key={signal}
                  className="inline-flex items-center gap-2 text-sm font-medium text-white/78"
                >
                  <CheckCircle className="size-4 text-teal-200" weight="fill" />
                  {signal}
                </span>
              ))}
            </div>
          </div>

          <div className="relative grid gap-3">
            <div className="rounded-md border border-white/10 bg-white/[0.07] p-4 backdrop-blur-xl">
              <div className="flex items-center justify-between gap-4">
                <div>
                  <p className="text-xs font-semibold tracking-[0.16em] text-white/42 uppercase">
                    Today
                  </p>
                  <p className="mt-1 text-sm font-semibold">
                    Product launch room
                  </p>
                </div>
                <span className="rounded-md bg-teal-300 px-2.5 py-1 text-xs font-bold text-zinc-950">
                  76%
                </span>
              </div>
              <div className="mt-4 h-2 rounded-full bg-white/10">
                <div className="h-full w-[76%] rounded-full bg-teal-300" />
              </div>
            </div>
            <div className="grid grid-cols-3 gap-3">
              {previewCards.map((card) => {
                const Icon = card.icon

                return (
                  <div
                    key={card.title}
                    className="rounded-md border border-white/10 bg-white/[0.05] p-4"
                  >
                    <Icon className="size-5 text-teal-200" />
                    <p className="mt-4 text-sm font-semibold">{card.title}</p>
                    <p className="mt-1 text-xs font-medium text-white/45">
                      {card.meta}
                    </p>
                  </div>
                )
              })}
            </div>
          </div>
        </div>

        <div className="flex min-h-svh flex-col px-5 py-6 sm:px-8 lg:px-12">
          <div className="flex items-center gap-3 lg:hidden">
            <span className="grid size-10 place-items-center rounded-md bg-zinc-950 text-white dark:bg-white dark:text-zinc-950">
              <Command className="size-5" weight="bold" />
            </span>
            <div>
              <p className="text-sm leading-none font-semibold">Coordina</p>
              <p className="mt-1 text-xs font-medium text-muted-foreground">
                Team workspace
              </p>
            </div>
          </div>

          <div className="mx-auto flex w-full max-w-[430px] flex-1 flex-col justify-center py-10">
            {session ? (
              <div className="grid gap-5">
                <div>
                  <p className="text-sm font-medium text-muted-foreground">
                    Active workspace
                  </p>
                  <h3 className="mt-2 text-2xl font-semibold tracking-normal">
                    Welcome back, {session.user.name}
                  </h3>
                  <p className="mt-2 text-sm leading-6 text-muted-foreground">
                    You are signed in as {session.user.email}. Your session
                    stays active until{" "}
                    {new Intl.DateTimeFormat("en-US", {
                      dateStyle: "medium",
                      timeStyle: "short",
                    }).format(new Date(session.expiresAt))}
                    .
                  </p>
                </div>
                <Button
                  type="button"
                  variant="outline"
                  className="h-11 rounded-md"
                  onClick={signOut}
                >
                  <SignOut className="size-4" />
                  Sign out
                </Button>
              </div>
            ) : (
              <>
                <div className="mb-8">
                  <p className="text-sm font-medium text-muted-foreground">
                    {mode === "login"
                      ? "Sign in to continue"
                      : "Create your workspace account"}
                  </p>
                  <h2 className="mt-3 text-3xl font-semibold tracking-normal">
                    {mode === "login" ? "Welcome back" : "Start with Coordina"}
                  </h2>
                </div>
                <AuthForm
                  key={mode}
                  mode={mode}
                  isSubmitting={isSubmitting}
                  onSubmit={handleSubmit}
                />
                <p className="mt-6 text-center text-sm text-muted-foreground">
                  {mode === "login"
                    ? "New to Coordina?"
                    : "Already have an account?"}{" "}
                  <button
                    type="button"
                    className="font-semibold text-zinc-950 underline-offset-4 hover:underline dark:text-white"
                    onClick={() =>
                      setMode((current) =>
                        current === "login" ? "register" : "login"
                      )
                    }
                  >
                    {mode === "login" ? "Create an account" : "Sign in"}
                  </button>
                </p>
              </>
            )}
          </div>
        </div>
      </div>
    </section>
  )
}

function readStoredSession(): AuthSession | null {
  const value = localStorage.getItem(storageKey)

  if (!value) {
    return null
  }

  try {
    return JSON.parse(value) as AuthSession
  } catch {
    localStorage.removeItem(storageKey)
    return null
  }
}
