import {
  Archive,
  ArrowCounterClockwise,
  CircleNotch,
  Trash,
} from "@phosphor-icons/react"

import { Button } from "@/components/ui/button"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"
import type { ProjectAction } from "@/features/projects/use-projects"

type ProjectActionDialogProps = {
  action: ProjectAction | null
  isBusy: boolean
  onCancel: () => void
  onConfirm: () => void
}

export function ProjectActionDialog({
  action,
  isBusy,
  onCancel,
  onConfirm,
}: ProjectActionDialogProps) {
  const title =
    action?.type === "archive"
      ? "Archive project"
      : action?.type === "restore"
        ? "Restore project"
        : "Delete project permanently"
  const description =
    action?.type === "archive"
      ? "Archived projects are hidden from the default project list but remain accessible."
      : action?.type === "restore"
        ? "The project will return to the active project list."
        : "This permanently removes the project and cannot be undone."

  return (
    <Dialog open={action !== null} onOpenChange={(open) => !open && onCancel()}>
      <DialogContent className="rounded-md border-zinc-950/10 bg-white p-5 shadow-xl dark:border-white/10 dark:bg-zinc-950">
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>{description}</DialogDescription>
        </DialogHeader>
        <DialogFooter>
          <Button
            type="button"
            variant="outline"
            className="h-9 rounded-md px-3"
            onClick={onCancel}
            disabled={isBusy}
          >
            Cancel
          </Button>
          <Button
            type="button"
            variant={action?.type === "delete" ? "destructive" : "default"}
            className="h-9 rounded-md px-3"
            onClick={onConfirm}
            disabled={isBusy}
          >
            {isBusy ? (
              <CircleNotch className="size-4 animate-spin" />
            ) : action?.type === "restore" ? (
              <ArrowCounterClockwise className="size-4" />
            ) : action?.type === "delete" ? (
              <Trash className="size-4" />
            ) : (
              <Archive className="size-4" />
            )}
            Confirm
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
