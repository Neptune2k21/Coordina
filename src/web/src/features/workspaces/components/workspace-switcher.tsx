import { CaretDown, Check, Gear } from "@phosphor-icons/react"

import { Button } from "@/components/ui/button"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import { WorkspaceCreateDialog } from "@/features/workspaces/components/workspace-create-dialog"
import { useWorkspaces } from "@/features/workspaces/workspace-context"

export function WorkspaceSwitcher() {
  const { activeWorkspace, setActiveWorkspaceId, workspaces } = useWorkspaces()

  function navigate(path: string) {
    window.history.pushState({}, "", path)
    window.dispatchEvent(new PopStateEvent("popstate"))
  }

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button
          type="button"
          variant="outline"
          className="h-11 min-w-0 justify-between rounded-full border-zinc-950/10 bg-white/74 px-3.5 text-sm shadow-[0_12px_32px_rgba(24,24,27,0.08)] backdrop-blur-xl transition-all duration-200 hover:-translate-y-0.5 hover:bg-white dark:border-white/10 dark:bg-white/[0.06] dark:hover:bg-white/[0.1]"
        >
          <span className="mr-2 grid size-7 place-items-center rounded-full bg-zinc-950 text-[11px] font-bold text-white dark:bg-white dark:text-zinc-950">
            {(activeWorkspace?.name ?? "W").slice(0, 1).toUpperCase()}
          </span>
          <span className="truncate font-semibold">
            {activeWorkspace?.name ?? "Select workspace"}
          </span>
          <CaretDown className="size-4 text-muted-foreground" />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent
        align="start"
        className="w-72 rounded-[18px] border-zinc-950/10 bg-white/94 p-2 shadow-[0_24px_76px_rgba(24,24,27,0.15)] backdrop-blur-2xl dark:border-white/10 dark:bg-zinc-950/94"
      >
        <DropdownMenuLabel>Workspaces</DropdownMenuLabel>
        <DropdownMenuSeparator />
        {workspaces.map((workspace) => (
          <DropdownMenuItem
            key={workspace.id}
            onSelect={() => setActiveWorkspaceId(workspace.id)}
          >
            <span className="grid size-8 place-items-center rounded-xl bg-zinc-950/[0.055] text-[11px] font-bold dark:bg-white/[0.08]">
              {workspace.name.slice(0, 1).toUpperCase()}
            </span>
            <span className="min-w-0 flex-1 truncate text-sm font-medium">
              {workspace.name}
            </span>
            {workspace.id === activeWorkspace?.id ? (
              <Check className="size-4" weight="bold" />
            ) : null}
          </DropdownMenuItem>
        ))}
        <DropdownMenuSeparator />
        <DropdownMenuItem onSelect={() => navigate("/app/workspace-settings")}>
          <Gear className="size-4" />
          Workspace settings
        </DropdownMenuItem>
        <WorkspaceCreateDialog trigger="menu-item" />
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
