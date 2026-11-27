import { expect, test, type Page } from "@playwright/test"

import type { Board } from "../src/types/task"
import {
  apiUrl,
  openSeededProject,
  seedBoard,
  type SeededBoard,
} from "./support/seeded-board"

// The day the given working day from today ends, today counting when it is
// one, as the API counts them (UTC).
function workingDaysAway(count: number) {
  const day = new Date()
  let left = count

  for (;;) {
    const weekday = day.getUTCDay()

    if (weekday !== 0 && weekday !== 6 && --left === 0) {
      return day.toISOString().slice(0, 10)
    }

    day.setUTCDate(day.getUTCDate() + 1)
  }
}

async function send(
  page: Page,
  seeded: SeededBoard,
  method: "post" | "patch",
  path: string,
  data: object
) {
  const response = await page.request[method](
    `${apiUrl}${seeded.projectPath}/boards/${seeded.board.id}${path}`,
    { data, headers: { Authorization: `Bearer ${seeded.accessToken}` } }
  )
  expect(response.ok()).toBe(true)

  return (await response.json()) as Board
}

test("cutting what a proposal suggests brings a late milestone on track", async ({
  page,
}) => {
  const seeded = await seedBoard(page, "BASIC")
  const listId = seeded.board.lists[0].id
  const ids = new Map<string, string>()

  // One person, six working days (36 focus hours): all of it takes about 39
  // hours, so the launch is late, and about 19 without the PDF export.
  for (const card of [
    { title: "PDF templates", size: "S" },
    { title: "Export PDF", size: "L", priority: "LOW" },
    { title: "SSO", size: "L", priority: "HIGH" },
    { title: "Launch", size: "XS" },
  ]) {
    const board = await send(
      page,
      seeded,
      "post",
      `/lists/${listId}/cards`,
      card
    )
    const created = board.lists
      .flatMap((list) => list.cards)
      .find((candidate) => candidate.title === card.title)
    ids.set(card.title, created!.id)
  }

  const id = (title: string) => ids.get(title)!
  for (const [card, dependency] of [
    ["Export PDF", "PDF templates"],
    ["Launch", "Export PDF"],
    ["Launch", "SSO"],
  ]) {
    await send(page, seeded, "post", `/cards/${id(card)}/dependencies`, {
      dependsOnCardId: id(dependency),
    })
  }
  await send(page, seeded, "patch", `/cards/${id("Launch")}`, {
    milestoneDate: workingDaysAway(6),
  })

  await openSeededProject(page, seeded)

  const milestones = page.getByRole("region", { name: "Milestones" })
  await expect(milestones.getByText("Late", { exact: true })).toBeVisible()
  await milestones.getByRole("button", { name: "Propose a scope" }).click()

  const dialog = page.getByRole("dialog")
  await expect(dialog.getByText("Export PDF")).toBeVisible()
  await expect(dialog.getByText("Also removes PDF templates")).toBeVisible()
  await dialog.getByRole("button", { name: "Take out 1 card" }).click()

  await expect(dialog).toBeHidden()
  await expect(milestones.getByText("On track", { exact: true })).toBeVisible()
})
