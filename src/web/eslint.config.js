import js from "@eslint/js"
import globals from "globals"
import reactHooks from "eslint-plugin-react-hooks"
import reactRefresh from "eslint-plugin-react-refresh"
import tseslint from "typescript-eslint"
import { defineConfig, globalIgnores } from "eslint/config"

export default defineConfig([
  // api-contract.ts is generated from contracts/openapi.json.
  globalIgnores(["dist", "src/types/api-contract.ts"]),
  {
    files: ["**/*.{ts,tsx}"],
    extends: [
      js.configs.recommended,
      // Type-aware rules catch what syntax alone cannot, such as a promise
      // passed where a callback is expected and never awaited.
      tseslint.configs.recommendedTypeChecked,
      tseslint.configs.stylisticTypeChecked,
      reactHooks.configs.flat.recommended,
      reactRefresh.configs.vite,
    ],
    languageOptions: {
      globals: globals.browser,
      parserOptions: {
        projectService: true,
        tsconfigRootDir: import.meta.dirname,
      },
    },
    rules: {
      eqeqeq: "error",
      "no-console": "error",
      "@typescript-eslint/consistent-type-definitions": ["error", "type"],
      // Array<T> stays readable for object types; simple ones use T[].
      "@typescript-eslint/array-type": ["error", { default: "array-simple" }],
      // `value || fallback` on a string also replaces "", which is often the point.
      "@typescript-eslint/prefer-nullish-coalescing": [
        "error",
        { ignorePrimitives: { string: true } },
      ],
      "@typescript-eslint/consistent-type-imports": "error",
      "@typescript-eslint/naming-convention": [
        "error",
        {
          selector: "default",
          format: ["camelCase"],
          leadingUnderscore: "allow",
        },
        // React components and module-level constants.
        {
          selector: "variable",
          format: ["camelCase", "PascalCase", "UPPER_CASE"],
        },
        { selector: "function", format: ["camelCase", "PascalCase"] },
        // Components passed as props, like `icon: Icon`.
        {
          selector: "parameter",
          format: ["camelCase", "PascalCase"],
          leadingUnderscore: "allow",
        },
        { selector: "typeLike", format: ["PascalCase"] },
        { selector: "import", format: null },
        // Keys follow the API and HTTP headers, not our naming.
        { selector: ["objectLiteralProperty", "typeProperty"], format: null },
      ],
    },
  },
  {
    // Augmenting a global type such as ImportMetaEnv needs an interface.
    files: ["**/*.d.ts"],
    rules: { "@typescript-eslint/consistent-type-definitions": "off" },
  },
  {
    files: ["eslint.config.js"],
    extends: [tseslint.configs.disableTypeChecked],
  },
])
