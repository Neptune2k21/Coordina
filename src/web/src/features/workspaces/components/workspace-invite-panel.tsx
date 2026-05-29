import { Check, Copy, Ticket } from "@phosphor-icons/react"
import { useState } from "react"

import { Button } from "@/components/ui/button"
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import { FieldError } from "@/components/ui/field"
import { Input } from "@/components/ui/input"
import { ApiError } from "@/features/auth/auth-api"
import { useWorkspaces } from "@/features/workspaces/workspace-context"
import type { WorkspaceInvite } from "@/features/workspaces/workspace-types"

export function WorkspaceInvitePanel() {
  const { activeWorkspace, createInvite, isMutating } = useWorkspaces()
  const [invite, setInvite] = useState<WorkspaceInvite | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [copied, setCopied] = useState(false)

  async function handleCreateInvite() {
    if (!activeWorkspace) {
      return
    }

    setError(null)

    try {
      setInvite(await createInvite(activeWorkspace.id))
    } catch (createError) {
      if (createError instanceof ApiError) {
        setError(createError.message)
        return
      }

      setError("Invitation code could not be created.")
    }
  }

  async function copyInvite() {
    if (!invite) {
      return
    }

    await navigator.clipboard.writeText(invite.code)
    setCopied(true)
    window.setTimeout(() => setCopied(false), 1400)
  }

  if (activeWorkspace?.role !== "OWNER") {
    return null
  }

  return (
    <Card className="relative isolate overflow-hidden bg-zinc-950 text-white shadow-[0_28px_90px_rgba(9,9,11,0.22)] dark:border-white/10 dark:bg-white dark:text-zinc-950">
      <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_18%_12%,rgba(45,212,191,0.24),transparent_32%),radial-gradient(circle_at_90%_8%,rgba(236,72,153,0.16),transparent_28%)] dark:bg-[radial-gradient(circle_at_18%_12%,rgba(20,184,166,0.18),transparent_32%)]" />
      <CardHeader>
        <div className="flex items-center gap-2">
          <span className="grid size-9 place-items-center rounded-2xl bg-white/12 dark:bg-zinc-950/[0.08]">
            <Ticket className="size-4" />
          </span>
          <CardTitle>Invite a member</CardTitle>
        </div>
        <CardDescription className="text-white/62 dark:text-zinc-500">
          Generate a one-time code. The first signed-in user to redeem it joins
          as MEMBER.
        </CardDescription>
      </CardHeader>
      <CardContent className="relative grid gap-3">
        {invite ? (
          <div className="flex gap-2">
            <Input
              readOnly
              value={invite.code}
              className="h-12 rounded-2xl border-white/12 bg-white/10 px-4 font-mono text-sm tracking-[0.14em] text-white shadow-[inset_0_1px_0_rgba(255,255,255,0.08)] dark:border-zinc-950/10 dark:bg-zinc-950/[0.055] dark:text-zinc-950"
            />
            <Button
              type="button"
              variant="outline"
              size="icon"
              className="h-12 w-12 rounded-2xl border-white/12 bg-white/10 text-white transition-all duration-200 hover:-translate-y-0.5 hover:bg-white/16 dark:border-zinc-950/10 dark:bg-zinc-950/[0.055] dark:text-zinc-950"
              onClick={() => void copyInvite()}
              aria-label="Copy invitation code"
            >
              {copied ? (
                <Check className="size-4" weight="bold" />
              ) : (
                <Copy className="size-4" />
              )}
            </Button>
          </div>
        ) : null}
        {invite ? (
          <p className="text-xs text-white/55 dark:text-zinc-500">
            Expires{" "}
            {new Intl.DateTimeFormat("en-US", {
              dateStyle: "medium",
              timeStyle: "short",
            }).format(new Date(invite.expiresAt))}
            .
          </p>
        ) : null}
        {error ? <FieldError>{error}</FieldError> : null}
        <Button
          type="button"
          className="w-fit rounded-full bg-white px-5 text-zinc-950 shadow-[0_18px_44px_rgba(0,0,0,0.22)] transition-transform duration-300 hover:-translate-y-0.5 hover:bg-zinc-100 dark:bg-zinc-950 dark:text-white dark:hover:bg-zinc-800"
          disabled={isMutating}
          onClick={() => void handleCreateInvite()}
        >
          <Ticket className="size-4" />
          Generate code
        </Button>
      </CardContent>
    </Card>
  )
}
