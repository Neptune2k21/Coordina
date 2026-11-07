import { expect, test } from "@playwright/test"

import { loadBoard, openSeededProject, seedBoard } from "./support/seeded-board"

test("dragging a card to another list moves it", async ({ page }) => {
  const seeded = await seedBoard(page, "BASIC")
  const card = seeded.board.lists[0].cards[0]
  const target = seeded.board.lists[1]

  await openSeededProject(page, seeded)

  const columns = page.locator("section.w-72")
  await expect(columns.first().getByText(card.title)).toBeVisible()

  await columns.first().getByText(card.title).dragTo(columns.nth(1))

  await expect(columns.nth(1).getByText(card.title)).toBeVisible()
  await expect(columns.first().getByText(card.title)).toHaveCount(0)

  // The board updates optimistically, so wait for the server to catch up.
  await expect
    .poll(
      async () =>
        (await loadBoard(page, seeded)).lists
          .flatMap((list) => list.cards)
          .find((item) => item.id === card.id)?.listId
    )
    .toBe(target.id)
})
