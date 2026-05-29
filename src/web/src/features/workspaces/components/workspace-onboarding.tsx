import { Buildings, Command, LinkSimple, Sparkle } from "@phosphor-icons/react"

import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import { WorkspaceCreateDialog } from "@/features/workspaces/components/workspace-create-dialog"
import { WorkspaceJoinForm } from "@/features/workspaces/components/workspace-join-form"

export function WorkspaceOnboarding() {
  return (
    <main className="relative isolate flex min-h-svh w-full items-center overflow-hidden px-5 py-10">
      <div className="pointer-events-none absolute inset-0 -z-10 bg-[radial-gradient(circle_at_18%_16%,rgba(45,212,191,0.16),transparent_30%),radial-gradient(circle_at_82%_10%,rgba(236,72,153,0.1),transparent_26%),linear-gradient(180deg,rgba(255,255,255,0.98)_0%,rgba(244,244,245,0.72)_100%)] dark:bg-[radial-gradient(circle_at_18%_16%,rgba(20,184,166,0.1),transparent_30%),radial-gradient(circle_at_82%_10%,rgba(236,72,153,0.08),transparent_26%),linear-gradient(180deg,rgba(9,9,11,1)_0%,rgba(24,24,27,0.92)_100%)]" />
      <div className="mx-auto grid w-full max-w-5xl gap-7">
        <div className="max-w-2xl">
          <div className="inline-flex items-center gap-2 rounded-full border border-zinc-950/10 bg-white/78 px-3 py-1.5 text-[12px] font-semibold text-zinc-700 shadow-[0_12px_34px_rgba(24,24,27,0.08)] backdrop-blur-xl dark:border-white/10 dark:bg-white/[0.06] dark:text-zinc-200">
            <span className="grid size-5 place-items-center rounded-full bg-zinc-950 text-white dark:bg-white dark:text-zinc-950">
              <Command className="size-3.5" weight="bold" />
            </span>
            Coordina workspaces
          </div>
          <h1 className="mt-5 max-w-2xl text-4xl leading-tight font-semibold tracking-normal sm:text-5xl">
            Create or join your first workspace.
          </h1>
          <p className="mt-4 max-w-xl text-sm leading-7 text-muted-foreground sm:text-base sm:leading-8">
            Every project, task, and realtime event will live inside this tenant
            boundary.
          </p>
        </div>
        <div className="grid gap-3 md:grid-cols-2">
          <Card className="group overflow-hidden bg-white/82 shadow-[0_28px_90px_rgba(24,24,27,0.1)] backdrop-blur-2xl transition-all duration-300 hover:-translate-y-1 hover:shadow-[0_34px_110px_rgba(24,24,27,0.14)] dark:bg-white/[0.055]">
            <CardHeader>
              <span className="grid size-12 place-items-center rounded-[18px] bg-zinc-950 text-white shadow-[0_18px_45px_rgba(9,9,11,0.18)] dark:bg-white dark:text-zinc-950">
                <Buildings className="size-5" />
              </span>
              <CardTitle className="text-base">Start a workspace</CardTitle>
              <CardDescription>
                You will become the owner and can delete it later.
              </CardDescription>
            </CardHeader>
            <CardContent>
              <WorkspaceCreateDialog />
            </CardContent>
          </Card>
          <Card className="group overflow-hidden bg-zinc-950 text-white shadow-[0_28px_90px_rgba(9,9,11,0.22)] transition-all duration-300 hover:-translate-y-1 hover:shadow-[0_34px_110px_rgba(9,9,11,0.28)] dark:border-white/10 dark:bg-white dark:text-zinc-950">
            <CardHeader>
              <span className="grid size-12 place-items-center rounded-[18px] bg-white/12 dark:bg-zinc-950/[0.08]">
                <LinkSimple className="size-5" />
              </span>
              <CardTitle className="text-base">Join by invite</CardTitle>
              <CardDescription className="text-white/62 dark:text-zinc-500">
                Paste a one-time invitation code to join as a member.
              </CardDescription>
            </CardHeader>
            <CardContent>
              <WorkspaceJoinForm />
            </CardContent>
          </Card>
        </div>
        <div className="flex items-center gap-2 text-xs font-medium text-muted-foreground">
          <Sparkle
            className="size-4 text-teal-600 dark:text-teal-300"
            weight="fill"
          />
          Invitation codes are single-use and checked by the API.
        </div>
      </div>
    </main>
  )
}
