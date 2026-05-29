<div align="center">
  <br />
  <h1>Coordina</h1>
  <p>
    A workspace-first collaboration platform foundation built with .NET, React, PostgreSQL, and a strict quality gate.
  </p>
  <p>
    <a href="https://github.com/Neptune2k21/Coordina/actions/workflows/quality.yml">
      <img src="https://github.com/Neptune2k21/Coordina/actions/workflows/quality.yml/badge.svg?branch=master" alt="Quality workflow status" />
    </a>
    <img src="https://img.shields.io/badge/status-pre--MVP-111827?style=flat-square" alt="Project status: pre-MVP" />
    <img src="https://img.shields.io/badge/license-MIT-111827?style=flat-square" alt="MIT License" />
  </p>
  <br />
  <p>
    <img height="42" src="https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/dotnetcore/dotnetcore-original.svg" alt=".NET" />
    &nbsp;&nbsp;
    <img height="42" src="https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/csharp/csharp-original.svg" alt="C#" />
    &nbsp;&nbsp;
    <img height="42" src="https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/postgresql/postgresql-original.svg" alt="PostgreSQL" />
    &nbsp;&nbsp;
    <img height="42" src="https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/react/react-original.svg" alt="React" />
    &nbsp;&nbsp;
    <img height="42" src="https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/typescript/typescript-original.svg" alt="TypeScript" />
    &nbsp;&nbsp;
    <img height="42" src="https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/vitejs/vitejs-original.svg" alt="Vite" />
    &nbsp;&nbsp;
    <img height="42" src="https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/tailwindcss/tailwindcss-original.svg" alt="Tailwind CSS" />
    &nbsp;&nbsp;
    <img height="42" src="https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/docker/docker-original.svg" alt="Docker" />
  </p>
  <br />
</div>

## What This Is

Coordina is not an MVP yet. It is the technical base of a future collaboration product: authentication, workspace tenancy, invitation-based membership, a React application shell, persistence, tests, CI, and documentation.

The current goal is to make the project easy to evolve without turning the first real product features into a rewrite. The backend is organized by feature module, the frontend is organized by product area, and the quality gate is strict enough to catch formatting, typing, tests, builds, and end-to-end regressions before code lands.

## Project State

| Area | Current state |
| --- | --- |
| Product maturity | Pre-MVP foundation |
| Primary domain concept | Workspace as tenant boundary |
| Backend shape | Modular ASP.NET Core API |
| Frontend shape | React SaaS shell with auth and workspace onboarding |
| Persistence | PostgreSQL with EF Core migrations |
| CI | GitHub Actions quality workflow |
| Tests | API integration tests, frontend tests, Playwright e2e |
| Deployment | Not production-hardened yet |

## Foundation Capabilities

| Capability | Why it exists |
| --- | --- |
| Account registration and sign-in | Gives the platform an authenticated user boundary |
| JWT bearer authentication | Provides the first secure API access layer |
| Workspace creation | Establishes the future tenant model |
| Workspace membership roles | Separates owner-level administration from member access |
| One-time invitation codes | Enables controlled workspace onboarding |
| Server-side authorization checks | Keeps workspace access rules in the API, not the browser |
| React workspace shell | Provides the first authenticated product surface |
| OpenAPI and Scalar | Keeps the API inspectable during development |
| Local Docker infrastructure | Makes database-backed development repeatable |
| Full quality gate | Keeps the foundation clean while the product grows |

## Tech Stack

| Layer | Technologies |
| --- | --- |
| API | .NET 9, ASP.NET Core Minimal APIs, C# |
| Persistence | PostgreSQL 16, Entity Framework Core, Npgsql |
| Authentication | JWT bearer tokens, PBKDF2 password hashing |
| Web | React 19, TypeScript, Vite |
| Styling | Tailwind CSS 4, shadcn-style primitives, Radix UI |
| Testing | xUnit, WebApplicationFactory, Vitest, Testing Library, Playwright |
| Tooling | pnpm, Make, Docker Compose, GitHub Actions |

## Architecture

The API follows a feature-module structure:

```text
src/api/modules/<feature>
├── Application      # Use cases, ports, result models
├── Contracts        # HTTP request and response contracts
├── Domain           # Domain records and enums
├── Infrastructure   # EF entities, configurations, stores
└── <Feature>Endpoints.cs
```

Current modules:

| Module | Responsibility |
| --- | --- |
| `auth` | Account registration, login, password hashing, JWT issuing |
| `workspaces` | Workspace lifecycle, invitations, membership, authorization rules |
| `health` | Runtime health endpoint |

The web application mirrors this direction with feature folders:

```text
src/web/src/features
├── auth          # Session state, auth API client, auth forms
├── docs          # In-app technical documentation
└── workspaces    # Workspace state, onboarding, shell panels
```

## Repository Map

```text
.
├── src
│   ├── api                  # ASP.NET Core API
│   └── web                  # React application
├── tests
│   └── api                  # API unit and integration tests
├── docker                   # PostgreSQL and Adminer compose services
├── .github/workflows        # CI quality workflow
├── Makefile                 # Local workflow entrypoint
├── CONTRIBUTING.md          # Contribution workflow
├── SECURITY.md              # Security policy
└── LICENSE                  # MIT license
```

## Getting Started

Requirements:

| Tool | Version |
| --- | --- |
| .NET SDK | 9.x |
| Node.js | 24.x recommended |
| pnpm | 10.17.x recommended |
| Docker | Current stable |

Create local configuration:

```bash
cp .env.example .env
```

Install frontend dependencies:

```bash
pnpm --dir src/web install
```

Start PostgreSQL and Adminer:

```bash
make docker-up
```

Apply migrations:

```bash
make api-migrate
```

Run the full development app:

```bash
make dev
```

Local services:

| Service | URL |
| --- | --- |
| Web app | http://localhost:5173 |
| API | http://localhost:5050 |
| API reference | http://localhost:5050/api-docs |
| OpenAPI schema | http://localhost:5050/openapi/v1.json |
| Adminer | http://localhost:8080 |

## Environment

The local `.env` file follows this shape:

```dotenv
POSTGRES_USER=coordina
POSTGRES_PASSWORD=change-me
POSTGRES_DB=coordina
POSTGRES_HOST=localhost
POSTGRES_PORT=5432
ADMINER_PORT=8080
ASPNETCORE_ENVIRONMENT=Development

Auth__Issuer=Coordina.Api
Auth__Audience=Coordina.Web
Auth__SigningKey=change-me-to-a-secure-random-secret-with-at-least-32-characters
Auth__AccessTokenMinutes=60
```

The checked-in example is for local development only. Use a unique signing key and managed secrets outside local environments.

## Workflow

The Makefile is the main local interface.

| Command | Purpose |
| --- | --- |
| `make dev` | Run API watcher and web dev server together |
| `make api` | Apply migrations and run the API |
| `make web` | Run the Vite dev server |
| `make api-test` | Run .NET tests |
| `make web-test` | Run Vitest tests |
| `make web-e2e` | Run Playwright tests |
| `make lint` | Run format verification, lint, and typecheck |
| `make test` | Run API, web, and e2e tests |
| `make format` | Format API and web sources |
| `make quality` | Run the same full gate expected by CI |

## Quality Gate

The CI workflow runs on pushes and pull requests targeting `master` and `develop`.

```bash
make quality
```

The gate verifies:

| Check | Tooling |
| --- | --- |
| API formatting | `dotnet format --verify-no-changes` |
| Web formatting | Prettier |
| Web lint | ESLint |
| Web type safety | TypeScript |
| API tests | xUnit |
| Web tests | Vitest |
| End-to-end flow | Playwright Chromium |
| API build | `dotnet build` |
| Web build | Vite production build |

The current e2e flow covers account creation, workspace onboarding, workspace creation, sign-out, sign-in, and return to the existing workspace.

## Security Direction

Coordina treats workspaces as the tenant boundary. The API owns the access rules.

Current rules include:

| Rule | Behavior |
| --- | --- |
| Workspace reads | Scoped to the authenticated user's memberships |
| Non-member access | Hidden behind `404` responses |
| Owner actions | Required for invites, member removal, and workspace deletion |
| Invite codes | One-time use, expiration-aware, stored hashed |
| Frontend state | Useful for UX, never trusted for authorization |

For vulnerability reporting and current security limits, read [SECURITY.md](SECURITY.md).

## Roadmap Direction

The next meaningful layers are product layers, not more scaffolding:

| Track | Intent |
| --- | --- |
| Projects | First real workspace-owned business object |
| Tasks or boards | Core collaboration workflow |
| Realtime updates | Shared workspace presence and live state |
| Audit trail | Owner visibility into membership and sensitive actions |
| Production hardening | Rate limiting, refresh tokens, secret management, deploy docs |

## Documentation

| File | Purpose |
| --- | --- |
| [CONTRIBUTING.md](CONTRIBUTING.md) | Local workflow, conventions, pull request expectations |
| [SECURITY.md](SECURITY.md) | Security policy, reporting, current guarantees and gaps |
| [src/web/README.md](src/web/README.md) | Web-specific setup and structure |

Runtime documentation is also available at `/docs` in the web app and `/api-docs` on the API when services are running.

## License

Coordina is released under the [MIT License](LICENSE).
