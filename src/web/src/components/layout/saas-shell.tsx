import {
  Command,
  Gear,
  Kanban,
  List,
  SignOut,
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

type SaasShellProps = {
  children: React.ReactNode
  currentPath: string
  onNavigate: (path: string) => void
}

export function SaasShell({
  children,
  currentPath,
  onNavigate,
}: SaasShellProps) {
  const { session, signOut } = useAuth()
  const isProjectsPage = currentPath.startsWith("/app/projects")
  const isProjectDetailPage = /^\/app\/projects\/[^/]+$/.test(currentPath)
  const isSettingsPage = currentPath.startsWith("/app/workspace-settings")

  return (
    <div
      className={`relative h-svh overflow-hidden bg-zinc-50 text-foreground dark:bg-zinc-950 ${
        isProjectDetailPage
          ? "lg:grid lg:grid-cols-[1fr]"
          : "lg:grid lg:grid-cols-[272px_1fr]"
      }`}
    >
      <div
        className={`pointer-events-none absolute inset-0 ${
          isProjectDetailPage
            ? "bg-zinc-100 dark:bg-zinc-950"
            : "bg-[radial-gradient(circle_at_14%_8%,rgba(45,212,191,0.12),transparent_26%),radial-gradient(circle_at_92%_2%,rgba(236,72,153,0.08),transparent_22%),linear-gradient(180deg,rgba(255,255,255,0.92)_0%,rgba(244,244,245,0.74)_100%)] dark:bg-[radial-gradient(circle_at_14%_8%,rgba(20,184,166,0.09),transparent_26%),radial-gradient(circle_at_92%_2%,rgba(236,72,153,0.07),transparent_22%),linear-gradient(180deg,rgba(9,9,11,1)_0%,rgba(24,24,27,0.92)_100%)]"
        }`}
      />

      <aside
        className={`relative hidden h-svh min-h-0 border-r border-zinc-950/[0.08] bg-white/72 text-sidebar-foreground shadow-[18px_0_60px_rgba(24,24,27,0.04)] backdrop-blur-2xl dark:border-white/10 dark:bg-white/[0.045] ${
          isProjectDetailPage ? "lg:hidden" : "lg:flex lg:flex-col"
        }`}
      >
        <button
          type="button"
          className="flex h-20 w-full items-center gap-3 px-5 text-left transition-colors hover:bg-zinc-950/[0.035] dark:hover:bg-white/[0.055]"
          onClick={() => onNavigate("/app")}
        >
          <span className="relative grid size-11 place-items-center overflow-hidden rounded-[16px] border border-zinc-950/10 bg-zinc-950 shadow-[0_18px_45px_rgba(9,9,11,0.18)] dark:border-white/10 dark:bg-white">
            <span className="absolute inset-0 bg-[radial-gradient(circle_at_70%_20%,rgba(101,255,218,0.82),transparent_30%),linear-gradient(140deg,rgba(255,255,255,0.22),transparent_48%)] dark:bg-[radial-gradient(circle_at_70%_20%,rgba(30,120,94,0.5),transparent_30%),linear-gradient(140deg,rgba(0,0,0,0.16),transparent_48%)]" />
            <Command
              className="relative size-5 text-white dark:text-zinc-950"
              weight="bold"
            />
          </span>
          <div>
            <p className="text-[15px] leading-none font-semibold">Coordina</p>
            <p className="mt-1 text-[11px] font-medium text-muted-foreground">
              Team workspace
            </p>
          </div>
        </button>
        <Separator className="bg-zinc-950/[0.08] dark:bg-white/10" />
        <nav className="min-h-0 flex-1 overflow-y-auto p-4">
          <div className="grid gap-1.5">
            <Button
              type="button"
              variant="ghost"
              className={`h-11 justify-start rounded-2xl px-3 text-sm transition-all duration-200 hover:-translate-y-0.5 hover:bg-zinc-950/[0.065] dark:hover:bg-white/[0.12] ${
                isProjectsPage
                  ? "bg-zinc-950/[0.045] shadow-[inset_0_0_0_1px_rgba(9,9,11,0.03)] dark:bg-white/[0.08]"
                  : "text-muted-foreground"
              }`}
              onClick={() => onNavigate("/app/projects")}
            >
              <Kanban className="size-4" />
              Projects
            </Button>
          </div>
        </nav>
        <div className="grid shrink-0 gap-2 border-t border-zinc-950/[0.08] p-4 dark:border-white/10">
          <Button
            type="button"
            variant="ghost"
            className={`h-11 justify-start rounded-2xl px-3 text-sm transition-all duration-200 hover:-translate-y-0.5 hover:bg-zinc-950/[0.065] dark:hover:bg-white/[0.12] ${
              isSettingsPage
                ? "bg-zinc-950/[0.045] shadow-[inset_0_0_0_1px_rgba(9,9,11,0.03)] dark:bg-white/[0.08]"
                : "text-muted-foreground"
            }`}
            onClick={() => onNavigate("/app/workspace-settings")}
          >
            <Gear className="size-4" />
            Settings
          </Button>
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button
                type="button"
                variant="outline"
                className="h-12 justify-start rounded-2xl border-zinc-950/10 bg-white/72 px-3 text-left shadow-[0_12px_32px_rgba(24,24,27,0.08)] dark:border-white/10 dark:bg-white/[0.06]"
                aria-label="User actions"
              >
                <span className="grid size-8 place-items-center rounded-full bg-zinc-950 text-white dark:bg-white dark:text-zinc-950">
                  <UserCircle className="size-5" />
                </span>
                <span className="min-w-0 flex-1">
                  <span className="block truncate text-sm font-semibold">
                    {session?.user.name}
                  </span>
                  <span className="block truncate text-[11px] font-normal text-muted-foreground">
                    {session?.user.email}
                  </span>
                </span>
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="start" side="right" className="w-64">
              <DropdownMenuLabel>Profile</DropdownMenuLabel>
              <DropdownMenuSeparator />
              <DropdownMenuItem onSelect={signOut}>
                <SignOut className="size-4" />
                Sign out
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        </div>
      </aside>

      <div className="relative h-svh min-w-0 overflow-y-auto">
        <header className="sticky top-0 z-30 border-b border-zinc-950/[0.08] bg-white/84 backdrop-blur-2xl dark:border-white/10 dark:bg-zinc-950/86">
          <div className="relative flex h-12 items-center justify-between gap-3 px-3 lg:px-4">
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
                  className="rounded-full border-zinc-950/10 bg-white/74 shadow-[0_12px_32px_rgba(24,24,27,0.08)] transition-transform duration-200 hover:-translate-y-0.5 lg:hidden dark:border-white/10 dark:bg-white/[0.06]"
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
        <main
          className={
            isProjectDetailPage
              ? "w-full px-2 py-2"
              : "mx-auto w-full max-w-7xl px-4 py-6 sm:px-6 lg:px-8 lg:py-8"
          }
        >
          {children}
        </main>
      </div>
    </div>
  )
}
