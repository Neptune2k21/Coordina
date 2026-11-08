import { expect, test } from "@playwright/test"

import { apiUrl, seedBoard, signIn } from "./support/seeded-board"

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
  const cookies = await page.context().cookies()
  expect(
    cookies.find((cookie) => cookie.name === "coordina_refresh")
  ).toMatchObject({
    httpOnly: true,
    secure: true,
    sameSite: "Strict",
    path: "/auth",
  })
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
