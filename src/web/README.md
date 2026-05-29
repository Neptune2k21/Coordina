# Coordina Web

React application for Coordina.

## Stack

| Area | Technology |
| --- | --- |
| Runtime | React 19 |
| Language | TypeScript |
| Build | Vite |
| Styling | Tailwind CSS 4 |
| UI primitives | shadcn-style components, Radix UI |
| Tests | Vitest, Testing Library, Playwright |

## Commands

Run all commands from the repository root:

```bash
pnpm --dir src/web install
pnpm --dir src/web dev
pnpm --dir src/web test
pnpm --dir src/web typecheck
pnpm --dir src/web lint
pnpm --dir src/web build
```

Or use the root Makefile:

```bash
make web
make web-test
make web-typecheck
make web-build
```

## Environment

The web app reads the API base URL from:

```dotenv
VITE_API_URL=http://localhost:5050
```

If the variable is not set, the app defaults to `http://localhost:5050`.

## Structure

```text
src
├── components       # Shared layout, docs, and UI primitives
├── features
│   ├── auth         # Auth session, API client, forms
│   ├── docs         # In-app documentation page
│   └── workspaces   # Workspace state, API client, screens
├── lib              # Shared utilities
└── main.tsx         # App entrypoint
```

The root [README.md](../../README.md) contains the full project setup and quality gate documentation.
