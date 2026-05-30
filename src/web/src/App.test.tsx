import { render, screen } from "@testing-library/react"
import { describe, expect, it } from "vitest"

import App from "./App"

describe("App", () => {
  it("renders the landing page by default", async () => {
    render(<App />)

    expect(
      await screen.findByRole("link", { name: "Coordina" })
    ).toBeInTheDocument()
    expect(
      screen.getByRole("heading", { name: "Coordina" })
    ).toBeInTheDocument()
  })
})
