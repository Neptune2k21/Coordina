import { Crown, Trash, UsersThree } from "@phosphor-icons/react"
import { useEffect, useState } from "react"

import { Button } from "@/components/ui/button"
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import { FieldError } from "@/components/ui/field"
import { Separator } from "@/components/ui/separator"
import { ApiError } from "@/features/auth/auth-api"
import { useAuth } from "@/features/auth/auth-context"
import { useWorkspaces } from "@/features/workspaces/workspace-context"
import type { WorkspaceMember } from "@/features/workspaces/workspace-types"

export function WorkspaceMembersPanel() {
  const { session } = useAuth()
  const { activeWorkspace, isMutating, listMembers, removeMember } =
    useWorkspaces()
  const [members, setMembers] = useState<WorkspaceMember[]>([])
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!activeWorkspace) {
      queueMicrotask(() => setMembers([]))
      return
    }

    let cancelled = false

    void listMembers(activeWorkspace.id)
      .then((nextMembers) => {
        if (!cancelled) {
          setMembers(nextMembers)
        }
      })
      .catch((loadError) => {
        if (!cancelled) {
          setError(
            loadError instanceof Error
              ? loadError.message
              : "Members failed to load."
          )
        }
      })

    return () => {
      cancelled = true
    }
  }, [activeWorkspace, listMembers])

  async function handleRemove(member: WorkspaceMember) {
    if (!activeWorkspace) {
      return
    }

    setError(null)

    try {
      await removeMember(activeWorkspace.id, member.userId)
      setMembers((current) =>
        current.filter((item) => item.userId !== member.userId)
      )
    } catch (removeError) {
      if (removeError instanceof ApiError) {
        setError(removeError.message)
        return
      }

      setError("Member could not be removed.")
    }
  }

  return (
    <Card className="bg-white/82 shadow-[0_24px_70px_rgba(24,24,27,0.08)] backdrop-blur-xl dark:bg-white/[0.055]">
      <CardHeader>
        <div className="flex items-center gap-2">
          <span className="grid size-9 place-items-center rounded-2xl bg-zinc-950/[0.045] dark:bg-white/[0.08]">
            <UsersThree className="size-4" />
          </span>
          <CardTitle>Members</CardTitle>
        </div>
        <CardDescription>
          Memberships are loaded from the workspace-scoped API.
        </CardDescription>
      </CardHeader>
      <CardContent className="grid gap-3">
        {error ? <FieldError>{error}</FieldError> : null}
        <div className="grid gap-2">
          {members.map((member) => (
            <div
              key={member.userId}
              className="group rounded-[18px] border border-transparent p-2 transition-all duration-200 hover:border-zinc-950/[0.08] hover:bg-zinc-950/[0.025] dark:hover:border-white/10 dark:hover:bg-white/[0.045]"
            >
              <div className="flex items-center justify-between gap-3">
                <div className="flex min-w-0 items-center gap-3">
                  <span className="grid size-10 shrink-0 place-items-center rounded-2xl bg-zinc-950 text-sm font-semibold text-white shadow-[0_12px_30px_rgba(9,9,11,0.12)] dark:bg-white dark:text-zinc-950">
                    {(member.name ?? member.email ?? "U")
                      .slice(0, 1)
                      .toUpperCase()}
                  </span>
                  <div className="min-w-0">
                    <p className="truncate text-sm font-medium">
                      {member.name ?? member.email ?? member.userId}
                    </p>
                    <p className="truncate text-xs text-muted-foreground">
                      {member.email ?? member.userId}
                    </p>
                  </div>
                </div>
                <div className="flex items-center gap-2">
                  <span className="inline-flex items-center gap-1 rounded-full border border-zinc-950/10 bg-white/72 px-2.5 py-1 text-[11px] font-semibold text-muted-foreground dark:border-white/10 dark:bg-white/[0.06]">
                    {member.role === "OWNER" ? (
                      <Crown className="size-3.5" weight="fill" />
                    ) : null}
                    {member.role}
                  </span>
                  {activeWorkspace?.role === "OWNER" &&
                  member.role !== "OWNER" &&
                  member.userId !== session?.user.id ? (
                    <Button
                      type="button"
                      variant="destructive"
                      size="icon"
                      className="rounded-full opacity-80 transition-all duration-200 hover:-translate-y-0.5 hover:opacity-100"
                      disabled={isMutating}
                      onClick={() => void handleRemove(member)}
                      aria-label={`Remove ${member.name ?? member.userId}`}
                    >
                      <Trash className="size-4" />
                    </Button>
                  ) : null}
                </div>
              </div>
              <Separator className="mt-2 opacity-70 group-last:hidden" />
            </div>
          ))}
        </div>
      </CardContent>
    </Card>
  )
}
