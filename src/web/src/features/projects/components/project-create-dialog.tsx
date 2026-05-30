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
import {
  ProjectColorPicker,
  ProjectIconMark,
  ProjectIconPicker,
} from "@/features/projects/components/project-personalization"
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
  const [icon, setIcon] = useState("kanban")
  const [color, setColor] = useState("teal")
  const [error, setError] = useState<string | null>(null)

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)

    try {
      await onCreate({ name, description, key, icon, color })
      setName("")
      setDescription("")
      setKey("")
      setIcon("kanban")
      setColor("teal")
      setOpen(false)
    } catch (submitError) {
      if (submitError instanceof ApiError) {
        setError(
          submitError.errors?.Name?.[0] ??
            submitError.errors?.Description?.[0] ??
            submitError.errors?.Key?.[0] ??
            submitError.errors?.Icon?.[0] ??
            submitError.errors?.Color?.[0] ??
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
          className="h-9 rounded-md px-3"
        >
          <Plus className="size-4" weight="bold" />
          New project
        </Button>
      </DialogTrigger>
      <DialogContent className="max-w-[560px] rounded-md border-zinc-950/10 bg-white p-5 shadow-xl dark:border-white/10 dark:bg-zinc-950">
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
                className="h-9 rounded-md bg-white px-3 text-sm dark:bg-white/[0.06]"
              />
            </Field>
            <Field>
              <FieldLabel htmlFor="project-description">Description</FieldLabel>
              <Textarea
                id="project-description"
                value={description}
                onChange={(event) => setDescription(event.target.value)}
                placeholder="Optional context for the team"
                className="rounded-md bg-white px-3 text-sm dark:bg-white/[0.06]"
              />
            </Field>
            <div className="grid gap-3 sm:grid-cols-[1fr_2fr]">
              <Field>
                <FieldLabel htmlFor="project-key">Key</FieldLabel>
                <Input
                  id="project-key"
                  value={key}
                  onChange={(event) => setKey(event.target.value)}
                  placeholder="APP"
                  autoComplete="off"
                  className="h-9 rounded-md bg-white px-3 text-sm uppercase dark:bg-white/[0.06]"
                />
              </Field>
              <Field>
                <FieldLabel>Preview</FieldLabel>
                <div className="flex h-9 items-center gap-3 rounded-md border border-zinc-950/10 bg-white px-3 dark:border-white/10 dark:bg-white/[0.06]">
                  <ProjectIconMark icon={icon} color={color} />
                  <span className="truncate text-sm font-medium">
                    {name || "Launch plan"}
                  </span>
                </div>
              </Field>
            </div>
            <Field>
              <FieldLabel>Icon</FieldLabel>
              <ProjectIconPicker value={icon} onChange={setIcon} />
            </Field>
            <Field>
              <FieldLabel>Color</FieldLabel>
              <ProjectColorPicker value={color} onChange={setColor} />
            </Field>
            {error ? <FieldError>{error}</FieldError> : null}
          </FieldGroup>
          <DialogFooter>
            <Button
              type="submit"
              disabled={isCreating}
              className="h-9 rounded-md px-3"
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
