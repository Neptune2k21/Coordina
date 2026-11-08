import { expect, test } from "@playwright/test"

import { seedBoard, signIn } from "./support/seeded-board"

test("My work lands after sign-in and opens a card on its board", async ({
  page,
}) => {
  const seeded = await seedBoard(page, "BASIC")

  await signIn(page, seeded)

  await expect(page.getByRole("heading", { name: "My work" })).toBeVisible()
  const next = page.getByRole("region", { name: "Next" })
  const first = next.getByRole("button").first()
  await expect(first).toBeVisible()
  const title = (await first.locator("span.truncate").textContent()) ?? ""

  await first.click()

  await expect(page).toHaveURL(new RegExp(`/app/projects/${seeded.projectId}$`))
  await expect(
    page.getByRole("dialog").getByRole("heading", { name: title })
  ).toBeVisible()
})
