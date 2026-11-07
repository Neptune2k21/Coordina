import { expect, test, type Page } from "@playwright/test"

import { openSeededProject, seedBoard } from "./support/seeded-board"

async function openAgileBoard(page: Page) {
  await openSeededProject(page, await seedBoard(page, "AGILE_SCRUM"))
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
