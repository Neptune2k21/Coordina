import {
  Archive,
  ArrowCounterClockwise,
  CircleNotch,
  DotsThreeVertical,
  Kanban,
  PencilSimple,
  Prohibit,
  Trash,
} from "@phosphor-icons/react"

import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import { Separator } from "@/components/ui/separator"
import { ProjectIconMark } from "@/features/projects/components/project-personalization"
import type { Project } from "@/types/project"

type ProjectCardProps = {
  isMutating: boolean
  isWorkspaceOwner: boolean
  onArchive: (project: Project) => void
  onEdit: (project: Project) => void
  onOpen?: (projectId: string) => void
  onPermanentDelete: (project: Project) => void
  onRestore: (project: Project) => void
  project: Project
  sessionUserId: string | null
}

export function ProjectCard({
  isMutating,
  isWorkspaceOwner,
  onArchive,
  onEdit,
  onOpen,
  onPermanentDelete,
  onRestore,
  project,
  sessionUserId,
}: ProjectCardProps) {
  const isProjectOwner = project.projectOwnerId === sessionUserId
  const canManage =
    (isWorkspaceOwner || isProjectOwner) && project.status !== "COMPLETED"
  const canPermanentlyDelete = isWorkspaceOwner

  return (
    <Card className="bg-white transition-colors hover:bg-zinc-50 dark:bg-white/[0.045] dark:hover:bg-white/[0.07]">
      <CardHeader>
        <div className="flex items-start justify-between gap-3">
          <div className="min-w-0">
            <div className="flex items-center gap-2">
              <ProjectIconMark icon={project.icon} color={project.color} />
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
                <span className="rounded-md border border-zinc-950/10 px-2 py-0.5 text-[11px] dark:border-white/10">
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
                className="size-9 shrink-0 rounded-md"
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
        <Button
          type="button"
          variant="outline"
          className="h-9 rounded-md"
          onClick={() => onOpen?.(project.id)}
        >
          <Kanban className="size-4" weight="bold" />
          Open board
        </Button>
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
