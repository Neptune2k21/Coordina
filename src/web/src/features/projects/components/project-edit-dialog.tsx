import { CircleNotch, FloppyDisk } from "@phosphor-icons/react"
import { useEffect, useState } from "react"

import { Button } from "@/components/ui/button"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
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
import type {
  Project,
  ProjectUpdateInput,
} from "@/features/projects/project-types"

type ProjectEditDialogProps = {
  isSaving: boolean
  onOpenChange: (open: boolean) => void
  onSave: (project: Project, input: ProjectUpdateInput) => Promise<void>
  open: boolean
  project: Project | null
}

export function ProjectEditDialog({
  isSaving,
  onOpenChange,
  onSave,
  open,
  project,
}: ProjectEditDialogProps) {
  const [name, setName] = useState("")
  const [description, setDescription] = useState("")
  const [key, setKey] = useState("")
  const [icon, setIcon] = useState("")
  const [color, setColor] = useState("")
  const [status, setStatus] = useState<Project["status"]>("ACTIVE")
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!project) {
      return
    }

    queueMicrotask(() => {
      setName(project.name)
      setDescription(project.description ?? "")
      setKey(project.key ?? "")
      setIcon(project.icon ?? "")
      setColor(project.color ?? "")
      setStatus(project.status)
      setError(null)
    })
  }, [project])

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()

    if (!project) {
      return
    }

    setError(null)

    try {
      await onSave(project, {
        name,
        description,
        key,
        icon,
        color,
        status,
      })
      onOpenChange(false)
    } catch (submitError) {
      if (submitError instanceof ApiError) {
        setError(
          submitError.errors?.Name?.[0] ??
            submitError.errors?.Description?.[0] ??
            submitError.errors?.Key?.[0] ??
            submitError.message
        )
        return
      }

      setError("Project could not be updated.")
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="rounded-[24px] border-zinc-950/10 bg-white/94 p-6 shadow-[0_34px_110px_rgba(24,24,27,0.2)] backdrop-blur-2xl dark:border-white/10 dark:bg-zinc-950/94">
        <DialogHeader>
          <DialogTitle>Edit project</DialogTitle>
          <DialogDescription>
            Project metadata and status remain scoped to the active workspace.
          </DialogDescription>
        </DialogHeader>
        <form className="grid gap-4" onSubmit={handleSubmit}>
          <FieldGroup>
            <Field>
              <FieldLabel htmlFor="edit-project-name">Project name</FieldLabel>
              <Input
                id="edit-project-name"
                value={name}
                onChange={(event) => setName(event.target.value)}
                className="h-11 rounded-2xl bg-white/70 px-4 text-sm dark:bg-white/[0.06]"
              />
            </Field>
            <Field>
              <FieldLabel htmlFor="edit-project-description">
                Description
              </FieldLabel>
              <Textarea
                id="edit-project-description"
                value={description}
                onChange={(event) => setDescription(event.target.value)}
                className="rounded-2xl bg-white/70 px-4 text-sm dark:bg-white/[0.06]"
              />
            </Field>
            <div className="grid gap-3 sm:grid-cols-3">
              <Field>
                <FieldLabel htmlFor="edit-project-key">Key</FieldLabel>
                <Input
                  id="edit-project-key"
                  value={key}
                  onChange={(event) => setKey(event.target.value)}
                  className="h-11 rounded-2xl bg-white/70 px-4 text-sm uppercase dark:bg-white/[0.06]"
                />
              </Field>
              <Field>
                <FieldLabel htmlFor="edit-project-icon">Icon</FieldLabel>
                <Input
                  id="edit-project-icon"
                  value={icon}
                  onChange={(event) => setIcon(event.target.value)}
                  className="h-11 rounded-2xl bg-white/70 px-4 text-sm dark:bg-white/[0.06]"
                />
              </Field>
              <Field>
                <FieldLabel htmlFor="edit-project-color">Color</FieldLabel>
                <Input
                  id="edit-project-color"
                  value={color}
                  onChange={(event) => setColor(event.target.value)}
                  className="h-11 rounded-2xl bg-white/70 px-4 text-sm dark:bg-white/[0.06]"
                />
              </Field>
            </div>
            <Field>
              <FieldLabel htmlFor="edit-project-status">Status</FieldLabel>
              <select
                id="edit-project-status"
                value={status}
                onChange={(event) =>
                  setStatus(event.target.value as Project["status"])
                }
                className="h-11 rounded-2xl border border-input bg-background px-4 text-sm shadow-xs outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50 dark:bg-white/[0.06]"
              >
                <option value="ACTIVE">Active</option>
                <option value="ARCHIVED">Archived</option>
                <option value="COMPLETED">Completed</option>
              </select>
            </Field>
            {error ? <FieldError>{error}</FieldError> : null}
          </FieldGroup>
          <DialogFooter>
            <Button
              type="submit"
              disabled={isSaving}
              className="h-11 rounded-full px-5"
            >
              {isSaving ? (
                <CircleNotch className="size-4 animate-spin" />
              ) : (
                <FloppyDisk className="size-4" weight="bold" />
              )}
              Save project
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
