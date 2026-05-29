import { expect, test } from "@playwright/test"

test("user can register, sign out and sign in", async ({ page }) => {
  const email = `ada-${Date.now()}@coordina.test`

  await page.goto("/login")
  await page.getByRole("button", { name: "Create an account" }).click()

  await page.getByLabel("Full name").fill("Ada Lovelace")
  await page.getByLabel("Email").fill(email)
  await page.getByLabel("Password").fill("Password123!")
  await page.getByRole("button", { name: /create workspace account/i }).click()

  await expect(page.getByText("Welcome back, Ada Lovelace")).toBeVisible()
  await expect(page.getByText(email)).toBeVisible()

  await page.getByRole("button", { name: /sign out/i }).click()
  await page.getByRole("button", { name: "Sign in" }).click()
  await page.getByLabel("Email").fill(email)
  await page.getByLabel("Password").fill("Password123!")
  await page
    .locator("form")
    .getByRole("button", { name: /sign in to coordina/i })
    .click()

  await expect(page.getByText("Welcome back, Ada Lovelace")).toBeVisible()
})
