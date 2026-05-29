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
import {
  Field,
  FieldError,
  FieldGroup,
  FieldLabel,
} from "@/components/ui/field"
import { Input } from "@/components/ui/input"
import { Textarea } from "@/components/ui/textarea"
import { ApiError } from "@/features/auth/auth-api"
import type { ProjectInput } from "@/features/projects/project-types"

type ProjectCreateDialogProps = {
  disabled?: boolean
  isCreating: boolean
  onCreate: (input: ProjectInput) => Promise<void>
}

export function ProjectCreateDialog({
  disabled,
  isCreating,
  onCreate,
}: ProjectCreateDialogProps) {
  const [open, setOpen] = useState(false)
  const [name, setName] = useState("")
  const [description, setDescription] = useState("")
  const [key, setKey] = useState("")
  const [icon, setIcon] = useState("")
  const [color, setColor] = useState("")
  const [error, setError] = useState<string | null>(null)

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)

    try {
      await onCreate({ name, description, key, icon, color })
      setName("")
      setDescription("")
      setKey("")
      setIcon("")
      setColor("")
      setOpen(false)
    } catch (submitError) {
      if (submitError instanceof ApiError) {
        setError(
          submitError.errors?.Name?.[0] ??
            submitError.errors?.Description?.[0] ??
            submitError.message
        )
        return
      }

      setError("Project could not be created.")
    }
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button
          type="button"
          disabled={disabled}
          className="h-11 rounded-full px-5 shadow-[0_18px_44px_rgba(9,9,11,0.16)]"
        >
          <Plus className="size-4" weight="bold" />
          New project
        </Button>
      </DialogTrigger>
      <DialogContent className="rounded-[24px] border-zinc-950/10 bg-white/94 p-6 shadow-[0_34px_110px_rgba(24,24,27,0.2)] backdrop-blur-2xl dark:border-white/10 dark:bg-zinc-950/94">
        <DialogHeader>
          <DialogTitle>Create project</DialogTitle>
          <DialogDescription>
            Projects are created inside the active workspace only.
          </DialogDescription>
        </DialogHeader>
        <form className="grid gap-4" onSubmit={handleSubmit}>
          <FieldGroup>
            <Field>
              <FieldLabel htmlFor="project-name">Project name</FieldLabel>
              <Input
                id="project-name"
                value={name}
                onChange={(event) => setName(event.target.value)}
                placeholder="Launch plan"
                autoComplete="off"
                className="h-11 rounded-2xl bg-white/70 px-4 text-sm dark:bg-white/[0.06]"
              />
            </Field>
            <Field>
              <FieldLabel htmlFor="project-description">Description</FieldLabel>
              <Textarea
                id="project-description"
                value={description}
                onChange={(event) => setDescription(event.target.value)}
                placeholder="Optional context for the team"
                className="rounded-2xl bg-white/70 px-4 text-sm dark:bg-white/[0.06]"
              />
            </Field>
            <div className="grid gap-3 sm:grid-cols-3">
              <Field>
                <FieldLabel htmlFor="project-key">Key</FieldLabel>
                <Input
                  id="project-key"
                  value={key}
                  onChange={(event) => setKey(event.target.value)}
                  placeholder="APP"
                  autoComplete="off"
                  className="h-11 rounded-2xl bg-white/70 px-4 text-sm uppercase dark:bg-white/[0.06]"
                />
              </Field>
              <Field>
                <FieldLabel htmlFor="project-icon">Icon</FieldLabel>
                <Input
                  id="project-icon"
                  value={icon}
                  onChange={(event) => setIcon(event.target.value)}
                  placeholder="✨"
                  autoComplete="off"
                  className="h-11 rounded-2xl bg-white/70 px-4 text-sm dark:bg-white/[0.06]"
                />
              </Field>
              <Field>
                <FieldLabel htmlFor="project-color">Color</FieldLabel>
                <Input
                  id="project-color"
                  value={color}
                  onChange={(event) => setColor(event.target.value)}
                  placeholder="teal"
                  autoComplete="off"
                  className="h-11 rounded-2xl bg-white/70 px-4 text-sm dark:bg-white/[0.06]"
                />
              </Field>
            </div>
            {error ? <FieldError>{error}</FieldError> : null}
          </FieldGroup>
          <DialogFooter>
            <Button
              type="submit"
              disabled={isCreating}
              className="h-11 rounded-full px-5"
            >
              {isCreating ? (
                <CircleNotch className="size-4 animate-spin" />
              ) : (
                <Plus className="size-4" weight="bold" />
              )}
              Create project
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
