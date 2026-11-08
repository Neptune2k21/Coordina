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
import { FieldError, FieldGroup } from "@/components/ui/field"
import { ProjectDetailsFields } from "@/features/projects/components/project-details-fields"
import {
  projectErrorMessage,
  type ProjectDetails,
} from "@/features/projects/project-details"
import type { ProjectInput } from "@/types/project"

const emptyDetails: ProjectDetails = {
  name: "",
  description: "",
  key: "",
  icon: "kanban",
  color: "teal",
}

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
  const [details, setDetails] = useState(emptyDetails)
  const [error, setError] = useState<string | null>(null)

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)

    try {
      await onCreate(details)
      setDetails(emptyDetails)
      setOpen(false)
    } catch (submitError) {
      setError(
        projectErrorMessage(submitError, "Project could not be created.")
      )
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
      <DialogContent className="max-w-560px rounded-md border-zinc-950/10 bg-white p-5 shadow-xl dark:border-white/10 dark:bg-zinc-950">
        <DialogHeader>
          <DialogTitle>Create project</DialogTitle>
          <DialogDescription>
            Name the project first. The board workflow is chosen when the
            project opens.
          </DialogDescription>
        </DialogHeader>
        <form
          className="grid gap-4"
          onSubmit={(event) => void handleSubmit(event)}
        >
          <FieldGroup>
            <ProjectDetailsFields
              idPrefix="project"
              value={details}
              onChange={setDetails}
              placeholders={{
                name: "Launch plan",
                description: "Optional context for the team",
                key: "APP",
              }}
            />
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
