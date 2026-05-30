import { CircleNotch } from "@phosphor-icons/react"
import { useEffect, useState } from "react"

import { Card, CardContent } from "@/components/ui/card"
import { SaasShell } from "@/components/layout/saas-shell"
import { ProjectShell } from "@/features/projects/components/project-shell"
import { ProjectsPage } from "@/features/projects/components/projects-page"
import { WorkspaceDashboard } from "@/features/workspaces/components/workspace-dashboard"
import { WorkspaceOnboarding } from "@/features/workspaces/components/workspace-onboarding"
import { WorkspaceSettingsPage } from "@/features/workspaces/components/workspace-settings-page"
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
  const [currentPath, setCurrentPath] = useState(window.location.pathname)

  useEffect(() => {
    const handlePopState = () => setCurrentPath(window.location.pathname)

    window.addEventListener("popstate", handlePopState)
    return () => window.removeEventListener("popstate", handlePopState)
  }, [])

  function navigate(path: string) {
    window.history.pushState({}, "", path)
    window.dispatchEvent(new PopStateEvent("popstate"))
    setCurrentPath(path)
  }

  const isProjectsPage = currentPath.startsWith("/app/projects")
  const isSettingsPage = currentPath.startsWith("/app/workspace-settings")
  const projectMatch = currentPath.match(/^\/app\/projects\/([^/]+)$/)

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
    <SaasShell currentPath={currentPath} onNavigate={navigate}>
      {projectMatch ? (
        <ProjectShell
          projectId={projectMatch[1]}
          onBack={() => navigate("/app/projects")}
        />
      ) : isProjectsPage ? (
        <ProjectsPage
          onOpenProject={(projectId) => navigate(`/app/projects/${projectId}`)}
        />
      ) : isSettingsPage ? (
        <WorkspaceSettingsPage />
      ) : (
        <WorkspaceDashboard />
      )}
    </SaasShell>
  )
}
