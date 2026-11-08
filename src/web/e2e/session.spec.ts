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
