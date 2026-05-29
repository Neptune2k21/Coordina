import {
  ArrowRight,
  Kanban,
  Lightning,
  Pulse,
  ShieldCheck,
  UsersThree,
} from "@phosphor-icons/react"

import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import { Separator } from "@/components/ui/separator"
import { WorkspaceCreateDialog } from "@/features/workspaces/components/workspace-create-dialog"
import { WorkspaceInvitePanel } from "@/features/workspaces/components/workspace-invite-panel"
import { WorkspaceJoinForm } from "@/features/workspaces/components/workspace-join-form"
import { WorkspaceList } from "@/features/workspaces/components/workspace-list"
import { WorkspaceMembersPanel } from "@/features/workspaces/components/workspace-members-panel"
import { useWorkspaces } from "@/features/workspaces/workspace-context"

const signals = [
  {
    label: "Tenant scope",
    value: "Isolated",
    detail: "Server checked",
    icon: ShieldCheck,
  },
  {
    label: "Members",
    value: "Ready",
    detail: "Invite-only",
    icon: UsersThree,
  },
  {
    label: "Projects",
    value: "Next",
    detail: "Coming layer",
    icon: Kanban,
  },
] as const

export function WorkspaceDashboard() {
  const { activeWorkspace } = useWorkspaces()

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
              Active workspace
            </div>
            <h1 className="mt-5 text-4xl leading-tight font-semibold tracking-normal text-zinc-950 sm:text-5xl dark:text-white">
              {activeWorkspace?.name}
            </h1>
            <p className="mt-4 max-w-2xl text-sm leading-7 text-zinc-600 sm:text-base sm:leading-8 dark:text-zinc-300">
              Secure tenant context is active. Members, invites, projects and
              future realtime channels inherit this workspace boundary.
            </p>
          </div>
          <div className="flex flex-col gap-3 sm:flex-row lg:items-center">
            <WorkspaceCreateDialog />
            <div className="hidden items-center gap-2 rounded-full border border-zinc-950/10 bg-white/62 px-3 py-2 text-xs font-medium text-muted-foreground shadow-[0_12px_34px_rgba(24,24,27,0.07)] backdrop-blur-xl sm:flex dark:border-white/10 dark:bg-white/[0.06]">
              <Lightning
                className="size-4 text-teal-600 dark:text-teal-300"
                weight="fill"
              />
              Context persisted
              <ArrowRight className="size-3.5 opacity-50" />
            </div>
          </div>
        </div>
      </section>

      <div className="grid gap-3 md:grid-cols-3">
        {signals.map((signal) => {
          const Icon = signal.icon

          return (
            <Card key={signal.label}>
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

      <Card className="overflow-hidden bg-white/82 shadow-[0_24px_70px_rgba(24,24,27,0.08)] backdrop-blur-xl dark:bg-white/[0.055]">
        <CardHeader>
          <div className="flex items-center gap-2">
            <Pulse className="size-4" />
            <CardTitle>Workspace access</CardTitle>
          </div>
          <CardDescription>
            Only memberships returned by the API are shown here.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <WorkspaceList />
        </CardContent>
      </Card>

      <div className="grid gap-3 xl:grid-cols-[0.9fr_1.1fr]">
        <WorkspaceInvitePanel />
        <WorkspaceMembersPanel />
      </div>

      <Card className="bg-white/82 shadow-[0_24px_70px_rgba(24,24,27,0.08)] backdrop-blur-xl dark:bg-white/[0.055]">
        <CardHeader>
          <CardTitle>Join another workspace</CardTitle>
          <CardDescription>
            Joining creates a MEMBER role for your account.
          </CardDescription>
        </CardHeader>
        <CardContent className="grid gap-4">
          <Separator />
          <WorkspaceJoinForm compact />
        </CardContent>
      </Card>
    </div>
  )
}
