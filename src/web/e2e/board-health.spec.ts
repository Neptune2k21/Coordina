import { expect, test } from "@playwright/test"

const apiUrl = "http://127.0.0.1:5051"
const password = "Password123!"

type SeededCard = {
  isCompleted: boolean
  dependencies: Array<{ isCompleted: boolean }>
}

test("board health agrees with the planning panel on blocked work", async ({
  page,
}) => {
  const email = `health-${Date.now()}@coordina.test`
  const post = async (path: string, data: object, token?: string) => {
    const response = await page.request.post(`${apiUrl}${path}`, {
      data,
      headers: token ? { Authorization: `Bearer ${token}` } : {},
    })
    expect(response.ok()).toBe(true)

    return response.json()
  }
  const { accessToken } = await post("/auth/register", {
    name: "Health Tester",
    email,
    password,
  })
  const workspace = await post("/workspaces", { name: "Health" }, accessToken)
  const project = await post(
    `/workspaces/${workspace.id}/projects`,
    { name: "Health", description: "Board health" },
    accessToken
  )
  const board = await post(
    `/workspaces/${workspace.id}/projects/${project.id}/boards`,
    { name: "Health board", template: "BASIC" },
    accessToken
  )
  const cards: SeededCard[] = board.lists.flatMap(
    (list: { cards: SeededCard[] }) => list.cards
  )
  const waiting = cards.filter(
    (card) =>
      !card.isCompleted &&
      card.dependencies.some((dependency) => !dependency.isCompleted)
  ).length

  expect(waiting).toBeGreaterThan(0)

  await page.goto("/login")
  await page.getByLabel("Email").fill(email)
  await page.getByLabel("Password").fill(password)
  await page
    .locator("form")
    .getByRole("button", { name: /sign in to coordina/i })
    .click()
  await page.waitForURL(/\/app/)
  await page.goto(`/app/projects/${project.id}`)

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
