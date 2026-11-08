import { act, cleanup, render, screen } from "@testing-library/react"
import { afterEach, describe, expect, it, vi } from "vitest"

import { AuthProvider, useAuth } from "@/features/auth/auth-context"
import { AuthSection } from "@/features/auth/components/auth-section"
import { apiRequest } from "@/lib/api"

const user = { id: "user-1", email: "ada@coordina.test", name: "Ada" }

function session(accessToken: string) {
  return { accessToken, expiresAt: "2026-11-08T10:00:00Z", user }
}

type Reply = { status: number; body?: unknown }

/** Answers each path with the next of its replies, and records the requests. */
function serve(routes: Record<string, Reply[]>) {
  const fetchMock = vi.fn((url: string) => {
    const path = new URL(url).pathname
    const reply = routes[path]?.shift() ?? { status: 404 }

    return Promise.resolve({
      ok: reply.status < 400,
      status: reply.status,
      json: () => Promise.resolve(reply.body ?? {}),
    })
  })
  vi.stubGlobal("fetch", fetchMock)

  return fetchMock
}

function Screen() {
  const { isLoading, session } = useAuth()

  if (isLoading) {
    return null
  }

  return session ? <p>Signed in with {session.accessToken}</p> : <AuthSection />
}

function renderApp() {
  render(
    <AuthProvider>
      <Screen />
    </AuthProvider>
  )
}

describe("AuthProvider", () => {
  afterEach(() => {
    cleanup()
    localStorage.clear()
    vi.unstubAllGlobals()
  })

  it("restores the session from the refresh cookie on load", async () => {
    const fetchMock = serve({
      "/auth/refresh": [{ status: 200, body: session("token-a") }],
    })

    renderApp()

    expect(
      await screen.findByText("Signed in with token-a")
    ).toBeInTheDocument()
    const [, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit]
    expect(init.credentials).toBe("include")
    expect(new Headers(init.headers).get("X-Requested-With")).toBe(
      "coordina-web"
    )
    expect(JSON.stringify(localStorage)).not.toContain("token-a")
  })

  it("starts signed out, without a notice, when there is no refresh cookie", async () => {
    serve({ "/auth/refresh": [{ status: 401 }, { status: 401 }] })

    renderApp()

    expect(
      await screen.findByRole("heading", { name: "Welcome back" })
    ).toBeInTheDocument()
    expect(screen.queryByRole("status")).not.toBeInTheDocument()
  })

  it("renews an expired access token and retries the request", async () => {
    const fetchMock = serve({
      "/auth/refresh": [
        { status: 200, body: session("token-a") },
        { status: 200, body: session("token-b") },
      ],
      "/projects": [{ status: 401 }, { status: 200, body: ["project"] }],
    })
    renderApp()
    await screen.findByText("Signed in with token-a")

    let projects: unknown
    await act(async () => {
      projects = await apiRequest("/projects", { accessToken: "token-a" })
    })

    expect(projects).toEqual(["project"])
    const retried = fetchMock.mock.calls.at(-1) as unknown as [
      string,
      RequestInit,
    ]
    expect(new Headers(retried[1].headers).get("Authorization")).toBe(
      "Bearer token-b"
    )
    expect(screen.getByText("Signed in with token-b")).toBeInTheDocument()
  })

  it("signs out with a notice when the session cannot be renewed", async () => {
    serve({
      "/auth/refresh": [
        { status: 200, body: session("token-a") },
        { status: 401 },
        { status: 401 },
      ],
      "/projects": [{ status: 401 }],
    })
    renderApp()
    await screen.findByText("Signed in with token-a")

    await act(async () => {
      await apiRequest("/projects", { accessToken: "token-a" }).catch(
        () => undefined
      )
    })

    expect(await screen.findByRole("status")).toHaveTextContent(
      "Your session expired. Sign in again."
    )
  })

  it("does not show the notice when a sign-in fails", async () => {
    serve({
      "/auth/refresh": [{ status: 401 }, { status: 401 }],
      "/auth/login": [{ status: 401 }],
    })
    renderApp()
    await screen.findByRole("heading", { name: "Welcome back" })

    await act(async () => {
      await apiRequest("/auth/login", {
        method: "POST",
        unauthorizedMessage: "Email or password is incorrect.",
      }).catch(() => undefined)
    })

    expect(screen.queryByRole("status")).not.toBeInTheDocument()
  })
})
