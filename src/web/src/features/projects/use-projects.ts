import { useCallback, useEffect, useState } from "react"

import { useAuth } from "@/features/auth/auth-context"
import { ApiError } from "@/lib/api"
import {
  archiveProject,
  createProject,
  permanentlyDeleteProject,
  restoreProject,
  updateProject,
  listProjects,
} from "@/features/projects/project-api"
import type { Project, ProjectInput, ProjectUpdateInput } from "@/types/project"
import { useWorkspaces } from "@/features/workspaces/workspace-context"

export type ProjectAction = {
  type: "archive" | "restore" | "delete"
  project: Project
}

export function useProjects() {
  const { session, signOut } = useAuth()
  const { activeWorkspace, activeWorkspaceId } = useWorkspaces()
  const [projects, setProjects] = useState<Project[]>([])
  const [error, setError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const [isCreating, setIsCreating] = useState(false)
  const [isSaving, setIsSaving] = useState(false)
  const [includeArchived, setIncludeArchived] = useState(false)
  const [includeCompleted, setIncludeCompleted] = useState(false)
  const [mutatingProjectId, setMutatingProjectId] = useState<string | null>(
    null
  )
  const [editingProject, setEditingProject] = useState<Project | null>(null)
  const [confirmAction, setConfirmAction] = useState<ProjectAction | null>(null)

  const isWorkspaceOwner = activeWorkspace?.role === "OWNER"

  const handleRequestError = useCallback(
    (requestError: unknown, fallback: string) => {
      if (requestError instanceof ApiError) {
        setError(requestError.message)

        if (requestError.status === 401) {
          signOut()
        }
        return
      }

      setError(fallback)
    },
    [signOut]
  )

  const loadProjects = useCallback(async () => {
    if (!session || !activeWorkspaceId) {
      setProjects([])
      return
    }

    setIsLoading(true)
    setError(null)

    try {
      setProjects(
        await listProjects(session.accessToken, activeWorkspaceId, {
          includeArchived,
          includeCompleted,
        })
      )
    } catch (requestError) {
      handleRequestError(requestError, "Unable to load projects.")
    } finally {
      setIsLoading(false)
    }
  }, [
    activeWorkspaceId,
    handleRequestError,
    includeArchived,
    includeCompleted,
    session,
  ])

  useEffect(() => {
    queueMicrotask(() => void loadProjects())
  }, [loadProjects])

  async function handleCreate(input: ProjectInput) {
    if (!session || !activeWorkspaceId) {
      throw new Error("Choose a workspace before creating a project.")
    }

    setIsCreating(true)

    try {
      const project = await createProject(
        session.accessToken,
        activeWorkspaceId,
        input
      )
      setProjects((current) =>
        [...current, project].sort((a, b) => a.name.localeCompare(b.name))
      )
    } finally {
      setIsCreating(false)
    }
  }

  async function handleEdit(project: Project, input: ProjectUpdateInput) {
    if (!session || !activeWorkspaceId) {
      return
    }

    setIsSaving(true)
    setError(null)

    try {
      const updated = await updateProject(
        session.accessToken,
        activeWorkspaceId,
        project.id,
        input
      )
      setProjects((current) =>
        current
          .map((item) => (item.id === updated.id ? updated : item))
          .filter((item) =>
            shouldShowProject(item, includeArchived, includeCompleted)
          )
          .sort((a, b) => a.name.localeCompare(b.name))
      )
    } catch (requestError) {
      handleRequestError(requestError, "Project could not be updated.")
      throw requestError
    } finally {
      setIsSaving(false)
    }
  }

  async function handleConfirmAction() {
    if (!session || !activeWorkspaceId || !confirmAction) {
      return
    }

    const { project, type } = confirmAction
    setMutatingProjectId(project.id)
    setError(null)

    try {
      if (type === "archive") {
        await archiveProject(session.accessToken, activeWorkspaceId, project.id)
      } else if (type === "restore") {
        await restoreProject(session.accessToken, activeWorkspaceId, project.id)
      } else {
        await permanentlyDeleteProject(
          session.accessToken,
          activeWorkspaceId,
          project.id
        )
      }

      await loadProjects()
      setConfirmAction(null)
    } catch (requestError) {
      handleRequestError(requestError, "Project action failed.")
    } finally {
      setMutatingProjectId(null)
    }
  }

  return {
    activeWorkspace,
    activeWorkspaceId,
    confirmAction,
    editingProject,
    error,
    handleConfirmAction,
    handleCreate,
    handleEdit,
    includeArchived,
    includeCompleted,
    isCreating,
    isLoading,
    isSaving,
    isWorkspaceOwner,
    mutatingProjectId,
    projects,
    sessionUserId: session?.user.id ?? null,
    setConfirmAction,
    setEditingProject,
    setIncludeArchived,
    setIncludeCompleted,
  }
}

function shouldShowProject(
  project: Project,
  includeArchived: boolean,
  includeCompleted: boolean
) {
  if (project.status === "ARCHIVED") {
    return includeArchived
  }

  if (project.status === "COMPLETED") {
    return includeCompleted
  }

  return true
}
