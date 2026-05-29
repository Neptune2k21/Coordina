import { CircleNotch } from "@phosphor-icons/react"

import { Card, CardContent } from "@/components/ui/card"
import { SaasShell } from "@/components/layout/saas-shell"
import { WorkspaceDashboard } from "@/features/workspaces/components/workspace-dashboard"
import { WorkspaceOnboarding } from "@/features/workspaces/components/workspace-onboarding"
import {
  WorkspaceProvider,
  useWorkspaces,
} from "@/features/workspaces/workspace-context"

export function WorkspaceApp() {
  return (
    <WorkspaceProvider>
      <WorkspaceAppContent />
    </WorkspaceProvider>
  )
}

function WorkspaceAppContent() {
  const { activeWorkspace, error, isLoading, workspaces } = useWorkspaces()

  if (isLoading) {
    return (
      <main className="grid min-h-svh place-items-center px-5">
        <Card>
          <CardContent className="flex items-center gap-3 p-5 text-sm text-muted-foreground">
            <CircleNotch className="size-4 animate-spin" />
            Loading workspaces
          </CardContent>
        </Card>
      </main>
    )
  }

  if (error) {
    return (
      <main className="grid min-h-svh place-items-center px-5">
        <Card className="max-w-md border-destructive/30 bg-destructive/5">
          <CardContent className="p-5 text-sm text-destructive">
            {error}
          </CardContent>
        </Card>
      </main>
    )
  }

  if (workspaces.length === 0 || !activeWorkspace) {
    return <WorkspaceOnboarding />
  }

  return (
    <SaasShell>
      <WorkspaceDashboard />
    </SaasShell>
  )
}
