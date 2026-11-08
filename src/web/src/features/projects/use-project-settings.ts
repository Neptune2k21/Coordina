import { useState } from "react"

import { useAuth } from "@/features/auth"
import {
  archiveProject,
  getProject,
  permanentlyDeleteProject,
  restoreProject,
  updateProject,
} from "@/features/projects/project-api"
import {
  detailsOf,
  projectErrorMessage,
  type ProjectDetails,
} from "@/features/projects/project-details"
import type { ProjectAction } from "@/features/projects/use-projects"
import { useWorkspaces } from "@/features/workspaces"
import type { Project } from "@/types/project"

type ProjectSettingsOptions = {
  project: Project
  onProjectChange: (project: Project) => void
  onDeleted: () => void
}

/**
 * The project part of the Settings tab: its details and lifecycle actions,
 * with the same permissions as the project list. A completed project is
 * read-only; only a workspace owner can delete one permanently.
 */
export function useProjectSettings({
  onDeleted,
  onProjectChange,
  project,
}: ProjectSettingsOptions) {
  const { session } = useAuth()
  const { activeWorkspace } = useWorkspaces()
  const [details, setDetails] = useState<ProjectDetails>(() =>
    detailsOf(project)
  )
  const [isSaving, setIsSaving] = useState(false)
  const [isSaved, setIsSaved] = useState(false)
  const [detailsError, setDetailsError] = useState<string | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)
  const [confirmAction, setConfirmAction] = useState<ProjectAction | null>(null)
  const [isRunningAction, setIsRunningAction] = useState(false)

  const accessToken = session?.accessToken ?? ""
  const workspaceId = activeWorkspace?.id ?? ""
  const isWorkspaceOwner = activeWorkspace?.role === "OWNER"
  const canManage =
    (isWorkspaceOwner || project.projectOwnerId === session?.user.id) &&
    project.status !== "COMPLETED"

  function changeDetails(next: ProjectDetails) {
    setDetails(next)
    setIsSaved(false)
  }

  async function saveDetails() {
    setIsSaving(true)
    setDetailsError(null)

    try {
      onProjectChange(
        await updateProject(accessToken, workspaceId, project.id, details)
      )
      setIsSaved(true)
    } catch (requestError) {
      setDetailsError(
        projectErrorMessage(requestError, "Project could not be updated.")
      )
    } finally {
      setIsSaving(false)
    }
  }

  async function runConfirmedAction() {
    if (!confirmAction) {
      return
    }

    setIsRunningAction(true)
    setActionError(null)

    try {
      if (confirmAction.type === "delete") {
        await permanentlyDeleteProject(accessToken, workspaceId, project.id)
        onDeleted()
        return
      }

      if (confirmAction.type === "archive") {
        await archiveProject(accessToken, workspaceId, project.id)
        onProjectChange(await getProject(accessToken, workspaceId, project.id))
      } else if (confirmAction.type === "restore") {
        onProjectChange(
          await restoreProject(accessToken, workspaceId, project.id)
        )
      } else {
        onProjectChange(
          await updateProject(accessToken, workspaceId, project.id, {
            status: "COMPLETED",
          })
        )
      }

      setConfirmAction(null)
    } catch (requestError) {
      setActionError(
        projectErrorMessage(requestError, "Project action failed.")
      )
      setConfirmAction(null)
    } finally {
      setIsRunningAction(false)
    }
  }

  return {
    details,
    changeDetails,
    saveDetails,
    isSaving,
    isSaved,
    detailsError,
    actionError,
    canManage,
    canPermanentlyDelete: isWorkspaceOwner,
    confirmAction,
    askToConfirm: (type: ProjectAction["type"]) =>
      setConfirmAction({ type, project }),
    cancelAction: () => setConfirmAction(null),
    runConfirmedAction,
    isRunningAction,
    workspaceId,
  }
}
