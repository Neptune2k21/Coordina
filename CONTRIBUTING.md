# Contributing to Coordina

Thank you for taking the time to improve Coordina. The project is currently a pre-MVP foundation, so contributions should strengthen the platform shape instead of adding disconnected product fragments.

## Development Principles

Coordina favors:

| Principle | Expectation |
| --- | --- |
| Clear module boundaries | Keep backend changes inside the owning feature module unless shared infrastructure is truly required |
| Server-side authorization | Never rely on frontend state for access control |
| Small pull requests | Prefer focused changes with clear behavior and tests |
| Typed contracts | Keep API contracts explicit and frontend clients typed |
| Repeatable quality | The full quality gate should pass locally before review |
| Product restraint | Prefer foundations that make the first real MVP easier to build |

## Local Setup

Install dependencies:

```bash
pnpm --dir src/web install
dotnet restore Coordina.sln -m:1
dotnet tool restore
```

Create your environment file:

```bash
cp .env.example .env
```

Start local infrastructure:

```bash
make docker-up
```

Apply migrations:

```bash
make api-migrate
```

Run the app:

```bash
make dev
```

## Branching

Use short, descriptive branch names:

```text
feature/workspace-projects
fix/invite-consumption-race
docs/security-policy
```

## Backend Guidelines

The API follows a feature-module layout:

```text
src/api/modules/<feature>
├── Application
├── Contracts
├── Domain
├── Infrastructure
└── <Feature>Endpoints.cs
```

When adding backend behavior:

1. Put business rules in `Application`.
2. Keep HTTP request and response shapes in `Contracts`.
3. Keep EF entities and database queries in `Infrastructure`.
4. Return explicit result statuses from services instead of throwing for normal user-facing outcomes.
5. Add integration tests for endpoint behavior.
6. Add migrations when persistence changes.

Authorization rules must be enforced by the API. Frontend checks may improve UX, but they are not security boundaries.

For now, avoid adding public API documentation sections to the README for every endpoint. Endpoint details should live in OpenAPI/Scalar and tests; the root README should explain the project, architecture, and current state.

## Frontend Guidelines

The web app is organized by feature:

```text
src/web/src/features/<feature>
```

When adding frontend behavior:

1. Keep API calls in a feature API client.
2. Keep reusable state in a context only when multiple screens need it.
3. Prefer existing UI primitives from `src/components/ui`.
4. Keep form validation explicit and user-facing errors actionable.
5. Add Vitest tests for component or state logic when behavior can regress.
6. Add Playwright coverage for important user flows.

## Database Changes

Create a migration with:

```bash
make api-migration name=DescriptiveMigrationName
```

Apply migrations locally:

```bash
make api-migrate
```

Before submitting a database change, verify:

| Check | Reason |
| --- | --- |
| The migration has a clear name | Keeps schema history readable |
| Delete behavior is intentional | Prevents unexpected data loss |
| Indexes match query patterns | Keeps workspace-scoped queries efficient |
| Constraints enforce important invariants | Avoids depending only on application code |

## Testing

Run focused tests while developing:

```bash
make api-test
make web-test
make web-typecheck
```

Run the full quality gate before opening a pull request:

```bash
make quality
```

If Playwright has not installed Chromium yet:

```bash
pnpm --dir src/web exec playwright install --with-deps chromium
```

## Formatting

Use the project formatters:

```bash
make format
```

Format checks are enforced in CI. Do not hand-format large generated blocks unless the formatter cannot handle them.

## Pull Request Checklist

Before requesting review, confirm:

```text
[ ] The change is focused and explained clearly.
[ ] The change fits the current pre-MVP direction.
[ ] Backend behavior has API or unit tests where appropriate.
[ ] Frontend behavior has component, state, or e2e tests where appropriate.
[ ] Database changes include an EF migration.
[ ] Security-sensitive behavior is enforced server-side.
[ ] make quality passes locally.
[ ] Documentation was updated if commands, contracts, or setup changed.
```

## Commit Style

Use clear imperative commit messages:

```text
Add workspace invite consumption tests
Fix owner-only member removal response
Document local quality gate
```

Avoid vague messages such as `update`, `fix stuff`, or `changes`.

## Reviewing

Reviews should focus on correctness first:

| Priority | Look for |
| --- | --- |
| Behavior | Does the change do what it claims? |
| Security | Are access rules enforced by the API? |
| Tests | Would the tests fail if the behavior regressed? |
| Maintainability | Does the change fit the existing module shape? |
| Product feel | Is the user experience clear and consistent? |

Keep feedback specific and actionable.
