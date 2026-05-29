import { useEffect, useState } from "react"

import { AuthProvider, useAuth } from "@/features/auth/auth-context"
import { Header } from "@/components/layout/header"
import { Hero } from "@/components/sections/hero"
import { AuthSection } from "@/features/auth/components/auth-section"
import { PlatformDocsPage } from "@/features/docs/components/platform-docs-page"
import { WorkspaceApp } from "@/features/workspaces/components/workspace-app"

export function App() {
  return (
    <AuthProvider>
      <AppRoutes />
    </AuthProvider>
  )
}

function AppRoutes() {
  const [path, setPath] = useState(window.location.pathname)
  const { isLoading, session } = useAuth()

  useEffect(() => {
    const handlePopState = () => setPath(window.location.pathname)

    window.addEventListener("popstate", handlePopState)
    return () => window.removeEventListener("popstate", handlePopState)
  }, [])

  const isLoginPage = path === "/login"
  const isDocsPage = path === "/docs"
  const isAppPage = path.startsWith("/app")

  useEffect(() => {
    if (isLoading) {
      return
    }

    if (session && (isLoginPage || path === "/")) {
      window.history.replaceState({}, "", "/app")
      window.dispatchEvent(new PopStateEvent("popstate"))
    }

    if (!session && isAppPage) {
      window.history.replaceState({}, "", "/login")
      window.dispatchEvent(new PopStateEvent("popstate"))
    }
  }, [isAppPage, isLoading, isLoginPage, path, session])

  if (isLoading) {
    return <div className="min-h-svh bg-background" />
  }

  return (
    <div className="min-h-svh bg-background text-foreground">
      {isDocsPage ? (
        <PlatformDocsPage />
      ) : isAppPage && session ? (
        <WorkspaceApp />
      ) : isLoginPage ? (
        <AuthSection />
      ) : (
        <>
          <Header />
          <Hero />
        </>
      )}
    </div>
  )
}

export default App
