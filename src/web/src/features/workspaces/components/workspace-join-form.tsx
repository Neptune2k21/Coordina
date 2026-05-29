import { ArrowRight, CircleNotch } from "@phosphor-icons/react"
import { useState } from "react"

import { Button } from "@/components/ui/button"
import {
  Field,
  FieldError,
  FieldGroup,
  FieldLabel,
} from "@/components/ui/field"
import { Input } from "@/components/ui/input"
import { ApiError } from "@/features/auth/auth-api"
import { useWorkspaces } from "@/features/workspaces/workspace-context"

export function WorkspaceJoinForm({ compact = false }: { compact?: boolean }) {
  const { isMutating, join } = useWorkspaces()
  const [inviteCode, setInviteCode] = useState("")
  const [error, setError] = useState<string | null>(null)

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)

    try {
      await join(inviteCode)
      setInviteCode("")
    } catch (submitError) {
      if (submitError instanceof ApiError) {
        setError(submitError.errors?.InviteCode?.[0] ?? submitError.message)
        return
      }

      setError("Workspace could not be joined.")
    }
  }

  return (
    <form
      className={compact ? "grid gap-2" : "grid gap-3"}
      onSubmit={handleSubmit}
    >
      <FieldGroup>
        <Field>
          <FieldLabel
            htmlFor={compact ? "join-workspace-compact" : "join-workspace"}
          >
            Invitation code
          </FieldLabel>
          <div className="flex flex-col gap-2 sm:flex-row">
            <Input
              id={compact ? "join-workspace-compact" : "join-workspace"}
              value={inviteCode}
              onChange={(event) => setInviteCode(event.target.value)}
              placeholder="A1B2C3D4E5F6"
              className="h-11 rounded-2xl bg-white/70 px-4 font-mono text-sm tracking-[0.12em] dark:bg-white/[0.06]"
            />
            <Button
              type="submit"
              variant={compact ? "outline" : "default"}
              disabled={isMutating}
              className="h-11 rounded-full px-5 transition-transform duration-200 hover:-translate-y-0.5"
            >
              {isMutating ? (
                <CircleNotch className="size-4 animate-spin" />
              ) : (
                <ArrowRight className="size-4" />
              )}
              Join
            </Button>
          </div>
        </Field>
        {error ? <FieldError>{error}</FieldError> : null}
      </FieldGroup>
    </form>
  )
}
