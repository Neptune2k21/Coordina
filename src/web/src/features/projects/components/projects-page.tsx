import {
  Archive,
  CircleNotch,
  DotsThreeVertical,
  FolderOpen,
  Kanban,
  PencilSimple,
  Prohibit,
  ArrowCounterClockwise,
  ShieldCheck,
  Trash,
} from "@phosphor-icons/react"
import { useCallback, useEffect, useMemo, useState } from "react"

import { Button } from "@/components/ui/button"
import { Badge } from "@/components/ui/badge"
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import { Separator } from "@/components/ui/separator"
import { Skeleton } from "@/components/ui/skeleton"
import { useAuth } from "@/features/auth/auth-context"
import { ApiError } from "@/features/auth/auth-api"
import {
  archiveProject,
  createProject,
  permanentlyDeleteProject,
  restoreProject,
  updateProject,
  listProjects,
} from "@/features/projects/project-api"
import { ProjectCreateDialog } from "@/features/projects/components/project-create-dialog"
import { ProjectEditDialog } from "@/features/projects/components/project-edit-dialog"
import type {
  Project,
  ProjectInput,
  ProjectUpdateInput,
} from "@/features/projects/project-types"
import { useWorkspaces } from "@/features/workspaces/workspace-context"

export function ProjectsPage() {
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
  const [confirmAction, setConfirmAction] = useState<{
    type: "archive" | "restore" | "delete"
    project: Project
  } | null>(null)

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

  const projectCountLabel = useMemo(() => {
    if (projects.length === 1) {
      return "1 project"
    }

    return `${projects.length} projects`
  }, [projects.length])

  return (
    <div className="grid gap-5 lg:gap-6">
      <section className="rounded-[32px] border border-zinc-950/[0.08] bg-white/78 p-5 shadow-[0_32px_110px_rgba(24,24,27,0.12)] backdrop-blur-2xl sm:p-6 lg:p-8 dark:border-white/10 dark:bg-white/[0.055]">
        <div className="flex flex-col justify-between gap-5 lg:flex-row lg:items-end">
          <div className="min-w-0">
            <div className="inline-flex items-center gap-2 rounded-full border border-zinc-950/10 bg-white/72 px-3 py-1.5 text-[12px] font-semibold text-zinc-700 shadow-[0_12px_34px_rgba(24,24,27,0.08)] backdrop-blur-xl dark:border-white/10 dark:bg-white/[0.08] dark:text-zinc-200">
              <Kanban className="size-4" weight="bold" />
              Projects
            </div>
            <h1 className="mt-5 text-3xl leading-tight font-semibold tracking-normal text-zinc-950 sm:text-4xl dark:text-white">
              {activeWorkspace?.name}
            </h1>
            <p className="mt-3 max-w-2xl text-sm leading-7 text-zinc-600 sm:text-base dark:text-zinc-300">
              Structured work in the active workspace. Every request is scoped
              by workspace membership on the API.
            </p>
          </div>
          <ProjectCreateDialog
            disabled={!activeWorkspaceId}
            isCreating={isCreating}
            onCreate={handleCreate}
          />
        </div>
      </section>

      <Card className="bg-white/82 shadow-[0_24px_70px_rgba(24,24,27,0.08)] backdrop-blur-xl dark:bg-white/[0.055]">
        <CardHeader>
          <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
            <div>
              <div className="flex items-center gap-2">
                <FolderOpen className="size-4" />
                <CardTitle>Workspace projects</CardTitle>
              </div>
              <CardDescription>
                {isLoading ? "Loading projects" : projectCountLabel}
              </CardDescription>
            </div>
            <div className="flex flex-wrap items-center gap-2">
              <Button
                type="button"
                variant={includeArchived ? "default" : "outline"}
                size="sm"
                className="rounded-full"
                onClick={() => setIncludeArchived((current) => !current)}
              >
                Archived
              </Button>
              <Button
                type="button"
                variant={includeCompleted ? "default" : "outline"}
                size="sm"
                className="rounded-full"
                onClick={() => setIncludeCompleted((current) => !current)}
              >
                Completed
              </Button>
              <div className="flex items-center gap-2 rounded-full border border-zinc-950/10 bg-white/60 px-3 py-2 text-xs font-medium text-muted-foreground dark:border-white/10 dark:bg-white/[0.06]">
                <ShieldCheck className="size-4 text-teal-600 dark:text-teal-300" />
                {activeWorkspace?.role ?? "MEMBER"}
              </div>
            </div>
          </div>
        </CardHeader>
        <CardContent className="grid gap-4">
          <Separator />
          {error ? (
            <div className="rounded-md border border-destructive/25 bg-destructive/5 px-3 py-2 text-sm text-destructive">
              {error}
            </div>
          ) : null}
          {isLoading ? (
            <ProjectsSkeleton />
          ) : projects.length > 0 ? (
            <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
              {projects.map((project) => (
                <ProjectCard
                  key={project.id}
                  isMutating={mutatingProjectId === project.id}
                  isWorkspaceOwner={isWorkspaceOwner}
                  project={project}
                  sessionUserId={session?.user.id ?? null}
                  onArchive={(item) =>
                    setConfirmAction({ type: "archive", project: item })
                  }
                  onEdit={setEditingProject}
                  onPermanentDelete={(item) =>
                    setConfirmAction({ type: "delete", project: item })
                  }
                  onRestore={(item) =>
                    setConfirmAction({ type: "restore", project: item })
                  }
                />
              ))}
            </div>
          ) : (
            <div className="grid place-items-center rounded-md border border-dashed border-zinc-950/15 bg-white/54 px-4 py-12 text-center dark:border-white/15 dark:bg-white/[0.035]">
              <div className="max-w-sm">
                <span className="mx-auto grid size-11 place-items-center rounded-2xl bg-zinc-950 text-white dark:bg-white dark:text-zinc-950">
                  <Kanban className="size-5" weight="bold" />
                </span>
                <h2 className="mt-4 text-base font-semibold">
                  No projects yet
                </h2>
                <p className="mt-2 text-sm leading-6 text-muted-foreground">
                  Create the first project for this workspace.
                </p>
              </div>
            </div>
          )}
        </CardContent>
      </Card>
      <ProjectEditDialog
        isSaving={isSaving}
        open={editingProject !== null}
        project={editingProject}
        onOpenChange={(open) => {
          if (!open) {
            setEditingProject(null)
          }
        }}
        onSave={handleEdit}
      />
      <ProjectActionDialog
        action={confirmAction}
        isBusy={mutatingProjectId !== null}
        onCancel={() => setConfirmAction(null)}
        onConfirm={() => void handleConfirmAction()}
      />
    </div>
  )
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

function ProjectCard({
  isMutating,
  isWorkspaceOwner,
  onArchive,
  onEdit,
  onPermanentDelete,
  onRestore,
  project,
  sessionUserId,
}: {
  isMutating: boolean
  isWorkspaceOwner: boolean
  onArchive: (project: Project) => void
  onEdit: (project: Project) => void
  onPermanentDelete: (project: Project) => void
  onRestore: (project: Project) => void
  project: Project
  sessionUserId: string | null
}) {
  const isProjectOwner = project.projectOwnerId === sessionUserId
  const canManage =
    (isWorkspaceOwner || isProjectOwner) && project.status !== "COMPLETED"
  const canPermanentlyDelete = isWorkspaceOwner

  return (
    <Card className="bg-white/74 transition-transform duration-200 hover:-translate-y-1 dark:bg-white/[0.045]">
      <CardHeader>
        <div className="flex items-start justify-between gap-3">
          <div className="min-w-0">
            <div className="flex items-center gap-2">
              <span className="grid size-8 place-items-center rounded-2xl bg-zinc-950/[0.06] text-sm dark:bg-white/[0.08]">
                {project.icon ?? "⌁"}
              </span>
              <CardTitle className="truncate text-base">
                {project.name}
              </CardTitle>
            </div>
            <CardDescription className="mt-2 flex flex-wrap items-center gap-2">
              {project.key ? (
                <span className="font-mono">{project.key}</span>
              ) : null}
              <StatusBadge status={project.status} />
              {project.color ? (
                <span className="rounded-full border border-zinc-950/10 px-2 py-0.5 text-[11px] dark:border-white/10">
                  {project.color}
                </span>
              ) : null}
            </CardDescription>
          </div>
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button
                type="button"
                variant="ghost"
                size="icon"
                className="size-9 shrink-0 rounded-full"
                aria-label={`${project.name} actions`}
              >
                {isMutating ? (
                  <CircleNotch className="size-4 animate-spin" />
                ) : (
                  <DotsThreeVertical className="size-5" weight="bold" />
                )}
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              <DropdownMenuLabel>Project actions</DropdownMenuLabel>
              <DropdownMenuSeparator />
              {canManage ? (
                <DropdownMenuItem onSelect={() => onEdit(project)}>
                  <PencilSimple className="size-4" />
                  Edit project
                </DropdownMenuItem>
              ) : null}
              {canManage && project.status !== "ARCHIVED" ? (
                <DropdownMenuItem onSelect={() => onArchive(project)}>
                  <Archive className="size-4" />
                  Archive project
                </DropdownMenuItem>
              ) : null}
              {canManage && project.status === "ARCHIVED" ? (
                <DropdownMenuItem onSelect={() => onRestore(project)}>
                  <ArrowCounterClockwise className="size-4" />
                  Restore project
                </DropdownMenuItem>
              ) : null}
              {!canManage ? (
                <DropdownMenuItem disabled>
                  <Prohibit className="size-4" />
                  View only
                </DropdownMenuItem>
              ) : null}
              {canPermanentlyDelete ? (
                <>
                  <DropdownMenuSeparator />
                  <DropdownMenuItem
                    onSelect={() => onPermanentDelete(project)}
                    className="text-destructive focus:text-destructive"
                  >
                    <Trash className="size-4" />
                    Delete permanently
                  </DropdownMenuItem>
                </>
              ) : null}
            </DropdownMenuContent>
          </DropdownMenu>
        </div>
      </CardHeader>
      <CardContent className="grid gap-3">
        <p className="min-h-12 text-sm leading-6 text-muted-foreground">
          {project.description ?? "No description added."}
        </p>
        <Separator />
        <div className="grid gap-1 text-xs text-muted-foreground">
          <span>Owner: {project.projectOwnerName ?? "Workspace member"}</span>
          <span>
            Updated{" "}
            {new Intl.DateTimeFormat(undefined, {
              dateStyle: "medium",
            }).format(new Date(project.updatedAt))}
          </span>
        </div>
      </CardContent>
    </Card>
  )
}

function StatusBadge({ status }: { status: Project["status"] }) {
  const className =
    status === "ACTIVE"
      ? "border-teal-500/20 bg-teal-500/10 text-teal-700 dark:text-teal-200"
      : status === "COMPLETED"
        ? "border-indigo-500/20 bg-indigo-500/10 text-indigo-700 dark:text-indigo-200"
        : "border-zinc-500/20 bg-zinc-500/10 text-zinc-700 dark:text-zinc-200"

  return <Badge className={className}>{status}</Badge>
}

function ProjectActionDialog({
  action,
  isBusy,
  onCancel,
  onConfirm,
}: {
  action: { type: "archive" | "restore" | "delete"; project: Project } | null
  isBusy: boolean
  onCancel: () => void
  onConfirm: () => void
}) {
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
      <DialogContent className="rounded-[24px] border-zinc-950/10 bg-white/94 p-6 shadow-[0_34px_110px_rgba(24,24,27,0.2)] backdrop-blur-2xl dark:border-white/10 dark:bg-zinc-950/94">
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>{description}</DialogDescription>
        </DialogHeader>
        <DialogFooter>
          <Button
            type="button"
            variant="outline"
            className="h-10 rounded-full px-4"
            onClick={onCancel}
            disabled={isBusy}
          >
            Cancel
          </Button>
          <Button
            type="button"
            variant={action?.type === "delete" ? "destructive" : "default"}
            className="h-10 rounded-full px-4"
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

function ProjectsSkeleton() {
  return (
    <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
      {Array.from({ length: 3 }).map((_, index) => (
        <Card key={index} className="bg-white/60 dark:bg-white/[0.04]">
          <CardHeader>
            <Skeleton className="h-5 w-2/3" />
            <Skeleton className="h-4 w-1/3" />
          </CardHeader>
          <CardContent className="grid gap-2">
            <Skeleton className="h-4 w-full" />
            <Skeleton className="h-4 w-4/5" />
          </CardContent>
        </Card>
      ))}
    </div>
  )
}
