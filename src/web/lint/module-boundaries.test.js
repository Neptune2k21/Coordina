// @vitest-environment node
import { Linter } from "eslint"
import tseslint from "typescript-eslint"
import { describe, expect, it } from "vitest"

import {
  featureDependencies,
  featuresIn,
  moduleBoundaries,
} from "./module-boundaries.js"

// "reports" stands for a feature folder someone added without declaring it.
const features = [...Object.keys(featureDependencies), "reports"]

const config = [
  {
    files: ["**/*.{ts,tsx}"],
    languageOptions: { parser: tseslint.parser },
  },
  ...moduleBoundaries(features),
]

function problems(file, source) {
  return new Linter()
    .verify(source, config, { filename: file })
    .map((message) => message.message)
}

describe("module boundaries", () => {
  it.each([
    [
      "src/features/board/hooks/use-x.ts",
      `import { useAuth } from "@/features/auth"`,
    ],
    [
      "src/features/board/hooks/use-x.ts",
      `import { x } from "@/features/board/model/x"`,
    ],
    [
      "src/features/projects/x.ts",
      `import { ProjectBoard } from "@/features/board"`,
    ],
    [
      "src/app/workspace-app.tsx",
      `import { ProjectsPage } from "@/features/projects"`,
    ],
    ["src/components/ui/button.tsx", `import { cn } from "@/lib/utils"`],
    ["src/features/board/x.ts", `import { y } from "./y"`],
  ])("lets %s use %s", (file, source) => {
    expect(problems(file, source)).toEqual([])
  })

  it.each([
    [
      "src/features/auth/x.ts",
      `import { useWorkspaces } from "@/features/workspaces"`,
      "'auth' may not depend on 'workspaces'",
    ],
    [
      "src/features/workspaces/x.ts",
      `import type { ProjectShell } from "@/features/projects"`,
      "'workspaces' may not depend on 'projects'",
    ],
    [
      "src/features/board/x.ts",
      `import { listWorkspaceMembers } from "@/features/workspaces/workspace-api"`,
      "Import from '@/features/workspaces'",
    ],
    [
      "src/features/board/x.ts",
      `export { useAuth } from "@/features/auth/auth-context"`,
      "Import from '@/features/auth'",
    ],
    [
      "src/features/board/x.ts",
      `import { ProjectBoard } from "@/features/board"`,
      "Import the file itself",
    ],
    [
      "src/features/board/x.ts",
      `import { reportOf } from "@/features/reports"`,
      "'board' may not depend on 'reports'",
    ],
    [
      "src/features/reports/x.ts",
      `import { useAuth } from "@/features/auth"`,
      "'reports' may not depend on 'auth'",
    ],
    [
      "src/features/projects/x.ts",
      `import { SaasShell } from "@/app/saas-shell"`,
      "Only the app layer",
    ],
    [
      "src/lib/x.ts",
      `import { useAuth } from "@/features/auth"`,
      "Shared code is used by features",
    ],
    [
      "src/components/layout/x.tsx",
      `import { App } from "@/App"`,
      "Only the app layer",
    ],
    [
      "src/app/x.tsx",
      `import { WorkspaceSwitcher } from "@/features/workspaces/components/workspace-switcher"`,
      "Import from '@/features/workspaces'",
    ],
    [
      "src/features/board/hooks/x.ts",
      `import { y } from "../model/y"`,
      "Use '@/…'",
    ],
  ])("stops %s from using %s", (file, source, expected) => {
    const found = problems(file, source)

    expect(found).toHaveLength(1)
    expect(found[0]).toContain(expected)
  })

  it("does not check lazily loaded pages", () => {
    expect(
      problems(
        "src/App.tsx",
        `const page = import("@/features/auth/components/auth-section")`
      )
    ).toEqual([])
  })

  it("declares every feature folder", () => {
    expect(Object.keys(featureDependencies).sort()).toEqual(
      featuresIn(`${import.meta.dirname}/..`).sort()
    )
  })

  it("only lets a feature depend on declared features listed before it", () => {
    const seen = new Set()

    for (const [feature, dependencies] of Object.entries(featureDependencies)) {
      expect(
        dependencies.filter((dependency) => !seen.has(dependency))
      ).toEqual([])
      seen.add(feature)
    }
  })
})
