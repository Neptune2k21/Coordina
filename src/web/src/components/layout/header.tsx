import {
  ArrowRight,
  BookOpenText,
  Buildings,
  CaretDown,
  CirclesThreePlus,
  Command,
  List,
  SignIn,
  Sparkle,
  X,
} from "@phosphor-icons/react"
import { useState } from "react"

import { Button } from "@/components/ui/button"
import { cn } from "@/lib/utils"

const navItems = [
  {
    label: "Platform",
    href: "#platform",
    eyebrow: "Core system",
    description: "Boards, tasks, docs and live context in one workspace.",
    icon: CirclesThreePlus,
    items: ["Workspace graph", "Realtime boards", "Context blocks"],
  },
  {
    label: "Teams",
    href: "#solution",
    eyebrow: "By team",
    description:
      "A calmer operating layer for product, ops and delivery teams.",
    icon: Buildings,
    items: ["Product planning", "Client delivery", "Team rituals"],
  },
  {
    label: "Resources",
    href: "#resources",
    eyebrow: "Learn",
    description:
      "Guides, changelog and patterns for building better workflows.",
    icon: BookOpenText,
    items: ["Playbooks", "Release notes", "API docs"],
  },
] as const

export function Header() {
  const [isOpen, setIsOpen] = useState(false)

  return (
    <header className="sticky top-0 z-50 border-b border-zinc-950/[0.08] bg-white/82 backdrop-blur-2xl dark:border-white/10 dark:bg-zinc-950/78">
      <div className="pointer-events-none absolute inset-x-0 top-full h-8 bg-[linear-gradient(to_bottom,rgba(255,255,255,0.62),transparent)] dark:bg-[linear-gradient(to_bottom,rgba(9,9,11,0.46),transparent)]" />

      <div className="relative mx-auto flex max-w-7xl items-center justify-between px-4 py-3 sm:px-6 lg:px-8">
        <a
          href="/"
          className="group flex items-center gap-3"
          aria-label="Coordina"
        >
          <span className="relative grid size-10 place-items-center overflow-hidden rounded-[14px] border border-zinc-950/10 bg-zinc-950 shadow-[0_18px_45px_rgba(9,9,11,0.18)] dark:border-white/10 dark:bg-white">
            <span className="absolute inset-0 bg-[radial-gradient(circle_at_70%_20%,rgba(101,255,218,0.9),transparent_28%),linear-gradient(140deg,rgba(255,255,255,0.22),transparent_48%)] dark:bg-[radial-gradient(circle_at_70%_20%,rgba(30,120,94,0.5),transparent_28%),linear-gradient(140deg,rgba(0,0,0,0.16),transparent_48%)]" />
            <Command className="relative size-5 text-white transition-transform duration-300 group-hover:rotate-12 dark:text-zinc-950" />
          </span>
          <span className="flex flex-col">
            <span className="text-[15px] leading-none font-semibold tracking-normal">
              Coordina
            </span>
            <span className="mt-1 text-[11px] leading-none font-medium text-muted-foreground">
              Team workspace
            </span>
          </span>
        </a>

        <nav
          className="hidden items-center gap-1 rounded-full border border-zinc-950/[0.08] bg-white/62 p-1 shadow-[0_10px_32px_rgba(24,24,27,0.07)] md:flex dark:border-white/10 dark:bg-white/[0.05] dark:shadow-none"
          aria-label="Main"
        >
          {navItems.map((item) => {
            const Icon = item.icon

            return (
              <div key={item.href} className="group relative">
                <a
                  href={item.href}
                  className="flex h-10 items-center gap-1.5 rounded-full px-4 text-sm font-medium text-zinc-600 transition-all duration-200 hover:bg-zinc-950/[0.04] hover:text-zinc-950 focus-visible:bg-zinc-950/[0.04] focus-visible:text-zinc-950 focus-visible:ring-1 focus-visible:ring-ring focus-visible:outline-none dark:text-zinc-300 dark:hover:bg-white/10 dark:hover:text-white dark:focus-visible:bg-white/10 dark:focus-visible:text-white"
                >
                  {item.label}
                  <CaretDown className="size-3.5 transition-transform duration-200 group-hover:rotate-180" />
                </a>

                <div className="pointer-events-none absolute top-12 left-1/2 w-[318px] -translate-x-1/2 translate-y-2 opacity-0 transition-all duration-200 group-focus-within:pointer-events-auto group-focus-within:translate-y-0 group-focus-within:opacity-100 group-hover:pointer-events-auto group-hover:translate-y-0 group-hover:opacity-100">
                  <div className="rounded-[22px] border border-zinc-950/[0.08] bg-white/94 p-4 shadow-[0_24px_76px_rgba(24,24,27,0.15)] backdrop-blur-2xl dark:border-white/10 dark:bg-zinc-950/94 dark:shadow-[0_24px_76px_rgba(0,0,0,0.34)]">
                    <div className="flex items-start gap-3">
                      <span className="grid size-10 shrink-0 place-items-center rounded-2xl bg-zinc-950 text-white dark:bg-white dark:text-zinc-950">
                        <Icon className="size-5" weight="bold" />
                      </span>
                      <div>
                        <p className="text-[11px] font-semibold tracking-[0.16em] text-zinc-400 uppercase">
                          {item.eyebrow}
                        </p>
                        <p className="mt-1 text-sm leading-6 text-zinc-700 dark:text-zinc-200">
                          {item.description}
                        </p>
                      </div>
                    </div>
                    <div className="mt-4 grid gap-1">
                      {item.items.map((detail) => (
                        <a
                          key={detail}
                          href={item.href}
                          className="flex items-center justify-between rounded-xl px-3 py-2 text-sm font-medium text-zinc-600 transition-colors hover:bg-zinc-950/[0.04] hover:text-zinc-950 dark:text-zinc-300 dark:hover:bg-white/10 dark:hover:text-white"
                        >
                          {detail}
                          <ArrowRight className="size-3.5 opacity-45 transition-transform group-hover:translate-x-0.5" />
                        </a>
                      ))}
                    </div>
                  </div>
                </div>
              </div>
            )
          })}
        </nav>

        <div className="hidden items-center gap-2 md:flex">
          <Button
            asChild
            variant="ghost"
            className="h-10 rounded-full px-4 text-sm text-muted-foreground hover:text-foreground"
          >
            <a href="/login">
              <SignIn className="size-4" />
              Sign in
            </a>
          </Button>
          <Button
            asChild
            className="h-10 rounded-full bg-zinc-950 px-5 text-sm text-white shadow-[0_14px_34px_rgba(9,9,11,0.22)] transition-transform duration-300 hover:-translate-y-0.5 hover:bg-zinc-900 dark:bg-white dark:text-zinc-950 dark:hover:bg-zinc-100"
          >
            <a href="/login">
              Get started
              <ArrowRight className="size-4" />
            </a>
          </Button>
        </div>

        <Button
          type="button"
          variant="ghost"
          size="icon"
          className="rounded-full border border-zinc-950/10 bg-white/72 shadow-[0_12px_32px_rgba(24,24,27,0.08)] md:hidden dark:border-white/10 dark:bg-white/[0.06]"
          aria-label={isOpen ? "Close menu" : "Open menu"}
          aria-expanded={isOpen}
          onClick={() => setIsOpen((current) => !current)}
        >
          {isOpen ? <X className="size-5" /> : <List className="size-5" />}
        </Button>
      </div>

      <div
        className={cn(
          "relative mx-4 grid overflow-hidden rounded-[26px] border border-zinc-950/[0.08] bg-white/94 shadow-[0_24px_70px_rgba(24,24,27,0.14)] transition-[grid-template-rows,opacity,transform] duration-250 sm:mx-6 md:hidden dark:border-white/10 dark:bg-zinc-950/94",
          isOpen
            ? "translate-y-0 grid-rows-[1fr] opacity-100"
            : "pointer-events-none -translate-y-3 grid-rows-[0fr] opacity-0"
        )}
      >
        <div className="min-h-0">
          <nav className="grid gap-2 p-3" aria-label="Mobile main">
            {navItems.map((item) => (
              <a
                key={item.href}
                href={item.href}
                className="group grid gap-2 rounded-2xl border border-transparent px-4 py-4 transition-colors hover:border-zinc-950/[0.08] hover:bg-zinc-950/[0.035] dark:hover:border-white/10 dark:hover:bg-white/[0.06]"
                onClick={() => setIsOpen(false)}
              >
                <span className="flex items-center justify-between text-sm font-semibold">
                  {item.label}
                  <ArrowRight className="size-4 text-muted-foreground transition-transform group-hover:translate-x-0.5" />
                </span>
                <span className="text-sm leading-6 text-muted-foreground">
                  {item.description}
                </span>
              </a>
            ))}
            <div className="grid gap-2 border-t border-zinc-950/10 p-3 dark:border-white/10">
              <div className="grid grid-cols-3 gap-2 pb-2">
                <div className="rounded-2xl bg-zinc-950/[0.035] p-3 dark:bg-white/[0.06]">
                  <CirclesThreePlus className="mb-2 size-4 text-muted-foreground" />
                  <p className="text-[11px] leading-4 font-medium text-muted-foreground">
                    Boards
                  </p>
                </div>
                <div className="rounded-2xl bg-zinc-950/[0.035] p-3 dark:bg-white/[0.06]">
                  <BookOpenText className="mb-2 size-4 text-muted-foreground" />
                  <p className="text-[11px] leading-4 font-medium text-muted-foreground">
                    Docs
                  </p>
                </div>
                <div className="rounded-2xl bg-zinc-950/[0.035] p-3 dark:bg-white/[0.06]">
                  <Buildings className="mb-2 size-4 text-muted-foreground" />
                  <p className="text-[11px] leading-4 font-medium text-muted-foreground">
                    Teams
                  </p>
                </div>
              </div>
              <Button asChild variant="outline" className="h-11 rounded-full">
                <a href="/login" onClick={() => setIsOpen(false)}>
                  Sign in
                </a>
              </Button>
              <Button
                asChild
                className="h-11 rounded-full bg-zinc-950 text-white dark:bg-white dark:text-zinc-950"
              >
                <a href="/login" onClick={() => setIsOpen(false)}>
                  <Sparkle className="size-4" />
                  Get started
                </a>
              </Button>
            </div>
          </nav>
        </div>
      </div>
    </header>
  )
}
