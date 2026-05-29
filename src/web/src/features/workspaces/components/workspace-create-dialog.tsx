import { CircleNotch, Plus } from "@phosphor-icons/react"
import { useState } from "react"

import { Button } from "@/components/ui/button"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog"
import { Field, FieldError, FieldGroup, FieldLabel } from "@/components/ui/field"
import { Input } from "@/components/ui/input"
import { ApiError } from "@/features/auth/auth-api"
import { useWorkspaces } from "@/features/workspaces/workspace-context"

export function WorkspaceCreateDialog() {
  const { create, isMutating } = useWorkspaces()
  const [open, setOpen] = useState(false)
  const [name, setName] = useState("")
  const [error, setError] = useState<string | null>(null)

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)

    try {
      await create(name)
      setName("")
      setOpen(false)
    } catch (submitError) {
      if (submitError instanceof ApiError) {
        setError(submitError.errors?.Name?.[0] ?? submitError.message)
        return
      }

      setError("Workspace could not be created.")
    }
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button
          type="button"
          className="h-11 rounded-full bg-zinc-950 px-5 text-sm text-white shadow-[0_18px_44px_rgba(9,9,11,0.2)] transition-transform duration-300 hover:-translate-y-0.5 hover:bg-zinc-900 dark:bg-white dark:text-zinc-950 dark:hover:bg-zinc-100"
        >
          <Plus className="size-4" weight="bold" />
          New workspace
        </Button>
      </DialogTrigger>
      <DialogContent className="rounded-[24px] border-zinc-950/10 bg-white/94 p-6 shadow-[0_34px_110px_rgba(24,24,27,0.2)] backdrop-blur-2xl dark:border-white/10 dark:bg-zinc-950/94">
        <DialogHeader>
          <DialogTitle>Create workspace</DialogTitle>
          <DialogDescription>
            Start a tenant boundary for a team, customer, or internal group.
          </DialogDescription>
        </DialogHeader>
        <form className="grid gap-4" onSubmit={handleSubmit}>
          <FieldGroup>
            <Field>
              <FieldLabel htmlFor="workspace-name">Workspace name</FieldLabel>
              <Input
                id="workspace-name"
                value={name}
                onChange={(event) => setName(event.target.value)}
                placeholder="Product Team"
                autoComplete="organization"
                className="h-11 rounded-2xl bg-white/70 px-4 text-sm dark:bg-white/[0.06]"
              />
            </Field>
            {error ? <FieldError>{error}</FieldError> : null}
          </FieldGroup>
          <DialogFooter>
            <Button
              type="submit"
              disabled={isMutating}
              className="h-11 rounded-full px-5"
            >
              {isMutating ? (
                <CircleNotch className="size-4 animate-spin" />
              ) : (
                <Plus className="size-4" weight="bold" />
              )}
              Create workspace
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
