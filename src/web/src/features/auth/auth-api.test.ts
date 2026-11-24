import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"

import { refreshSession } from "@/features/auth/auth-api"

const session = {
  accessToken: "token-a",
  expiresAt: "2026-11-08T10:00:00Z",
  user: { id: "user-1", email: "ada@coordina.test", name: "Ada" },
}

/** Answers each refresh with the next status, then with a session. */
function serveRefreshes(...statuses: number[]) {
  const fetchMock = vi.fn(() => {
    const status = statuses.shift() ?? 200

    return Promise.resolve({
      ok: status < 400,
      status,
      json: () => Promise.resolve(status < 400 ? session : {}),
    })
  })
  vi.stubGlobal("fetch", fetchMock)

  return fetchMock
}

describe("refreshSession", () => {
  beforeEach(() => {
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.useRealTimers()
    vi.unstubAllGlobals()
  })

  it("shares one request between calls made together", async () => {
    const fetchMock = serveRefreshes()

    const [first, second] = await Promise.all([
      refreshSession(),
      refreshSession(),
    ])

    expect(fetchMock).toHaveBeenCalledTimes(1)
    expect(first).toEqual(session)
    expect(second).toBe(first)
  })

  it("sends a new request once the previous one is done", async () => {
    const fetchMock = serveRefreshes()

    await refreshSession()
    await refreshSession()

    expect(fetchMock).toHaveBeenCalledTimes(2)
  })

  it("holds the lock the other tabs wait for", async () => {
    serveRefreshes()
    const request = vi.fn((_name: string, refresh: () => Promise<unknown>) =>
      refresh()
    )
    vi.stubGlobal("navigator", { locks: { request } })

    await refreshSession()

    expect(request).toHaveBeenCalledWith(
      "coordina.refresh",
      expect.any(Function)
    )
  })

  it("lets the new cookie arrive when the page reloads meanwhile", async () => {
    const fetchMock = serveRefreshes()

    await refreshSession()

    const [, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit]
    expect(init.keepalive).toBe(true)
  })

  it("retries until the new cookie arrives after a lost race", async () => {
    const fetchMock = serveRefreshes(409, 409)

    const refreshed = refreshSession()
    await vi.runAllTimersAsync()

    expect(await refreshed).toEqual(session)
    expect(fetchMock).toHaveBeenCalledTimes(3)
  })

  it("gives up when the race never settles", async () => {
    const fetchMock = serveRefreshes(409, 409, 409, 409, 409)

    const refreshed = refreshSession().catch((error: unknown) => error)
    await vi.runAllTimersAsync()

    expect(await refreshed).toMatchObject({ status: 409 })
    expect(fetchMock).toHaveBeenCalledTimes(5)
  })

  it("does not retry without a valid cookie", async () => {
    const fetchMock = serveRefreshes(401)

    await expect(refreshSession()).rejects.toMatchObject({ status: 401 })
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })
})
