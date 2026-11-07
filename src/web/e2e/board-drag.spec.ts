import { expect, test } from "@playwright/test"

const apiUrl = "http://127.0.0.1:5051"
const password = "Password123!"

type SeededBoard = {
  lists: Array<{
    id: string
    title: string
    cards: Array<{ id: string; title: string; listId: string }>
  }>
}

test("dragging a card to another list moves it", async ({ page }) => {
  const email = `drag-${Date.now()}@coordina.test`
  const post = async (path: string, data: object, token?: string) => {
    const response = await page.request.post(`${apiUrl}${path}`, {
      data,
      headers: token ? { Authorization: `Bearer ${token}` } : {},
    })
    expect(response.ok()).toBe(true)

    return response.json()
  }
  const { accessToken } = await post("/auth/register", {
    name: "Drag Tester",
    email,
    password,
  })
  const workspace = await post("/workspaces", { name: "Drag" }, accessToken)
  const project = await post(
    `/workspaces/${workspace.id}/projects`,
    { name: "Drag", description: "Drag and drop" },
    accessToken
  )
  const projectPath = `/workspaces/${workspace.id}/projects/${project.id}`
  const seeded: SeededBoard = await post(
    `${projectPath}/boards`,
    { name: "Drag board", template: "BASIC" },
    accessToken
  )
  const card = seeded.lists[0].cards[0]
  const target = seeded.lists[1]

  await page.goto("/login")
  await page.getByLabel("Email").fill(email)
  await page.getByLabel("Password").fill(password)
  await page
    .locator("form")
    .getByRole("button", { name: /sign in to coordina/i })
    .click()
  await page.waitForURL(/\/app/)
  await page.goto(`/app/projects/${project.id}`)

  const columns = page.locator("section.w-72")
  await expect(columns.first().getByText(card.title)).toBeVisible()

  await columns.first().getByText(card.title).dragTo(columns.nth(1))

  await expect(columns.nth(1).getByText(card.title)).toBeVisible()
  await expect(columns.first().getByText(card.title)).toHaveCount(0)

  // The board updates optimistically, so wait for the server to catch up.
  await expect
    .poll(async () => {
      const response = await page.request.get(`${apiUrl}${projectPath}/board`, {
        headers: { Authorization: `Bearer ${accessToken}` },
      })
      const board: SeededBoard = await response.json()

      return board.lists
        .flatMap((list) => list.cards)
        .find((item) => item.id === card.id)?.listId
    })
    .toBe(target.id)
})
