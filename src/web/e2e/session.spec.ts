import { expect, test, type BrowserContext } from "@playwright/test"

import { apiUrl, seedBoard, signIn } from "./support/seeded-board"

async function refreshCookie(context: BrowserContext) {
  const cookies = await context.cookies()

  return cookies.find((cookie) => cookie.name === "coordina_refresh")
}

test("an expired session returns to sign-in with a notice", async ({
  page,
}) => {
  const seeded = await seedBoard(page, "BASIC")
  await signIn(page, seeded)
  await expect(page.getByRole("heading", { name: "My work" })).toBeVisible()

  // The API now rejects the token, as it does once the token expires.
  await page.route(`${apiUrl}/**`, (route) =>
    route.fulfill({ status: 401, body: "" })
  )
  await page.getByRole("button", { name: "Projects" }).click()

  await expect(page).toHaveURL(/\/login$/)
  await expect(page.getByRole("status")).toHaveText(
    "Your session expired. Sign in again."
  )
})

test("reloading keeps the user signed in without storing the token", async ({
  page,
}) => {
  const seeded = await seedBoard(page, "BASIC")
  await signIn(page, seeded)
  await expect(page.getByRole("heading", { name: "My work" })).toBeVisible()

  await page.reload()

  await expect(page.getByRole("heading", { name: "My work" })).toBeVisible()
  const storage = await page.evaluate(() =>
    JSON.stringify({ ...localStorage, ...sessionStorage })
  )
  expect(storage).not.toMatch(/accessToken|eyJ/)
  expect(await refreshCookie(page.context())).toMatchObject({
    httpOnly: true,
    secure: true,
    sameSite: "Strict",
    path: "/auth",
  })
})

test("tabs reloading together stay signed in", async ({ context, page }) => {
  const seeded = await seedBoard(page, "BASIC")
  await signIn(page, seeded)
  const tabs = [page, await context.newPage()]
  await tabs[1].goto("/app")

  for (let round = 0; round < 3; round++) {
    await Promise.all(tabs.map((tab) => tab.reload()))

    for (const tab of tabs) {
      await expect(tab.getByRole("heading", { name: "My work" })).toBeVisible()
    }
  }

  expect(await refreshCookie(context)).toBeDefined()
})

test("reloading again while the session is restored keeps it", async ({
  context,
  page,
}) => {
  const seeded = await seedBoard(page, "BASIC")
  await signIn(page, seeded)
  await expect(page.getByRole("heading", { name: "My work" })).toBeVisible()

  for (let reload = 0; reload < 3; reload++) {
    const refreshing = page.waitForRequest(
      (request) =>
        request.url() === `${apiUrl}/auth/refresh` &&
        request.method() === "POST"
    )
    await page.reload({ waitUntil: "commit" })
    await refreshing
  }

  await page.reload()

  await expect(page.getByRole("heading", { name: "My work" })).toBeVisible()
  expect(await refreshCookie(context)).toBeDefined()
})

test("signing out revokes the session, so a reload stays signed out", async ({
  page,
}) => {
  const seeded = await seedBoard(page, "BASIC")
  await signIn(page, seeded)

  await page.getByRole("button", { name: "User actions" }).click()
  await page.getByRole("menuitem", { name: /sign out/i }).click()
  await expect(page).toHaveURL(/\/login$/)
  await page.reload()

  await expect(page).toHaveURL(/\/login$/)
  await expect(page.getByLabel("Email")).toBeVisible()
})
