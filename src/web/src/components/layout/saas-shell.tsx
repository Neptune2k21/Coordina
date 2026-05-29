import {
  BookOpenText,
  Command,
  House,
  Kanban,
  List,
  SignOut,
  Sparkle,
  UserCircle,
} from "@phosphor-icons/react"

import { Button } from "@/components/ui/button"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import { Separator } from "@/components/ui/separator"
import { useAuth } from "@/features/auth/auth-context"
import { WorkspaceSwitcher } from "@/features/workspaces/components/workspace-switcher"

export function SaasShell({ children }: { children: React.ReactNode }) {
  const { session, signOut } = useAuth()

  function navigate(path: string) {
    window.history.pushState({}, "", path)
    window.dispatchEvent(new PopStateEvent("popstate"))
  }

  return (
    <div className="relative min-h-svh overflow-hidden bg-zinc-50 text-foreground dark:bg-zinc-950 lg:grid lg:grid-cols-[272px_1fr]">
      <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_14%_8%,rgba(45,212,191,0.12),transparent_26%),radial-gradient(circle_at_92%_2%,rgba(236,72,153,0.08),transparent_22%),linear-gradient(180deg,rgba(255,255,255,0.92)_0%,rgba(244,244,245,0.74)_100%)] dark:bg-[radial-gradient(circle_at_14%_8%,rgba(20,184,166,0.09),transparent_26%),radial-gradient(circle_at_92%_2%,rgba(236,72,153,0.07),transparent_22%),linear-gradient(180deg,rgba(9,9,11,1)_0%,rgba(24,24,27,0.92)_100%)]" />

      <aside className="relative hidden border-r border-zinc-950/[0.08] bg-white/72 text-sidebar-foreground shadow-[18px_0_60px_rgba(24,24,27,0.04)] backdrop-blur-2xl lg:block dark:border-white/10 dark:bg-white/[0.045]">
        <div className="flex h-20 items-center gap-3 px-5">
          <span className="relative grid size-11 place-items-center overflow-hidden rounded-[16px] border border-zinc-950/10 bg-zinc-950 shadow-[0_18px_45px_rgba(9,9,11,0.18)] dark:border-white/10 dark:bg-white">
            <span className="absolute inset-0 bg-[radial-gradient(circle_at_70%_20%,rgba(101,255,218,0.82),transparent_30%),linear-gradient(140deg,rgba(255,255,255,0.22),transparent_48%)] dark:bg-[radial-gradient(circle_at_70%_20%,rgba(30,120,94,0.5),transparent_30%),linear-gradient(140deg,rgba(0,0,0,0.16),transparent_48%)]" />
            <Command className="relative size-5 text-white dark:text-zinc-950" weight="bold" />
          </span>
          <div>
            <p className="text-[15px] font-semibold leading-none">Coordina</p>
            <p className="mt-1 text-[11px] font-medium text-muted-foreground">
              Team workspace
            </p>
          </div>
        </div>
        <Separator className="bg-zinc-950/[0.08] dark:bg-white/10" />
        <nav className="grid gap-1.5 p-4">
          <Button
            type="button"
            variant="ghost"
            className="h-11 justify-start rounded-2xl bg-zinc-950/[0.045] px-3 text-sm shadow-[inset_0_0_0_1px_rgba(9,9,11,0.03)] transition-all duration-200 hover:-translate-y-0.5 hover:bg-zinc-950/[0.065] dark:bg-white/[0.08] dark:hover:bg-white/[0.12]"
          >
            <House className="size-4" />
            Workspace
          </Button>
          <Button
            type="button"
            variant="ghost"
            className="h-11 justify-start rounded-2xl px-3 text-sm text-muted-foreground"
            disabled
          >
            <Kanban className="size-4" />
            Projects
          </Button>
          <Button
            type="button"
            variant="ghost"
            className="h-11 justify-start rounded-2xl px-3 text-sm text-muted-foreground transition-all duration-200 hover:-translate-y-0.5 hover:bg-zinc-950/[0.045] hover:text-foreground dark:hover:bg-white/[0.08]"
            onClick={() => navigate("/docs")}
          >
            <BookOpenText className="size-4" />
            Docs
          </Button>
        </nav>
        <div className="absolute right-4 bottom-4 left-4 rounded-[24px] border border-zinc-950/[0.08] bg-white/70 p-4 shadow-[0_22px_60px_rgba(24,24,27,0.08)] backdrop-blur-2xl dark:border-white/10 dark:bg-white/[0.06]">
          <div className="flex items-center gap-3">
            <span className="grid size-9 place-items-center rounded-2xl bg-zinc-950 text-white dark:bg-white dark:text-zinc-950">
              <Sparkle className="size-4" weight="fill" />
            </span>
            <div>
              <p className="text-sm font-semibold">Workspace layer</p>
              <p className="mt-1 text-xs leading-5 text-muted-foreground">
                Secure tenant context for the product ahead.
              </p>
            </div>
          </div>
        </div>
      </aside>

      <div className="relative min-w-0">
        <header className="sticky top-0 z-30 border-b border-zinc-950/[0.08] bg-white/74 backdrop-blur-2xl dark:border-white/10 dark:bg-zinc-950/76">
          <div className="pointer-events-none absolute inset-x-0 top-full h-8 bg-[linear-gradient(to_bottom,rgba(255,255,255,0.62),transparent)] dark:bg-[linear-gradient(to_bottom,rgba(9,9,11,0.46),transparent)]" />
          <div className="relative flex h-16 items-center justify-between gap-3 px-4 lg:h-20 lg:px-6">
          <Button
            type="button"
            variant="ghost"
            size="icon"
            className="rounded-full border border-zinc-950/10 bg-white/72 shadow-[0_12px_32px_rgba(24,24,27,0.08)] lg:hidden dark:border-white/10 dark:bg-white/[0.06]"
            aria-label="Open navigation"
          >
            <List className="size-5" />
          </Button>
          <div className="min-w-0 flex-1 lg:max-w-[360px]">
            <WorkspaceSwitcher />
          </div>
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button
                type="button"
                variant="outline"
                size="icon"
                className="rounded-full border-zinc-950/10 bg-white/74 shadow-[0_12px_32px_rgba(24,24,27,0.08)] transition-transform duration-200 hover:-translate-y-0.5 dark:border-white/10 dark:bg-white/[0.06]"
                aria-label="User actions"
              >
                <UserCircle className="size-5" />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end" className="w-64">
              <DropdownMenuLabel>
                <span className="block truncate">{session?.user.name}</span>
                <span className="mt-1 block truncate text-xs font-normal text-muted-foreground">
                  {session?.user.email}
                </span>
              </DropdownMenuLabel>
              <DropdownMenuSeparator />
              <DropdownMenuItem onSelect={signOut}>
                <SignOut className="size-4" />
                Sign out
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
          </div>
        </header>
        <main className="mx-auto w-full max-w-7xl px-4 py-6 sm:px-6 lg:px-8 lg:py-8">
          {children}
        </main>
      </div>
    </div>
  )
}
