import { CheckCircle } from "@phosphor-icons/react"

import { cn } from "@/lib/utils"

const maxVisibleLists = 4
const maxVisibleCards = 2

type BoardTemplatePreviewProps = {
  lists: Array<{ title: string; cardTitles: string[] }>
}

type BoardTemplateOptionProps = BoardTemplatePreviewProps & {
  checked?: boolean
  contents: string
  disabled?: boolean
  name: string
  summary: string
  onSelect: () => void
}

export function BoardTemplateOption({
  checked = false,
  contents,
  disabled = false,
  lists,
  name,
  onSelect,
  summary,
}: BoardTemplateOptionProps) {
  return (
    <button
      type="button"
      className={cn(
        "grid gap-3 rounded-md border bg-white p-3 text-left shadow-xs transition-colors disabled:pointer-events-none disabled:opacity-50 dark:bg-white/[0.045]",
        checked
          ? "border-teal-500/45 ring-1 ring-teal-500/25 dark:border-teal-300/45"
          : "border-zinc-950/10 hover:bg-zinc-50 dark:border-white/10 dark:hover:bg-white/[0.08]"
      )}
      disabled={disabled}
      aria-pressed={checked}
      onClick={onSelect}
    >
      <span className="flex items-start justify-between gap-2">
        <span className="min-w-0">
          <span className="block text-sm font-semibold">{name}</span>
          <span className="mt-1 block text-xs leading-5 text-muted-foreground">
            {summary}
          </span>
          <span className="mt-1 block text-[11px] font-medium text-zinc-700 dark:text-zinc-200">
            {contents}
          </span>
        </span>
        {checked ? (
          <CheckCircle
            className="mt-0.5 size-4 shrink-0 text-teal-600 dark:text-teal-300"
            weight="fill"
          />
        ) : null}
      </span>
      <BoardTemplatePreview lists={lists} />
    </button>
  )
}

export function BoardTemplatePreview({ lists }: BoardTemplatePreviewProps) {
  const visibleLists = lists.slice(0, maxVisibleLists)

  return (
    <span className="grid gap-1.5">
      <span className="grid grid-cols-2 gap-1.5 sm:grid-cols-4">
        {visibleLists.map((list) => (
          <span
            key={list.title}
            className="min-h-16 rounded-md border border-zinc-950/10 bg-zinc-50 p-1.5 dark:border-white/10 dark:bg-zinc-950/78"
          >
            <span className="block truncate text-[10px] font-semibold text-zinc-700 dark:text-zinc-200">
              {list.title}
            </span>
            <span className="mt-1 grid gap-1">
              {list.cardTitles.slice(0, maxVisibleCards).map((card) => (
                <span
                  key={card}
                  className="block truncate rounded-sm bg-white px-1.5 py-1 text-[10px] text-muted-foreground shadow-xs dark:bg-white/[0.07]"
                >
                  {card}
                </span>
              ))}
            </span>
          </span>
        ))}
      </span>
      {lists.length > visibleLists.length ? (
        <span className="text-[11px] leading-5 text-muted-foreground">
          + {lists.length - visibleLists.length} more list
          {lists.length - visibleLists.length > 1 ? "s" : ""}
        </span>
      ) : null}
    </span>
  )
}
