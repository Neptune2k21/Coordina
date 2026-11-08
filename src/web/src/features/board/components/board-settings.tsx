import { SettingsSection } from "@/components/layout/settings-section"
import { useAuth } from "@/features/auth"
import { BoardPlanningSettings } from "@/features/board/components/board-planning-settings"
import { BoardWorkflowSettings } from "@/features/board/components/board-workflow-settings"
import { useBoardWorkflow } from "@/features/board/hooks/use-board-workflow"
import type { Project } from "@/types/project"

type BoardSettingsProps = {
  project: Project
  workspaceId: string
}

/** The board's part of the project settings: its workflow and the planning preferences. */
export function BoardSettings({ project, workspaceId }: BoardSettingsProps) {
  const { session } = useAuth()
  const accessToken = session?.accessToken ?? ""
  const workflow = useBoardWorkflow({
    accessToken,
    workspaceId,
    projectId: project.id,
  })

  if (workflow.isLoading) {
    return null
  }

  if (!workflow.board) {
    return (
      <SettingsSection
        title="Board workflow"
        description="This project has no board yet. Create one from the Board tab, then configure it here."
      >
        {workflow.error ? (
          <p className="text-sm text-destructive">{workflow.error}</p>
        ) : null}
      </SettingsSection>
    )
  }

  return (
    <>
      <SettingsSection
        title="Board workflow"
        description="The lists of the board, in order, and the status each stands for. Cards in a Done list are completed. A board keeps at least one To do and one Done list."
      >
        {project.status === "COMPLETED" ? (
          <p className="text-sm text-muted-foreground">
            Completed projects are read-only.
          </p>
        ) : null}
        <BoardWorkflowSettings
          workflow={workflow}
          isReadOnly={project.status === "COMPLETED"}
        />
        {workflow.error ? (
          <p className="text-sm text-destructive" role="alert">
            {workflow.error}
          </p>
        ) : null}
      </SettingsSection>
      <SettingsSection
        title="Planning"
        description="How Next work ranks your cards. These preferences are yours: teammates keep their own."
      >
        <BoardPlanningSettings
          accessToken={accessToken}
          workspaceId={workspaceId}
          projectId={project.id}
          boardId={workflow.board.id}
        />
      </SettingsSection>
    </>
  )
}
