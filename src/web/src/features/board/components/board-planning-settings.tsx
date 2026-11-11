import { CircleNotch, FloppyDisk } from "@phosphor-icons/react"

import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { usePlanningSettings } from "@/features/board/hooks/use-planning-settings"
import {
  type CardSize,
  focusSlotOptions,
} from "@/features/board/model/card-size"

type BoardPlanningSettingsProps = {
  accessToken: string
  workspaceId: string
  projectId: string
  boardId: string
}

export function BoardPlanningSettings(props: BoardPlanningSettingsProps) {
  const { preferences, isLoading, isSaving, isSaved, error, change, save } =
    usePlanningSettings(props)

  if (isLoading) {
    return <p className="text-sm text-muted-foreground">Loading…</p>
  }

  return (
    <form
      className="grid gap-3"
      aria-label="Planning preferences"
      onSubmit={(event) => {
        event.preventDefault()
        void save()
      }}
    >
      <div className="grid gap-3 sm:grid-cols-2">
        <label className="grid gap-1 text-xs font-medium">
          Focus slot
          <select
            value={preferences.maxSize ?? ""}
            onChange={(event) =>
              change({
                maxSize: (event.target.value || null) as CardSize | null,
              })
            }
            className="h-9 rounded-md border border-input bg-background px-3 text-sm font-normal shadow-xs outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50 dark:bg-white/[0.06]"
          >
            {focusSlotOptions.map((option) => (
              <option key={option.label} value={option.value ?? ""}>
                {option.label}
              </option>
            ))}
          </select>
        </label>
        <label className="grid gap-1 text-xs font-medium">
          Context label
          <Input
            value={preferences.context ?? ""}
            placeholder="frontend"
            onChange={(event) =>
              change({ context: event.target.value || null })
            }
            className="h-9 rounded-md text-sm font-normal"
          />
        </label>
      </div>
      <label className="flex items-center gap-2 text-sm">
        <input
          type="checkbox"
          checked={preferences.preferAssigned}
          onChange={(event) => change({ preferAssigned: event.target.checked })}
        />
        Rank the cards assigned to me first
      </label>
      <label className="flex items-center gap-2 text-sm">
        <input
          type="checkbox"
          checked={preferences.includeUnassigned}
          onChange={(event) =>
            change({ includeUnassigned: event.target.checked })
          }
        />
        Include unassigned cards
      </label>
      {error ? <p className="text-sm text-destructive">{error}</p> : null}
      <div className="flex items-center gap-3">
        <Button
          type="submit"
          disabled={isSaving}
          className="h-9 rounded-md px-3"
        >
          {isSaving ? (
            <CircleNotch className="size-4 animate-spin" />
          ) : (
            <FloppyDisk className="size-4" weight="bold" />
          )}
          Save preferences
        </Button>
        {isSaved ? (
          <span className="text-xs text-muted-foreground" role="status">
            Saved. The board starts with them.
          </span>
        ) : null}
      </div>
    </form>
  )
}
