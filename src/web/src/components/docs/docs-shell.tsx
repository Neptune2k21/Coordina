import { ArrowRight, Command, Copy } from "@phosphor-icons/react"
import type { ReactNode } from "react"
import { useState } from "react"

import type { DocsIcon, DocsNavItem } from "@/components/docs/docs-types"
import { Button } from "@/components/ui/button"

type DocsShellProps = {
  apiDocsUrl: string
  badgeIcon: DocsIcon
  badgeText: string
  children: ReactNode
  copyText: string
  description: string
  navItems: readonly DocsNavItem[]
  primaryAction: {
    href: string
    label: string
  }
  secondaryAction: {
    href: string
    label: string
  }
  subtitle: string
  title: string
}

export function DocsShell({
  apiDocsUrl,
  badgeIcon: BadgeIcon,
  badgeText,
  children,
  copyText,
  description,
  navItems,
  primaryAction,
  secondaryAction,
  subtitle,
  title,
}: DocsShellProps) {
  const [copiedForLlm, setCopiedForLlm] = useState(false)

  async function copyForLlm() {
    await navigator.clipboard.writeText(copyText)
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
                {subtitle}
              </p>
            </div>
          </a>
          <div className="flex items-center gap-2">
            <Button
              asChild
              variant="outline"
              className="hidden h-10 rounded-md sm:inline-flex"
            >
              <a href={apiDocsUrl}>API docs</a>
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
            {navItems.map((item) => (
              <a
                key={item.id}
                href={`#${item.id}`}
                className="rounded-md px-3 py-2 text-sm font-medium text-muted-foreground transition-colors hover:bg-zinc-950/[0.04] hover:text-foreground dark:hover:bg-white/10"
              >
                {item.label}
              </a>
            ))}
          </nav>
        </aside>

        <div className="min-w-0">
          <section className="border-b border-zinc-950/10 pb-12 dark:border-white/10">
            <div className="inline-flex items-center gap-2 rounded-full border border-zinc-950/10 px-3 py-1.5 text-xs font-semibold text-zinc-700 dark:border-white/10 dark:text-zinc-200">
              <BadgeIcon className="size-4 text-teal-600 dark:text-teal-300" />
              {badgeText}
            </div>
            <h1 className="mt-6 max-w-3xl text-5xl leading-tight font-semibold tracking-normal sm:text-6xl">
              {title}
            </h1>
            <p className="mt-5 max-w-2xl text-base leading-8 text-muted-foreground">
              {description}
            </p>
            <div className="mt-8 flex flex-col gap-3 sm:flex-row">
              <Button asChild className="h-11 rounded-md">
                <a href={primaryAction.href}>
                  {primaryAction.label}
                  <ArrowRight className="size-4" />
                </a>
              </Button>
              <Button asChild variant="outline" className="h-11 rounded-md">
                <a href={secondaryAction.href}>{secondaryAction.label}</a>
              </Button>
            </div>
          </section>

          {children}
        </div>
      </main>
    </div>
  )
}
