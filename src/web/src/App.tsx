import { DocsPage } from "@/components/docs/docs-page"
import { Header } from "@/components/layout/header"
import { Hero } from "@/components/sections/hero"
import { AuthSection } from "@/features/auth/components/auth-section"

export function App() {
  const isLoginPage = window.location.pathname === "/login"
  const isDocsPage = window.location.pathname === "/docs"

  return (
    <div className="min-h-svh bg-background text-foreground">
      {isDocsPage ? (
        <DocsPage />
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
