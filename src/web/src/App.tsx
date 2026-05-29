import { Header } from "@/components/layout/header"
import { Hero } from "@/components/sections/hero"
import { AuthSection } from "@/features/auth/components/auth-section"
import { PlatformDocsPage } from "@/features/docs/components/platform-docs-page"

export function App() {
  const isLoginPage = window.location.pathname === "/login"
  const isDocsPage = window.location.pathname === "/docs"

  return (
    <div className="min-h-svh bg-background text-foreground">
      {isDocsPage ? (
        <PlatformDocsPage />
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
