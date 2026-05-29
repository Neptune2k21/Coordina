import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react"
import { afterEach, describe, expect, it, vi } from "vitest"

import { AuthProvider } from "@/features/auth/auth-context"
import { AuthSection } from "@/features/auth/components/auth-section"

describe("AuthSection", () => {
  afterEach(() => {
    cleanup()
    localStorage.clear()
    window.history.replaceState({}, "", "/login")
    vi.restoreAllMocks()
  })

  it("renders the login form by default", () => {
    renderAuthSection()

    expect(
      screen.getByRole("heading", { name: "Welcome back" })
    ).toBeInTheDocument()
    expect(screen.getByLabelText("Email")).toBeInTheDocument()
    expect(screen.getByLabelText("Password")).toBeInTheDocument()
    expect(screen.queryByLabelText("Full name")).not.toBeInTheDocument()
  })

  it("shows client validation errors", async () => {
    renderAuthSection()

    fireEvent.click(
      screen.getByRole("button", { name: /sign in to coordina/i })
    )

    expect(await screen.findByText("Enter a valid email.")).toBeInTheDocument()
    expect(
      await screen.findByText("Password must be at least 8 characters.")
    ).toBeInTheDocument()
  })

  it("registers a user and redirects to the app", async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({
        accessToken: "token-123",
        expiresAt: "2026-05-29T12:00:00.000Z",
        user: {
          id: "user-1",
          email: "ada@coordina.test",
          name: "Ada Lovelace",
        },
      }),
    })
    vi.stubGlobal("fetch", fetchMock)

    renderAuthSection()

    fireEvent.click(screen.getByRole("button", { name: "Create an account" }))

    fireEvent.change(screen.getByLabelText("Full name"), {
      target: { value: "Ada Lovelace" },
    })
    fireEvent.change(screen.getByLabelText("Email"), {
      target: { value: "ada@coordina.test" },
    })
    fireEvent.change(screen.getByLabelText("Password"), {
      target: { value: "Password123!" },
    })
    fireEvent.click(
      screen.getByRole("button", { name: /create workspace account/i })
    )

    await waitFor(() => {
      expect(window.location.pathname).toBe("/app")
    })

    expect(localStorage.getItem("coordina.auth.session")).toContain(
      "ada@coordina.test"
    )

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith(
        "http://localhost:5050/auth/register",
        expect.objectContaining({
          method: "POST",
        })
      )
    })
  })
})

function renderAuthSection() {
  return render(
    <AuthProvider>
      <AuthSection />
    </AuthProvider>
  )
}
