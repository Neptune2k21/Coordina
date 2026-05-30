import { CircleNotch, Plus } from "@phosphor-icons/react"
import { useMemo, useState } from "react"

import { Button } from "@/components/ui/button"
import { Field, FieldLabel } from "@/components/ui/field"
import { Input } from "@/components/ui/input"
import { boardTemplates } from "@/features/tasks/board-constants"
import type { BoardTemplate } from "@/features/tasks/task-types"

type TemplateChooserProps = {
  disabled?: boolean
  isCreating: boolean
  onCreate: (template: BoardTemplate, customListTitles?: string[]) => void
}

export function TemplateChooser({
  disabled = false,
  isCreating,
  onCreate,
}: TemplateChooserProps) {
  const [customLists, setCustomLists] = useState("To Do, Doing, Review, Done")
  const customListTitles = useMemo(
    () =>
      customLists
        .split(",")
        .map((list) => list.trim())
        .filter(Boolean),
    [customLists]
  )
  const isDisabled = disabled || isCreating

  return (
    <div className="grid gap-3 rounded-md border border-zinc-950/10 bg-white p-3 shadow-xs dark:border-white/10 dark:bg-zinc-950">
      <div className="flex items-center justify-between gap-3">
        <div className="min-w-0">
          <div className="text-sm font-semibold">Choose board template</div>
          <p className="mt-1 text-xs text-muted-foreground">
            Start from a workflow or define custom list names.
          </p>
        </div>
        {isCreating ? <CircleNotch className="size-4 animate-spin" /> : null}
      </div>
      <div className="grid gap-2 md:grid-cols-2 xl:grid-cols-4">
        {boardTemplates.map((template) => (
          <button
            key={template.id}
            type="button"
            className="rounded-md border border-zinc-950/10 bg-white p-3 text-left transition-colors hover:bg-zinc-50 disabled:pointer-events-none disabled:opacity-50 dark:border-white/10 dark:bg-white/[0.04] dark:hover:bg-white/[0.08]"
            disabled={isDisabled}
            onClick={() => onCreate(template.id)}
          >
            <div className="text-sm font-semibold">{template.name}</div>
            <p className="mt-2 text-xs leading-5 text-muted-foreground">
              {template.summary}
            </p>
            <p className="mt-2 text-[11px] leading-5 text-muted-foreground">
              {template.lists}
            </p>
          </button>
        ))}
      </div>
      <div className="grid gap-2 rounded-md border border-dashed border-zinc-950/15 bg-zinc-50 p-2 md:grid-cols-[1fr_auto] md:items-end dark:border-white/10 dark:bg-white/[0.035]">
        <Field>
          <FieldLabel htmlFor="custom-lists">Custom board lists</FieldLabel>
          <Input
            id="custom-lists"
            value={customLists}
            disabled={isDisabled}
            onChange={(event) => setCustomLists(event.target.value)}
            className="h-8 rounded-md bg-white text-xs dark:bg-white/[0.06]"
          />
        </Field>
        <Button
          type="button"
          size="sm"
          className="h-8 rounded-md px-3 text-xs"
          disabled={isDisabled || customListTitles.length === 0}
          onClick={() => onCreate("CUSTOM", customListTitles)}
        >
          <Plus className="size-3.5" />
          Create custom
        </Button>
      </div>
    </div>
  )
}
