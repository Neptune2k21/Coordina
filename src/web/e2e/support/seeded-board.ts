import { expect, type Page } from "@playwright/test"

import type { Board, BoardTemplate } from "../../src/types/task"

export const apiUrl = "http://127.0.0.1:5051"
const password = "Password123!"

export type SeededBoard = {
  accessToken: string
  board: Board
  email: string
  projectId: string
  projectPath: string
}

/** Registers a user and creates a workspace, a project and its board through the API. */
export async function seedBoard(
  page: Page,
  template: BoardTemplate
): Promise<SeededBoard> {
  const email = `e2e-${Date.now()}-${Math.random().toString(36).slice(2)}@coordina.test`
  const post = async (path: string, data: object, token?: string) => {
    const response = await page.request.post(`${apiUrl}${path}`, {
      data,
      headers: token ? { Authorization: `Bearer ${token}` } : {},
    })
    expect(response.ok()).toBe(true)

    return response.json()
  }
  const { accessToken } = await post("/auth/register", {
    name: "E2E Tester",
    email,
    password,
  })
  const workspace = await post("/workspaces", { name: "E2E team" }, accessToken)
  const project = await post(
    `/workspaces/${workspace.id}/projects`,
    { name: "E2E", description: "End-to-end test project" },
    accessToken
  )
  const projectPath = `/workspaces/${workspace.id}/projects/${project.id}`
  const board: Board = await post(
    `${projectPath}/boards`,
    { name: "E2E board", template },
    accessToken
  )

  return { accessToken, board, email, projectId: project.id, projectPath }
}

/** Signs in through the form, like a user would, and opens the seeded project. */
export async function openSeededProject(page: Page, seeded: SeededBoard) {
  await page.goto("/login")
  await page.getByLabel("Email").fill(seeded.email)
  await page.getByLabel("Password").fill(password)
  await page
    .locator("form")
    .getByRole("button", { name: /sign in to coordina/i })
    .click()
  await page.waitForURL(/\/app/)
  await page.goto(`/app/projects/${seeded.projectId}`)
}

/** Reads the board as the API sees it now. */
export async function loadBoard(page: Page, seeded: SeededBoard) {
  const response = await page.request.get(
    `${apiUrl}${seeded.projectPath}/board`,
    { headers: { Authorization: `Bearer ${seeded.accessToken}` } }
  )

  return (await response.json()) as Board
}
