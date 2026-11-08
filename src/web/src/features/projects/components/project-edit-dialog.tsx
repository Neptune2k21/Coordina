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
import { ProjectDetailsFields } from "@/features/projects/components/project-details-fields"
import {
  detailsOf,
  projectErrorMessage,
  type ProjectDetails,
} from "@/features/projects/project-details"
import type { Project, ProjectUpdateInput } from "@/types/project"

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
  const [details, setDetails] = useState<ProjectDetails | null>(null)
  const [status, setStatus] = useState<Project["status"]>("ACTIVE")
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!project) {
      return
    }

    queueMicrotask(() => {
      setDetails(detailsOf(project))
      setStatus(project.status)
      setError(null)
    })
  }, [project])

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()

    if (!project || !details) {
      return
    }

    setError(null)

    try {
      await onSave(project, { ...details, status })
      onOpenChange(false)
    } catch (submitError) {
      setError(
        projectErrorMessage(submitError, "Project could not be updated.")
      )
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-[560px] rounded-md border-zinc-950/10 bg-white p-5 shadow-xl dark:border-white/10 dark:bg-zinc-950">
        <DialogHeader>
          <DialogTitle>Edit project</DialogTitle>
          <DialogDescription>
            Project metadata and status remain scoped to the active workspace.
          </DialogDescription>
        </DialogHeader>
        <form
          className="grid gap-4"
          onSubmit={(event) => void handleSubmit(event)}
        >
          <FieldGroup>
            {details ? (
              <ProjectDetailsFields
                idPrefix="edit-project"
                value={details}
                onChange={setDetails}
              />
            ) : null}
            <Field>
              <FieldLabel htmlFor="edit-project-status">Status</FieldLabel>
              <select
                id="edit-project-status"
                value={status}
                onChange={(event) =>
                  setStatus(event.target.value as Project["status"])
                }
                className="h-9 rounded-md border border-input bg-background px-3 text-sm shadow-xs outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50 dark:bg-white/[0.06]"
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
              className="h-9 rounded-md px-3"
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
