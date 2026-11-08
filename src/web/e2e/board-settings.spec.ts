import { expect, test } from "@playwright/test"

import { loadBoard, openSeededProject, seedBoard } from "./support/seeded-board"

test("a list renamed and moved in Settings shows on the board", async ({
  page,
}) => {
  const seeded = await seedBoard(page, "BASIC")
  const [first] = seeded.board.lists

  await openSeededProject(page, seeded)
  await expect(page.getByRole("button", { name: "Docs" })).toHaveCount(0)

  await page.getByRole("button", { name: "Settings" }).click()
  const title = page.getByLabel(`Title of ${first.title}`)
  await title.fill("Backlog")
  await title.press("Enter")
  await expect(page.getByLabel("Status of Backlog")).toBeVisible()
  await page.getByRole("button", { name: "Move Backlog down" }).click()
  await expect(
    page.getByRole("button", { name: "Move Backlog up" })
  ).toBeEnabled()

  await page.getByRole("button", { name: "Board" }).click()
  const columns = page.locator("section.w-72")
  await expect(columns.nth(1).getByLabel("List title")).toHaveValue("Backlog")

  await expect
    .poll(async () =>
      (await loadBoard(page, seeded)).lists
        .sort((a, b) => a.position - b.position)
        .map((list) => list.title)
    )
    .toEqual([
      seeded.board.lists[1].title,
      "Backlog",
      seeded.board.lists[2].title,
    ])
})
