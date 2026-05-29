import {
  BracketsCurly,
  Database,
  FileText,
  Lightning,
  ShieldCheck,
} from "@phosphor-icons/react"

const apiBaseUrl = import.meta.env.VITE_API_URL ?? "http://localhost:5050"

export const docsApiBaseUrl = apiBaseUrl

export const docsNavItems = [
  { id: "quickstart", label: "Quickstart" },
  { id: "auth", label: "Authentication" },
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
] as const

export function getDocsLlmContext() {
  return [
    "# Coordina Platform Docs",
    "Coordina is a React + .NET workspace product.",
    "",
    "Routes:",
    "- /: marketing home",
    "- /login: authentication page",
    "- /docs: platform documentation",
    "",
    "API:",
    `- OpenAPI JSON: ${apiBaseUrl}/openapi/v1.json`,
    `- API testing UI: ${apiBaseUrl}/api-docs`,
    "",
    "Commands:",
    installCode,
    "",
    "Environment:",
    envCode,
  ].join("\n")
}
