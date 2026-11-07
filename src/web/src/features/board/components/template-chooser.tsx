import { CircleNotch, Plus } from "@phosphor-icons/react"
import { useId, useMemo, useState } from "react"

import { Button } from "@/components/ui/button"
import { Field, FieldLabel } from "@/components/ui/field"
import { Input } from "@/components/ui/input"
import { Skeleton } from "@/components/ui/skeleton"
import {
  BoardTemplateOption,
  BoardTemplatePreview,
} from "@/features/board/components/board-template-preview"
import { useBoardTemplates } from "@/features/board/hooks/use-board-templates"
import {
  countTemplateCards,
  countTemplateDependencies,
} from "@/features/board/model/board-templates"
import type { BoardCreationInput } from "@/types/task"

type TemplateChooserProps = {
  disabled?: boolean
  isCreating: boolean
  onCreate: (input: BoardCreationInput) => void
}

export function TemplateChooser({
  disabled = false,
  isCreating,
  onCreate,
}: TemplateChooserProps) {
  const { templates, status, retry } = useBoardTemplates()
  const [includeExampleCards, setIncludeExampleCards] = useState(true)
  const [customLists, setCustomLists] = useState("To Do, Doing, Review, Done")
  const exampleCardsId = useId()
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
          <div className="text-sm font-semibold">Start this board</div>
          <p className="mt-1 text-xs text-muted-foreground">
            Choose a previewed workflow or define custom list names.
          </p>
        </div>
        {isCreating ? <CircleNotch className="size-4 animate-spin" /> : null}
      </div>
      <label
        htmlFor={exampleCardsId}
        className="flex items-start gap-2 rounded-md border border-zinc-950/10 bg-zinc-50 p-2 text-xs dark:border-white/10 dark:bg-white/[0.035]"
      >
        <input
          id={exampleCardsId}
          type="checkbox"
          checked={includeExampleCards}
          disabled={isDisabled}
          className="mt-0.5 size-4"
          onChange={(event) => setIncludeExampleCards(event.target.checked)}
        />
        <span>
          <span className="block font-medium">Include example cards</span>
          <span className="mt-0.5 block text-muted-foreground">
            Shows how dependencies and next work rankings behave. Turn this off
            to start with empty lists.
          </span>
        </span>
      </label>
      {status === "loading" ? (
        <div
          className="grid gap-2 md:grid-cols-2"
          aria-label="Loading templates"
        >
          <Skeleton className="h-40" />
          <Skeleton className="h-40" />
          <Skeleton className="h-40" />
          <Skeleton className="h-40" />
        </div>
      ) : null}
      {status === "error" ? (
        <div
          role="alert"
          className="flex items-center justify-between gap-3 rounded-md border border-zinc-950/10 p-3 text-xs dark:border-white/10"
        >
          <span>Templates could not be loaded.</span>
          <Button
            type="button"
            size="sm"
            variant="outline"
            className="h-8 rounded-md px-3 text-xs"
            onClick={retry}
          >
            Try again
          </Button>
        </div>
      ) : null}
      {status === "ready" ? (
        <div className="grid gap-2 md:grid-cols-2">
          {templates.map((template) => (
            <BoardTemplateOption
              key={template.id}
              contents={describeContents(
                countTemplateCards(template),
                countTemplateDependencies(template),
                template.lists.length,
                includeExampleCards
              )}
              disabled={isDisabled}
              lists={template.lists.map((list) => ({
                title: list.title,
                cardTitles: includeExampleCards
                  ? list.cards.map((card) => card.title)
                  : [],
              }))}
              name={template.name}
              summary={template.summary}
              onSelect={() =>
                onCreate({ template: template.id, includeExampleCards })
              }
            />
          ))}
        </div>
      ) : null}
      <BoardTemplateOption
        contents="No lists, no cards"
        disabled={isDisabled}
        lists={[]}
        name="Blank"
        summary="Start from an empty board and add your own lists."
        onSelect={() => onCreate({ template: "BLANK" })}
      />
      <div className="grid gap-2 rounded-md border border-dashed border-zinc-950/15 bg-zinc-50 p-2 md:grid-cols-[1fr_auto] md:items-end dark:border-white/10 dark:bg-white/[0.035]">
        <Field>
          <FieldLabel htmlFor="custom-lists">Custom board lists</FieldLabel>
          <Input
            id="custom-lists"
            value={customLists}
            disabled={isDisabled}
            onChange={(event) => setCustomLists(event.target.value)}
            className="dark:bg-white/0.06 h-8 rounded-md bg-white text-xs"
          />
        </Field>
        <Button
          type="button"
          size="sm"
          className="h-8 rounded-md px-3 text-xs"
          disabled={isDisabled || customListTitles.length === 0}
          onClick={() => onCreate({ template: "CUSTOM", customListTitles })}
        >
          <Plus className="size-3.5" />
          Create custom
        </Button>
        <div className="md:col-span-2">
          <BoardTemplatePreview
            lists={customListTitles.map((title) => ({
              title,
              cardTitles: [],
            }))}
          />
        </div>
      </div>
    </div>
  )
}

function describeContents(
  cardCount: number,
  dependencyCount: number,
  listCount: number,
  includeExampleCards: boolean
) {
  if (!includeExampleCards) {
    return `${listCount} empty lists`
  }

  const dependencies = dependencyCount === 1 ? "dependency" : "dependencies"

  return `${cardCount} example cards, ${dependencyCount} ${dependencies}`
}
