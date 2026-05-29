import { Header } from "@/components/layout/header"
import { Hero } from "@/components/sections/hero"
import { AuthSection } from "@/features/auth/components/auth-section"

export function App() {
  const isLoginPage = window.location.pathname === "/login"

  return (
    <div className="min-h-svh bg-background text-foreground">
      {isLoginPage ? (
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
