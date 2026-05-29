import {
  ArrowRight,
  Kanban,
  Pulse,
  ShieldCheck,
  Sparkle,
} from "@phosphor-icons/react"

import { Button } from "@/components/ui/button"
import {
  Card,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import { useWorkspaces } from "@/features/workspaces/workspace-context"

const dashboardSignals = [
  {
    label: "Active module",
    value: "Projects",
    detail: "Workspace-scoped",
    icon: Kanban,
  },
  {
    label: "Access",
    value: "Protected",
    detail: "Server-enforced",
    icon: ShieldCheck,
  },
  {
    label: "Next layer",
    value: "Tasks",
    detail: "Coming after projects",
    icon: Sparkle,
  },
] as const

export function WorkspaceDashboard() {
  const { activeWorkspace } = useWorkspaces()

  function navigate(path: string) {
    window.history.pushState({}, "", path)
    window.dispatchEvent(new PopStateEvent("popstate"))
  }

  return (
    <div className="grid gap-5 lg:gap-6">
      <section className="relative isolate overflow-hidden rounded-[32px] border border-zinc-950/[0.08] bg-white/78 p-5 shadow-[0_32px_110px_rgba(24,24,27,0.12)] backdrop-blur-2xl sm:p-6 lg:p-8 dark:border-white/10 dark:bg-white/[0.055] dark:shadow-[0_32px_110px_rgba(0,0,0,0.34)]">
        <div className="pointer-events-none absolute inset-0 -z-10 bg-[radial-gradient(circle_at_16%_16%,rgba(45,212,191,0.16),transparent_30%),radial-gradient(circle_at_88%_10%,rgba(236,72,153,0.1),transparent_28%)]" />
        <div className="flex flex-col justify-between gap-6 lg:flex-row lg:items-end">
          <div className="max-w-3xl">
            <div className="inline-flex items-center gap-2 rounded-full border border-zinc-950/10 bg-white/72 px-3 py-1.5 text-[12px] font-semibold text-zinc-700 shadow-[0_12px_34px_rgba(24,24,27,0.08)] backdrop-blur-xl dark:border-white/10 dark:bg-white/[0.08] dark:text-zinc-200">
              <span className="grid size-5 place-items-center rounded-full bg-zinc-950 text-white dark:bg-white dark:text-zinc-950">
                <Pulse className="size-3.5" weight="bold" />
              </span>
              Dashboard
            </div>
            <h1 className="mt-5 text-4xl leading-tight font-semibold tracking-normal text-zinc-950 sm:text-5xl dark:text-white">
              {activeWorkspace?.name}
            </h1>
            <p className="mt-4 max-w-2xl text-sm leading-7 text-zinc-600 sm:text-base sm:leading-8 dark:text-zinc-300">
              Un espace clair pour suivre le travail du workspace. Les membres,
              invitations et réglages restent dans les paramètres.
            </p>
          </div>
          <Button
            type="button"
            className="h-11 rounded-full px-5 shadow-[0_18px_44px_rgba(9,9,11,0.16)]"
            onClick={() => navigate("/app/projects")}
          >
            <Kanban className="size-4" weight="bold" />
            Open projects
            <ArrowRight className="size-4" />
          </Button>
        </div>
      </section>

      <div className="grid gap-3 md:grid-cols-3">
        {dashboardSignals.map((signal) => {
          const Icon = signal.icon

          return (
            <Card
              key={signal.label}
              className="bg-white/82 dark:bg-white/[0.055]"
            >
              <CardHeader className="rounded-md p-5 transition-transform duration-200 hover:-translate-y-1">
                <div className="flex items-center justify-between gap-2">
                  <CardDescription>{signal.label}</CardDescription>
                  <span className="grid size-9 place-items-center rounded-2xl bg-zinc-950/[0.045] dark:bg-white/[0.08]">
                    <Icon className="size-4 text-muted-foreground" />
                  </span>
                </div>
                <CardTitle className="text-2xl">{signal.value}</CardTitle>
                <CardDescription>{signal.detail}</CardDescription>
              </CardHeader>
            </Card>
          )
        })}
      </div>
    </div>
  )
}
