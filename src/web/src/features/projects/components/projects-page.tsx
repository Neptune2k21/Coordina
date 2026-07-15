import { FolderOpen, Kanban, ShieldCheck } from "@phosphor-icons/react"
import { useMemo } from "react"

import { Button } from "@/components/ui/button"
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import { Separator } from "@/components/ui/separator"
import { ProjectActionDialog } from "@/features/projects/components/project-action-dialog"
import { ProjectCard } from "@/features/projects/components/project-card"
import { ProjectCreateDialog } from "@/features/projects/components/project-create-dialog"
import { ProjectEditDialog } from "@/features/projects/components/project-edit-dialog"
import { ProjectsSkeleton } from "@/features/projects/components/projects-skeleton"
import { useProjects } from "@/features/projects/use-projects"

export function ProjectsPage({
  onOpenProject,
}: {
  onOpenProject?: (projectId: string) => void
}) {
  const {
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
    sessionUserId,
    setConfirmAction,
    setEditingProject,
    setIncludeArchived,
    setIncludeCompleted,
  } = useProjects()

  const projectCountLabel = useMemo(() => {
    if (projects.length === 1) {
      return "1 project"
    }

    return `${projects.length} projects`
  }, [projects.length])

  return (
    <div className="grid gap-4">
      <section className="rounded-md border border-zinc-950/10 bg-white p-4 shadow-xs dark:border-white/10 dark:bg-white/[0.055]">
        <div className="flex flex-col justify-between gap-3 lg:flex-row lg:items-center">
          <div className="min-w-0">
            <div className="inline-flex items-center gap-2 rounded-md border border-zinc-950/10 bg-zinc-50 px-2.5 py-1.5 text-[12px] font-semibold text-zinc-700 dark:border-white/10 dark:bg-white/[0.08] dark:text-zinc-200">
              <Kanban className="size-4" weight="bold" />
              Projects
            </div>
            <h1 className="mt-3 text-xl leading-tight font-semibold tracking-normal text-zinc-950 dark:text-white">
              {activeWorkspace?.name}
            </h1>
            <p className="mt-1 max-w-2xl text-sm leading-6 text-muted-foreground">
              Structured work in the active workspace.
            </p>
          </div>
          <ProjectCreateDialog
            disabled={!activeWorkspaceId}
            isCreating={isCreating}
            onCreate={handleCreate}
          />
        </div>
      </section>

      <Card className="bg-white shadow-xs dark:bg-white/[0.055]">
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
                className="rounded-md"
                onClick={() => setIncludeArchived((current) => !current)}
              >
                Archived
              </Button>
              <Button
                type="button"
                variant={includeCompleted ? "default" : "outline"}
                size="sm"
                className="rounded-md"
                onClick={() => setIncludeCompleted((current) => !current)}
              >
                Completed
              </Button>
              <div className="flex items-center gap-2 rounded-md border border-zinc-950/10 bg-zinc-50 px-3 py-2 text-xs font-medium text-muted-foreground dark:border-white/10 dark:bg-white/[0.06]">
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
                  sessionUserId={sessionUserId}
                  onArchive={(item) =>
                    setConfirmAction({ type: "archive", project: item })
                  }
                  onEdit={setEditingProject}
                  onOpen={onOpenProject}
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
