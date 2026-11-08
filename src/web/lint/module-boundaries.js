import { readdirSync } from "node:fs"
import path from "node:path"

/**
 * Which features each feature may import. A feature may only use the ones
 * listed for it, and those come before it in this table, so features never
 * form a cycle. A feature folder missing from this table may depend on
 * nothing, and nothing may depend on it, until it is added here.
 */
export const featureDependencies = {
  auth: [],
  workspaces: ["auth"],
  board: ["auth", "workspaces"],
  projects: ["auth", "workspaces", "board"],
  work: ["auth", "workspaces", "projects"],
}

const sharedFolders = ["components", "lib", "types"]
const appFiles = ["src/app/**", "src/App.tsx", "src/main.tsx"]

const parentImport = {
  regex: "^\\.\\.(/|$)",
  message: "Use '@/…' so the module you depend on is visible in the import.",
}

const appImport = {
  regex: "^@/(app(/.*)?|App|main)$",
  message:
    "Only the app layer (src/app, App.tsx, main.tsx) composes the app; features and shared code do not import it.",
}

/** The feature folders under src/features, as the linter finds them on disk. */
export function featuresIn(webRoot) {
  return readdirSync(path.join(webRoot, "src/features"), {
    withFileTypes: true,
  })
    .filter((entry) => entry.isDirectory())
    .map((entry) => entry.name)
}

function deepImport(feature) {
  return {
    regex: `^@/features/${feature}/.+`,
    message: `Import from '@/features/${feature}', the feature's public entry (src/features/${feature}/index.ts).`,
  }
}

function restrict(files, patterns) {
  return {
    files,
    rules: { "no-restricted-imports": ["error", { patterns }] },
  }
}

/**
 * ESLint configs that keep the web app layered: the app layer composes
 * features, features use each other only through their index.ts and in the
 * order of {@link featureDependencies}, and shared code never reaches into a
 * feature. Each file matches exactly one config, because a later
 * no-restricted-imports replaces an earlier one instead of adding to it.
 *
 * Dynamic import() is not checked, so a lazily loaded page can still be
 * imported from inside its feature and stay in its own chunk.
 */
export function moduleBoundaries(features) {
  const featureConfigs = features.map((feature) => {
    const allowed = featureDependencies[feature] ?? []

    return restrict(
      [`src/features/${feature}/**`],
      [
        parentImport,
        appImport,
        {
          regex: `^@/features/${feature}$`,
          message:
            "Import the file itself; a feature's index.ts is for the code outside it.",
        },
        ...features
          .filter((other) => other !== feature && !allowed.includes(other))
          .map((other) => ({
            regex: `^@/features/${other}(/.*)?$`,
            message: `'${feature}' may not depend on '${other}'. Features depend only on the ones listed for them in lint/module-boundaries.js.`,
          })),
        ...allowed.map(deepImport),
      ]
    )
  })

  return [
    ...featureConfigs,
    restrict(
      sharedFolders.map((folder) => `src/${folder}/**`),
      [
        parentImport,
        appImport,
        {
          regex: "^@/features(/.*)?$",
          message:
            "Shared code is used by features, so it cannot depend on one. Move the code into the feature, or the feature's part into shared code.",
        },
      ]
    ),
    restrict(appFiles, [parentImport, ...features.map(deepImport)]),
  ]
}
