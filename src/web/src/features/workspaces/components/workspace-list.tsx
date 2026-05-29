import { ArrowRight, Buildings, Trash } from "@phosphor-icons/react"
import { useState } from "react"

import { Button } from "@/components/ui/button"
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import { Separator } from "@/components/ui/separator"
import { ApiError } from "@/features/auth/auth-api"
import { useWorkspaces } from "@/features/workspaces/workspace-context"
import type { Workspace } from "@/features/workspaces/workspace-types"

export function WorkspaceList() {
  const {
    activeWorkspace,
    isMutating,
    remove,
    setActiveWorkspaceId,
    workspaces,
  } = useWorkspaces()
  const [error, setError] = useState<string | null>(null)

  async function handleDelete(workspace: Workspace) {
    setError(null)

    try {
      await remove(workspace.id)
    } catch (deleteError) {
      if (deleteError instanceof ApiError) {
        setError(deleteError.message)
        return
      }

      setError("Workspace could not be deleted.")
    }
  }

  return (
    <div className="grid gap-3">
      {error ? (
        <Card className="border-destructive/30 bg-destructive/5">
          <CardContent className="pt-4 text-sm text-destructive">
            {error}
          </CardContent>
        </Card>
      ) : null}
      <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
        {workspaces.map((workspace) => (
          <Card
            key={workspace.id}
            className={[
              "group overflow-hidden bg-white/76 shadow-[0_18px_54px_rgba(24,24,27,0.07)] backdrop-blur-xl transition-all duration-300 hover:-translate-y-1 hover:border-zinc-950/15 hover:shadow-[0_28px_80px_rgba(24,24,27,0.12)] dark:bg-white/[0.045] dark:hover:border-white/20",
              workspace.id === activeWorkspace?.id
                ? "border-zinc-950/25 bg-white/92 dark:border-white/25 dark:bg-white/[0.08]"
                : "",
            ].join(" ")}
          >
            <CardHeader>
              <div className="flex items-start justify-between gap-3">
                <span className="grid size-11 place-items-center rounded-[16px] bg-zinc-950 text-white shadow-[0_14px_34px_rgba(9,9,11,0.16)] dark:bg-white dark:text-zinc-950">
                  <Buildings className="size-4" />
                </span>
                <span className="rounded-full border border-zinc-950/10 bg-white/74 px-2.5 py-1 text-[11px] font-semibold text-muted-foreground dark:border-white/10 dark:bg-white/[0.06]">
                  {workspace.role}
                </span>
              </div>
              <CardTitle className="pt-1 text-base">{workspace.name}</CardTitle>
              <CardDescription className="truncate font-mono">
                {workspace.id}
              </CardDescription>
            </CardHeader>
            <CardContent className="grid gap-3">
              <Separator />
              <div className="flex items-center justify-between gap-2">
                <Button
                  type="button"
                  variant="outline"
                  className="rounded-full bg-white/62 transition-all duration-200 hover:-translate-y-0.5 dark:bg-white/[0.06]"
                  onClick={() => setActiveWorkspaceId(workspace.id)}
                >
                  Open
                  <ArrowRight className="size-4 transition-transform duration-200 group-hover:translate-x-0.5" />
                </Button>
                {workspace.role === "OWNER" ? (
                  <Button
                    type="button"
                    variant="destructive"
                    size="icon"
                    className="rounded-full transition-transform duration-200 hover:-translate-y-0.5"
                    disabled={isMutating}
                    onClick={() => void handleDelete(workspace)}
                    aria-label={`Delete ${workspace.name}`}
                  >
                    <Trash className="size-4" />
                  </Button>
                ) : null}
              </div>
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  )
}
