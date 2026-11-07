import { expect, test } from "@playwright/test"

import { openSeededProject, seedBoard } from "./support/seeded-board"

test("board health agrees with the planning panel on blocked work", async ({
  page,
}) => {
  const seeded = await seedBoard(page, "BASIC")
  const waiting = seeded.board.lists
    .flatMap((list) => list.cards)
    .filter(
      (card) =>
        !card.isCompleted &&
        card.dependencies.some((dependency) => !dependency.isCompleted)
    ).length

  expect(waiting).toBeGreaterThan(0)

  await openSeededProject(page, seeded)

  const insights = page.getByRole("button", { name: "Insights" })
  await expect(insights).toBeVisible()

  if ((await insights.getAttribute("aria-pressed")) !== "true") {
    await insights.click()
  }

  const health = page.locator("section", { hasText: "Board health" })

  await expect(
    health.getByText(`${waiting} planned items waiting on another task.`)
  ).toBeVisible()
  await expect(health).toContainText(new RegExp(`${waiting}\\s*Blocked`))
})
