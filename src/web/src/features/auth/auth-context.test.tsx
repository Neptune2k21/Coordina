import { act, cleanup, render, screen, waitFor } from "@testing-library/react"
import { afterEach, describe, expect, it, vi } from "vitest"

import { AuthProvider, useAuth } from "@/features/auth/auth-context"
import { authStorageKey } from "@/features/auth/auth-session"
import { AuthSection } from "@/features/auth/components/auth-section"
import { apiRequest } from "@/lib/api"

const user = { id: "user-1", email: "ada@coordina.test", name: "Ada" }

function Screen() {
  const { isLoading, session } = useAuth()

  if (isLoading) {
    return null
  }

  return session ? <p>Signed in as {session.user.name}</p> : <AuthSection />
}

describe("AuthProvider", () => {
  afterEach(() => {
    cleanup()
    localStorage.clear()
    vi.unstubAllGlobals()
  })

  it("signs the user out with a notice when their session expires", async () => {
    localStorage.setItem(
      authStorageKey,
      JSON.stringify({ accessToken: "token", expiresAt: "2026-01-01", user })
    )
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: () => Promise.resolve(user),
    })
    vi.stubGlobal("fetch", fetchMock)
    render(
      <AuthProvider>
        <Screen />
      </AuthProvider>
    )
    expect(await screen.findByText("Signed in as Ada")).toBeInTheDocument()

    fetchMock.mockResolvedValue({
      ok: false,
      status: 401,
      json: () => Promise.resolve({}),
    })
    await act(async () => {
      await apiRequest("/projects", { accessToken: "token" }).catch(
        () => undefined
      )
    })

    expect(await screen.findByRole("status")).toHaveTextContent(
      "Your session expired. Sign in again."
    )
    expect(localStorage.getItem(authStorageKey)).toBeNull()
  })

  it("does not show the notice when a sign-in fails", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: false,
        status: 401,
        json: () => Promise.resolve({}),
      })
    )
    render(
      <AuthProvider>
        <Screen />
      </AuthProvider>
    )

    await act(async () => {
      await apiRequest("/auth/login", {
        method: "POST",
        unauthorizedMessage: "Incorrect email or password.",
      }).catch(() => undefined)
    })

    await waitFor(() =>
      expect(
        screen.getByRole("heading", { name: "Welcome back" })
      ).toBeInTheDocument()
    )
    expect(screen.queryByRole("status")).not.toBeInTheDocument()
  })
})
