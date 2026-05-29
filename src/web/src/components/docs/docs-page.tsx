import {
  ArrowRight,
  BookOpenText,
  BracketsCurly,
  Command,
  Copy,
  Database,
  FileText,
  Lightning,
  ShieldCheck,
} from "@phosphor-icons/react"
import type { ComponentType } from "react"
import { useMemo, useState } from "react"

import { CodeBlock } from "@/components/docs/code-block"
import { Button } from "@/components/ui/button"

const apiBaseUrl = import.meta.env.VITE_API_URL ?? "http://localhost:5050"

const sections = [
  { id: "quickstart", label: "Quickstart" },
  { id: "auth", label: "Authentication" },
  { id: "environment", label: "Environment" },
  { id: "api", label: "API testing" },
] as const

const installCode = `make docker-up
make api
make web`

const authCode = `const response = await fetch("${apiBaseUrl}/auth/login", {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({
    email: "you@company.com",
    password: "Password123!"
  })
})

const session = await response.json()
localStorage.setItem("coordina.auth.session", JSON.stringify(session))`

const envCode = `POSTGRES_USER=coordina
POSTGRES_PASSWORD=change-me
POSTGRES_DB=coordina
POSTGRES_HOST=localhost
POSTGRES_PORT=5432

Auth__Issuer=Coordina.Api
Auth__Audience=Coordina.Web
Auth__SigningKey=change-me-to-a-secure-random-secret-with-at-least-32-characters
Auth__AccessTokenMinutes=60`

const curlCode = `curl -X POST ${apiBaseUrl}/auth/register \\
  -H "Content-Type: application/json" \\
  -d '{
    "name": "Maya Chen",
    "email": "maya@coordina.test",
    "password": "Password123!"
  }'`

export function DocsPage() {
  const [copiedForLlm, setCopiedForLlm] = useState(false)
  const llmContext = useMemo(
    () =>
      [
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
      ].join("\n"),
    []
  )

  async function copyForLlm() {
    await navigator.clipboard.writeText(llmContext)
    setCopiedForLlm(true)
    window.setTimeout(() => setCopiedForLlm(false), 1400)
  }

  return (
    <div className="min-h-svh bg-white text-zinc-950 dark:bg-zinc-950 dark:text-white">
      <header className="sticky top-0 z-40 border-b border-zinc-950/10 bg-white/86 backdrop-blur-2xl dark:border-white/10 dark:bg-zinc-950/82">
        <div className="mx-auto flex max-w-7xl items-center justify-between px-4 py-3 sm:px-6 lg:px-8">
          <a
            href="/"
            className="flex items-center gap-3"
            aria-label="Coordina home"
          >
            <span className="grid size-10 place-items-center rounded-md bg-zinc-950 text-white dark:bg-white dark:text-zinc-950">
              <Command className="size-5" weight="bold" />
            </span>
            <div>
              <p className="text-sm leading-none font-semibold">Coordina</p>
              <p className="mt-1 text-xs font-medium text-muted-foreground">
                Documentation
              </p>
            </div>
          </a>
          <div className="flex items-center gap-2">
            <Button
              asChild
              variant="outline"
              className="hidden h-10 rounded-md sm:inline-flex"
            >
              <a href={`${apiBaseUrl}/api-docs`}>API docs</a>
            </Button>
            <Button className="h-10 rounded-md" onClick={copyForLlm}>
              <Copy className="size-4" />
              {copiedForLlm ? "Copied" : "Copy for LLM"}
            </Button>
          </div>
        </div>
      </header>

      <main className="mx-auto grid max-w-7xl gap-10 px-4 py-10 sm:px-6 lg:grid-cols-[220px_1fr] lg:px-8">
        <aside className="hidden lg:block">
          <nav className="sticky top-24 grid gap-1" aria-label="Documentation">
            {sections.map((section) => (
              <a
                key={section.id}
                href={`#${section.id}`}
                className="rounded-md px-3 py-2 text-sm font-medium text-muted-foreground transition-colors hover:bg-zinc-950/[0.04] hover:text-foreground dark:hover:bg-white/10"
              >
                {section.label}
              </a>
            ))}
          </nav>
        </aside>

        <div className="min-w-0">
          <section className="border-b border-zinc-950/10 pb-12 dark:border-white/10">
            <div className="inline-flex items-center gap-2 rounded-full border border-zinc-950/10 px-3 py-1.5 text-xs font-semibold text-zinc-700 dark:border-white/10 dark:text-zinc-200">
              <BookOpenText className="size-4 text-teal-600 dark:text-teal-300" />
              Platform guide
            </div>
            <h1 className="mt-6 max-w-3xl text-5xl leading-tight font-semibold tracking-normal sm:text-6xl">
              Build with Coordina without losing the shape of the product.
            </h1>
            <p className="mt-5 max-w-2xl text-base leading-8 text-muted-foreground">
              A compact guide for working on the React app, authentication flow,
              local environment, and API contract.
            </p>
            <div className="mt-8 flex flex-col gap-3 sm:flex-row">
              <Button asChild className="h-11 rounded-md">
                <a href="#quickstart">
                  Start building
                  <ArrowRight className="size-4" />
                </a>
              </Button>
              <Button asChild variant="outline" className="h-11 rounded-md">
                <a href={`${apiBaseUrl}/api-docs`}>Open API tester</a>
              </Button>
            </div>
          </section>

          <section
            id="quickstart"
            className="grid gap-5 border-b border-zinc-950/10 py-12 dark:border-white/10"
          >
            <DocHeading icon={Lightning} title="Quickstart" />
            <p className="max-w-2xl text-sm leading-7 text-muted-foreground">
              Run Postgres, the .NET API, and the Vite app. The frontend is
              served on the Vite port, while API calls use `VITE_API_URL`.
            </p>
            <CodeBlock filename="terminal" code={installCode} />
          </section>

          <section
            id="auth"
            className="grid gap-5 border-b border-zinc-950/10 py-12 dark:border-white/10"
          >
            <DocHeading icon={ShieldCheck} title="Authentication" />
            <p className="max-w-2xl text-sm leading-7 text-muted-foreground">
              The login page stores the returned session under
              `coordina.auth.session`. Protected API calls send the access token
              as a bearer token.
            </p>
            <CodeBlock
              filename="auth-client.ts"
              language="ts"
              code={authCode}
            />
          </section>

          <section
            id="environment"
            className="grid gap-5 border-b border-zinc-950/10 py-12 dark:border-white/10"
          >
            <DocHeading icon={Database} title="Environment" />
            <p className="max-w-2xl text-sm leading-7 text-muted-foreground">
              Keep secrets in `.env`. Use `.env.example` as the shape of the
              config, never as production values.
            </p>
            <CodeBlock
              filename=".env.example"
              language="dotenv"
              code={envCode}
            />
          </section>

          <section id="api" className="grid gap-5 py-12">
            <DocHeading icon={BracketsCurly} title="API testing" />
            <p className="max-w-2xl text-sm leading-7 text-muted-foreground">
              Use the interactive OpenAPI UI to test auth and health endpoints,
              or copy a curl request directly.
            </p>
            <div className="grid gap-3 sm:grid-cols-2">
              <DocLink
                icon={FileText}
                title="OpenAPI JSON"
                description={`${apiBaseUrl}/openapi/v1.json`}
                href={`${apiBaseUrl}/openapi/v1.json`}
              />
              <DocLink
                icon={BracketsCurly}
                title="API tester"
                description={`${apiBaseUrl}/api-docs`}
                href={`${apiBaseUrl}/api-docs`}
              />
            </div>
            <CodeBlock filename="register.sh" code={curlCode} />
          </section>
        </div>
      </main>
    </div>
  )
}

type IconComponent = ComponentType<{
  className?: string
  weight?: "regular" | "bold" | "fill"
}>

function DocHeading({
  icon: Icon,
  title,
}: {
  icon: IconComponent
  title: string
}) {
  return (
    <div className="flex items-center gap-3">
      <span className="grid size-10 place-items-center rounded-md border border-zinc-950/10 bg-zinc-50 dark:border-white/10 dark:bg-white/[0.06]">
        <Icon
          className="size-5 text-teal-600 dark:text-teal-300"
          weight="bold"
        />
      </span>
      <h2 className="text-2xl font-semibold tracking-normal">{title}</h2>
    </div>
  )
}

function DocLink({
  icon: Icon,
  title,
  description,
  href,
}: {
  icon: IconComponent
  title: string
  description: string
  href: string
}) {
  return (
    <a
      href={href}
      className="group rounded-md border border-zinc-950/10 p-4 transition-colors hover:bg-zinc-50 dark:border-white/10 dark:hover:bg-white/[0.06]"
    >
      <Icon className="size-5 text-teal-600 dark:text-teal-300" weight="bold" />
      <p className="mt-4 text-sm font-semibold">{title}</p>
      <p className="mt-1 text-xs leading-5 break-all text-muted-foreground">
        {description}
      </p>
    </a>
  )
}
