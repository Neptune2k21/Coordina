import {
  BracketsCurly,
  Database,
  FileText,
  Lightning,
  ShieldCheck,
  StackSimple,
} from "@phosphor-icons/react"

const apiBaseUrl = import.meta.env.VITE_API_URL ?? "http://localhost:5050"

export const docsApiBaseUrl = apiBaseUrl

export const docsNavItems = [
  { id: "quickstart", label: "Quickstart" },
  { id: "auth", label: "Authentication" },
  { id: "workspaces-api", label: "Workspace API" },
  { id: "workspaces-security", label: "Security model" },
  { id: "projects-api", label: "Projects API" },
  { id: "workspaces-ux", label: "Workspace UX" },
  { id: "workspaces-ui", label: "shadcn usage" },
  { id: "tests", label: "Testing" },
  { id: "environment", label: "Environment" },
  { id: "api", label: "API testing" },
] as const

export const installCode = `make docker-up
make api
make web`

export const authCode = `const response = await fetch("${apiBaseUrl}/auth/login", {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({
    email: "you@company.com",
    password: "Password123!"
  })
})

const session = await response.json()
localStorage.setItem("coordina.auth.session", JSON.stringify(session))`

export const envCode = `POSTGRES_USER=coordina
POSTGRES_PASSWORD=change-me
POSTGRES_DB=coordina
POSTGRES_HOST=localhost
POSTGRES_PORT=5432

Auth__Issuer=Coordina.Api
Auth__Audience=Coordina.Web
Auth__SigningKey=change-me-to-a-secure-random-secret-with-at-least-32-characters
Auth__AccessTokenMinutes=60`

export const curlCode = `curl -X POST ${apiBaseUrl}/auth/register \\
  -H "Content-Type: application/json" \\
  -d '{
    "name": "Maya Chen",
    "email": "maya@coordina.test",
    "password": "Password123!"
  }'`

export const workspaceApiCode = `GET    /workspaces
POST   /workspaces              { "name": "Product Team" }
GET    /workspaces/{workspaceId}
POST   /workspaces/join         { "inviteCode": "A1B2C3D4E5F6" }
POST   /workspaces/{workspaceId}/invites
GET    /workspaces/{workspaceId}/members
DELETE /workspaces/{workspaceId}/members/{memberUserId}
DELETE /workspaces/{workspaceId}

All workspace endpoints require:
Authorization: Bearer <accessToken>`

export const workspaceSecurityCode = `Tenant isolation rules:
- list queries are filtered by current user membership
- details return 404 for non-members
- join requires a valid one-time invitation code
- create auto-creates OWNER membership
- invite code creation requires OWNER membership
- member removal requires OWNER membership
- delete requires OWNER membership
- frontend state never grants access; the API validates every request`

export const projectsApiCode = `GET    /workspaces/{workspaceId}/projects
GET    /workspaces/{workspaceId}/projects?includeArchived=true
GET    /workspaces/{workspaceId}/projects?includeCompleted=true
POST   /workspaces/{workspaceId}/projects
       { "name": "Launch plan", "description": "Optional context", "key": "APP" }
GET    /workspaces/{workspaceId}/projects/{projectId}
PATCH  /workspaces/{workspaceId}/projects/{projectId}
       { "status": "COMPLETED", "color": "teal" }
DELETE /workspaces/{workspaceId}/projects/{projectId}
DELETE /workspaces/{workspaceId}/projects/{projectId}/permanent

Project rules:
- all routes require Authorization: Bearer <accessToken>
- every route validates membership against workspaceId
- project lookup always includes both workspaceId and projectId
- default list returns ACTIVE projects only
- ARCHIVED and COMPLETED are included only by query param
- COMPLETED projects are read-only
- non-members receive 404
- edit/archive requires workspace OWNER or project owner
- permanent delete requires workspace OWNER`

export const workspaceUxCode = `Workspace UI:
- /login authenticates with the existing JWT flow
- /app loads the user's workspaces
- /app/projects loads projects for coordina.activeWorkspaceId
- project filters are backend-backed: active by default, archived/completed on demand
- no workspace: onboarding cards with create dialog and invite-code join form
- existing workspace: persistent sidebar/topbar shell
- active workspace id persists in localStorage as coordina.activeWorkspaceId
- workspace switching is instant and reloads workspace-scoped project data`

export const shadcnUsageCode = `Workspace primitives:
- Card: onboarding, workspace list, status panels
- Button: commands and icon actions
- Dialog: create workspace form
- DropdownMenu: workspace switcher and user actions
- Input: create and join forms
- Textarea: project descriptions
- Badge: project lifecycle status
- Skeleton: project loading states
- Field: labels, grouping, validation copy
- Separator: shell and panel structure`

export const testCode = `dotnet test tests/api/Coordina.Api.Tests.csproj

Manual test:
1. Register or sign in at /login
2. Open /app
3. Create a workspace and confirm OWNER role
4. Open /app/projects and create a project
5. Archive it and confirm it is hidden by default
6. Enable Archived and confirm the status badge appears
7. Mark a project Completed and confirm edits are blocked
8. Generate an invitation code, sign in as another user, redeem it once
9. Confirm only workspace owners or project owners can edit/archive
10. Confirm a non-member receives 404 for workspace and project details`

export const apiResources = [
  {
    description: `${apiBaseUrl}/openapi/v1.json`,
    href: `${apiBaseUrl}/openapi/v1.json`,
    icon: FileText,
    title: "OpenAPI JSON",
  },
  {
    description: `${apiBaseUrl}/api-docs`,
    href: `${apiBaseUrl}/api-docs`,
    icon: BracketsCurly,
    title: "API tester",
  },
] as const

export const docsSections = [
  {
    body: "Run Postgres, the .NET API, and the Vite app. The frontend is served on the Vite port, while API calls use `VITE_API_URL`.",
    code: {
      filename: "terminal",
      language: "bash",
      value: installCode,
    },
    icon: Lightning,
    id: "quickstart",
    title: "Quickstart",
  },
  {
    body: "The login page stores the returned session under `coordina.auth.session`. Protected API calls send the access token as a bearer token.",
    code: {
      filename: "auth-client.ts",
      language: "ts",
      value: authCode,
    },
    icon: ShieldCheck,
    id: "auth",
    title: "Authentication",
  },
  {
    body: "Workspaces are the root tenant boundary for Coordina. Projects, tasks, realtime channels, and future modules should scope data through the active workspace id.",
    code: {
      filename: "workspace-endpoints.txt",
      language: "http",
      value: workspaceApiCode,
    },
    icon: StackSimple,
    id: "workspaces-api",
    title: "Workspace API",
  },
  {
    body: "The server enforces membership and owner checks on every workspace endpoint. The frontend keeps useful state, but it is never trusted for authorization.",
    code: {
      filename: "security-model.txt",
      language: "txt",
      value: workspaceSecurityCode,
    },
    icon: ShieldCheck,
    id: "workspaces-security",
    title: "Security model",
  },
  {
    body: "Projects are the first workspace-owned business object. The API never serves a project by id alone; every read and delete is evaluated inside the workspace route.",
    code: {
      filename: "project-endpoints.txt",
      language: "http",
      value: projectsApiCode,
    },
    icon: StackSimple,
    id: "projects-api",
    title: "Projects API",
  },
  {
    body: "The authenticated app uses a shadcn-based SaaS shell with an onboarding path for new accounts and an active workspace context for returning users.",
    code: {
      filename: "workspace-ux.txt",
      language: "txt",
      value: workspaceUxCode,
    },
    icon: BracketsCurly,
    id: "workspaces-ux",
    title: "Frontend UX",
  },
  {
    body: "Workspace screens are built from reusable shadcn primitives so future modules can share the same SaaS surface.",
    code: {
      filename: "shadcn-usage.txt",
      language: "txt",
      value: shadcnUsageCode,
    },
    icon: FileText,
    id: "workspaces-ui",
    title: "shadcn usage",
  },
  {
    body: "Keep secrets in `.env`. Use `.env.example` as the shape of the config, never as production values.",
    code: {
      filename: ".env.example",
      language: "dotenv",
      value: envCode,
    },
    icon: Database,
    id: "environment",
    title: "Environment",
  },
  {
    body: "Backend integration tests cover workspace creation, member isolation, owner-only delete, unauthorized rejection, and list filtering. Use the manual flow to verify the app end to end.",
    code: {
      filename: "workspace-tests.txt",
      language: "txt",
      value: testCode,
    },
    icon: Lightning,
    id: "tests",
    title: "Testing",
  },
] as const

export function getDocsLlmContext() {
  return [
    "# Coordina Platform Docs",
    "Coordina is a React + .NET workspace product.",
    "",
    "Routes:",
    "- /: marketing home",
    "- /login: authentication page",
    "- /app: authenticated workspace console",
    "- /docs: platform documentation",
    "",
    "API:",
    `- OpenAPI JSON: ${apiBaseUrl}/openapi/v1.json`,
    `- API testing UI: ${apiBaseUrl}/api-docs`,
    "- Workspace endpoints require a bearer token and server-side membership validation.",
    "- Project endpoints are nested below /workspaces/{workspaceId} and are always workspace-scoped.",
    "",
    "Commands:",
    installCode,
    testCode,
    "",
    "Environment:",
    envCode,
  ].join("\n")
}
