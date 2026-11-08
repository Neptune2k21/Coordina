import { afterEach, describe, expect, it, vi } from "vitest"

import { ApiError, apiRequest, onSessionRejected } from "@/lib/api"

function respondWith(status: number, body: object = {}) {
  vi.stubGlobal(
    "fetch",
    vi.fn().mockResolvedValue({
      ok: status < 400,
      status,
      json: () => Promise.resolve(body),
    })
  )
}

describe("apiRequest", () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it("reports a 401 on an authenticated request as a rejected session", async () => {
    const listener = vi.fn()
    const unsubscribe = onSessionRejected(listener)
    respondWith(401, { title: "Unauthorized" })

    await expect(
      apiRequest("/projects", { accessToken: "expired" })
    ).rejects.toBeInstanceOf(ApiError)

    expect(listener).toHaveBeenCalledTimes(1)
    unsubscribe()
  })

  it("keeps a sign-in failure to its own message", async () => {
    const listener = vi.fn()
    const unsubscribe = onSessionRejected(listener)
    respondWith(401)

    await expect(
      apiRequest("/auth/login", {
        method: "POST",
        unauthorizedMessage: "Incorrect email or password.",
      })
    ).rejects.toThrow("Incorrect email or password.")

    expect(listener).not.toHaveBeenCalled()
    unsubscribe()
  })

  it("does not treat other errors as a rejected session", async () => {
    const listener = vi.fn()
    const unsubscribe = onSessionRejected(listener)
    respondWith(403, { message: "Not allowed." })

    await expect(
      apiRequest("/projects", { accessToken: "valid" })
    ).rejects.toThrow("Not allowed.")

    expect(listener).not.toHaveBeenCalled()
    unsubscribe()
  })

  it("stops calling a listener once it unsubscribes", async () => {
    const listener = vi.fn()
    onSessionRejected(listener)()
    respondWith(401)

    await expect(
      apiRequest("/projects", { accessToken: "expired" })
    ).rejects.toBeInstanceOf(ApiError)

    expect(listener).not.toHaveBeenCalled()
  })
})
