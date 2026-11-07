import { expect, test, type Page } from "@playwright/test"

const apiUrl = "http://127.0.0.1:5051"
const password = "Password123!"

async function openAgileBoard(page: Page) {
  const email = `layout-${Date.now()}@coordina.test`
  const post = async (path: string, data: object, token?: string) => {
    const response = await page.request.post(`${apiUrl}${path}`, {
      data,
      headers: token ? { Authorization: `Bearer ${token}` } : {},
    })
    expect(response.ok()).toBe(true)

    return response.json()
  }
  const { accessToken } = await post("/auth/register", {
    name: "Layout Tester",
    email,
    password,
  })
  const workspace = await post(
    "/workspaces",
    { name: "Layout team" },
    accessToken
  )
  const project = await post(
    `/workspaces/${workspace.id}/projects`,
    { name: "Layout", description: "Board layout" },
    accessToken
  )
  await post(
    `/workspaces/${workspace.id}/projects/${project.id}/boards`,
    { name: "Layout board", template: "AGILE_SCRUM" },
    accessToken
  )

  await page.goto("/login")
  await page.getByLabel("Email").fill(email)
  await page.getByLabel("Password").fill(password)
  await page
    .locator("form")
    .getByRole("button", { name: /sign in to coordina/i })
    .click()
  await page.waitForURL(/\/app/)
  await page.goto(`/app/projects/${project.id}`)
  await expect(page.getByRole("button", { name: "Insights" })).toBeVisible()
}

function pageOverflow(page: Page) {
  return page.locator("main").evaluate((main) => {
    const scroller = main.parentElement!

    return scroller.scrollWidth - scroller.clientWidth
  })
}

for (const viewport of [
  { width: 1440, height: 1000 },
  { width: 400, height: 900 },
]) {
  test(`a wide board scrolls inside its own area at ${viewport.width}px`, async ({
    page,
  }) => {
    await page.setViewportSize(viewport)
    await openAgileBoard(page)

    expect(await pageOverflow(page)).toBeLessThanOrEqual(0)
    await expect(
      page.getByRole("button", { name: "Settings" })
    ).toBeInViewport()
    await expect(
      page.getByRole("button", { name: "Insights" })
    ).toBeInViewport()

    const lists = page
      .getByRole("button", { name: "Add another list" })
      .locator("xpath=..")

    expect(
      await lists.evaluate((area) => area.scrollWidth - area.clientWidth)
    ).toBeGreaterThan(0)

    await page.getByRole("button", { name: "Insights" }).click()

    expect(await pageOverflow(page)).toBeLessThanOrEqual(0)
    await expect(
      page.getByRole("button", { name: "Settings" })
    ).toBeInViewport()
  })
}
