import {
  Archive,
  ArrowCounterClockwise,
  CheckCircle,
  CircleNotch,
  FloppyDisk,
  Trash,
} from "@phosphor-icons/react"

import { SettingsSection } from "@/components/layout/settings-section"
import { Button } from "@/components/ui/button"
import { FieldGroup } from "@/components/ui/field"
import { BoardSettings } from "@/features/board"
import { ProjectActionDialog } from "@/features/projects/components/project-action-dialog"
import { ProjectDetailsFields } from "@/features/projects/components/project-details-fields"
import { useProjectSettings } from "@/features/projects/use-project-settings"
import type { Project } from "@/types/project"

type ProjectSettingsProps = {
  project: Project
  onProjectChange: (project: Project) => void
  onDeleted: () => void
}

const statusDescriptions: Record<Project["status"], string> = {
  ACTIVE: "Active: the team plans and delivers work here.",
  ARCHIVED:
    "Archived: hidden from the project list by default, and still editable.",
  COMPLETED: "Completed: read-only for good.",
}

export function ProjectSettings({
  onDeleted,
  onProjectChange,
  project,
}: ProjectSettingsProps) {
  const settings = useProjectSettings({ project, onProjectChange, onDeleted })

  return (
    <div className="mx-auto grid w-full max-w-3xl gap-3">
      <SettingsSection
        title="General"
        description="How the project shows up in the workspace."
      >
        <form
          className="grid gap-4"
          onSubmit={(event) => {
            event.preventDefault()
            void settings.saveDetails()
          }}
        >
          <fieldset disabled={!settings.canManage} className="contents">
            <FieldGroup>
              <ProjectDetailsFields
                idPrefix="settings-project"
                value={settings.details}
                onChange={settings.changeDetails}
              />
            </FieldGroup>
          </fieldset>
          {settings.canManage ? (
            <div className="flex items-center gap-3">
              <Button
                type="submit"
                disabled={settings.isSaving}
                className="h-9 rounded-md px-3"
              >
                {settings.isSaving ? (
                  <CircleNotch className="size-4 animate-spin" />
                ) : (
                  <FloppyDisk className="size-4" weight="bold" />
                )}
                Save details
              </Button>
              {settings.isSaved ? (
                <span role="status" className="text-xs text-muted-foreground">
                  Saved.
                </span>
              ) : null}
            </div>
          ) : null}
          {settings.detailsError ? (
            <p role="alert" className="text-sm text-destructive">
              {settings.detailsError}
            </p>
          ) : null}
          {settings.canManage ? null : (
            <p className="text-sm text-muted-foreground">
              {project.status === "COMPLETED"
                ? "Completed projects are read-only."
                : "Only workspace owners and the project owner can change these settings."}
            </p>
          )}
        </form>
      </SettingsSection>

      <SettingsSection
        title="Lifecycle"
        description={statusDescriptions[project.status]}
      >
        <div className="flex flex-wrap gap-2">
          {settings.canManage && project.status === "ACTIVE" ? (
            <>
              <Button
                type="button"
                variant="outline"
                className="h-9 rounded-md px-3"
                onClick={() => settings.askToConfirm("complete")}
              >
                <CheckCircle className="size-4" />
                Complete project
              </Button>
              <Button
                type="button"
                variant="outline"
                className="h-9 rounded-md px-3"
                onClick={() => settings.askToConfirm("archive")}
              >
                <Archive className="size-4" />
                Archive project
              </Button>
            </>
          ) : null}
          {settings.canManage && project.status === "ARCHIVED" ? (
            <Button
              type="button"
              variant="outline"
              className="h-9 rounded-md px-3"
              onClick={() => settings.askToConfirm("restore")}
            >
              <ArrowCounterClockwise className="size-4" />
              Restore project
            </Button>
          ) : null}
          {settings.canPermanentlyDelete ? (
            <Button
              type="button"
              variant="destructive"
              className="h-9 rounded-md px-3"
              onClick={() => settings.askToConfirm("delete")}
            >
              <Trash className="size-4" />
              Delete permanently
            </Button>
          ) : null}
        </div>
        {settings.actionError ? (
          <p role="alert" className="text-sm text-destructive">
            {settings.actionError}
          </p>
        ) : null}
      </SettingsSection>

      <BoardSettings project={project} workspaceId={settings.workspaceId} />

      <ProjectActionDialog
        action={settings.confirmAction}
        isBusy={settings.isRunningAction}
        onCancel={settings.cancelAction}
        onConfirm={() => void settings.runConfirmedAction()}
      />
    </div>
  )
}
