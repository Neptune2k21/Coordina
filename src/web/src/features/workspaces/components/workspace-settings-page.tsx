import { Gear, UsersThree } from "@phosphor-icons/react"

import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import { Separator } from "@/components/ui/separator"
import { WorkspaceInvitePanel } from "@/features/workspaces/components/workspace-invite-panel"
import { WorkspaceJoinForm } from "@/features/workspaces/components/workspace-join-form"
import { WorkspaceList } from "@/features/workspaces/components/workspace-list"
import { WorkspaceMembersPanel } from "@/features/workspaces/components/workspace-members-panel"
import { useWorkspaces } from "@/features/workspaces/workspace-context"

export function WorkspaceSettingsPage() {
  const { activeWorkspace } = useWorkspaces()

  return (
    <div className="grid gap-5 lg:gap-6">
      <section className="rounded-[32px] border border-zinc-950/[0.08] bg-white/78 p-5 shadow-[0_32px_110px_rgba(24,24,27,0.12)] backdrop-blur-2xl sm:p-6 lg:p-8 dark:border-white/10 dark:bg-white/[0.055]">
        <div className="inline-flex items-center gap-2 rounded-full border border-zinc-950/10 bg-white/72 px-3 py-1.5 text-[12px] font-semibold text-zinc-700 shadow-[0_12px_34px_rgba(24,24,27,0.08)] backdrop-blur-xl dark:border-white/10 dark:bg-white/[0.08] dark:text-zinc-200">
          <Gear className="size-4" weight="bold" />
          Workspace settings
        </div>
        <h1 className="mt-5 text-3xl leading-tight font-semibold tracking-normal text-zinc-950 sm:text-4xl dark:text-white">
          {activeWorkspace?.name}
        </h1>
        <p className="mt-3 max-w-2xl text-sm leading-7 text-zinc-600 sm:text-base dark:text-zinc-300">
          Gérez les accès, invitations et workspaces sans polluer le dashboard.
        </p>
      </section>

      <div className="grid gap-3 xl:grid-cols-[0.9fr_1.1fr]">
        <WorkspaceInvitePanel />
        <WorkspaceMembersPanel />
      </div>

      <Card className="overflow-hidden bg-white/82 shadow-[0_24px_70px_rgba(24,24,27,0.08)] backdrop-blur-xl dark:bg-white/[0.055]">
        <CardHeader>
          <div className="flex items-center gap-2">
            <UsersThree className="size-4" />
            <CardTitle>Workspaces</CardTitle>
          </div>
          <CardDescription>
            Switch, open, or delete owned workspaces.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <WorkspaceList />
        </CardContent>
      </Card>

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
