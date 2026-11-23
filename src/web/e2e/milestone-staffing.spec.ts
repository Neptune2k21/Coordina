import { expect, test, type Page } from "@playwright/test"

import type { AuthSession } from "../src/types/auth"
import type { Board } from "../src/types/task"
import {
  apiUrl,
  openSeededProject,
  seedBoard,
  type SeededBoard,
} from "./support/seeded-board"

async function post<T>(page: Page, path: string, data: object, token: string) {
  const response = await page.request.post(`${apiUrl}${path}`, {
    data,
    headers: { Authorization: `Bearer ${token}` },
  })
  expect(response.ok()).toBe(true)

  return (await response.json()) as T
}

// A second person joins the seeded workspace through an invite.
async function addMember(page: Page, seeded: SeededBoard) {
  const workspacePath = seeded.projectPath.split("/projects/")[0]
  const invite = await post<{ code: string }>(
    page,
    `${workspacePath}/invites`,
    {},
    seeded.accessToken
  )
  const response = await page.request.post(`${apiUrl}/auth/register`, {
    data: {
      name: "Grace",
      email: `e2e-grace-${Date.now()}@coordina.test`,
      password: "Password123!",
    },
  })
  expect(response.ok()).toBe(true)
  const member = (await response.json()) as AuthSession
  await post(
    page,
    "/workspaces/join",
    { inviteCode: invite.code },
    member.accessToken
  )
  await page.context().clearCookies()
}

test("applying a better plan spreads the work and brings the milestone in", async ({
  page,
}) => {
  const seeded = await seedBoard(page, "BASIC")
  await addMember(page, seeded)
  const boardPath = `${seeded.projectPath}/boards/${seeded.board.id}`
  const listId = seeded.board.lists[0].id
  const cardId = (board: Board, title: string) =>
    board.lists
      .flatMap((list) => list.cards)
      .find((card) => card.title === title)!.id

  // Nobody is assigned, so the forecast has one person do four days of work.
  let board = await post<Board>(
    page,
    `${boardPath}/lists/${listId}/cards`,
    { title: "Launch", size: "XS" },
    seeded.accessToken
  )
  const launch = cardId(board, "Launch")

  for (const title of ["Pricing page", "Signup", "Onboarding", "Invoices"]) {
    board = await post<Board>(
      page,
      `${boardPath}/lists/${listId}/cards`,
      { title, size: "M" },
      seeded.accessToken
    )
    await post(
      page,
      `${boardPath}/cards/${launch}/dependencies`,
      { dependsOnCardId: cardId(board, title) },
      seeded.accessToken
    )
  }

  const target = new Date()
  target.setUTCDate(target.getUTCDate() + 30)
  const response = await page.request.patch(
    `${apiUrl}${boardPath}/cards/${launch}`,
    {
      data: { milestoneDate: target.toISOString().slice(0, 10) },
      headers: { Authorization: `Bearer ${seeded.accessToken}` },
    }
  )
  expect(response.ok()).toBe(true)

  await openSeededProject(page, seeded)

  const milestones = page.getByRole("region", { name: "Milestones" })
  const commit = milestones.getByText(/85%:/)
  await expect(commit).toBeVisible()
  const before = await commit.textContent()
  await milestones.getByRole("button", { name: "Optimize the plan" }).click()

  const dialog = page.getByRole("dialog")
  await expect(dialog.getByText(/Ships \d+ days? sooner/)).toBeVisible()
  await expect(dialog.getByText(/^Give .+ to Grace$/)).toBeVisible()
  await dialog.getByRole("button", { name: /^Apply \d+ changes?$/ }).click()

  await expect(dialog).toBeHidden()
  await expect(commit).not.toHaveText(before ?? "")
})
