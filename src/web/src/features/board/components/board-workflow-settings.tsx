import { ArrowDown, ArrowUp, Plus, Trash } from "@phosphor-icons/react"
import { useState } from "react"

import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import type { useBoardWorkflow } from "@/features/board/hooks/use-board-workflow"
import { boardTaskStatuses } from "@/features/board/model/board-constants"
import type { BoardList, BoardTaskStatus } from "@/types/task"

type BoardWorkflow = ReturnType<typeof useBoardWorkflow>

type BoardWorkflowSettingsProps = {
  workflow: BoardWorkflow
  isReadOnly: boolean
}

const selectClassName =
  "h-8 rounded-md border border-input bg-background px-2 text-xs shadow-xs outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50 disabled:opacity-50 dark:bg-white/[0.06]"

export function BoardWorkflowSettings({
  workflow,
  isReadOnly,
}: BoardWorkflowSettingsProps) {
  const { lists } = workflow

  return (
    <div className="grid gap-3">
      <ol className="grid gap-2" aria-label="Board lists">
        {lists.map((list, index) => (
          <WorkflowListRow
            key={list.id}
            list={list}
            otherLists={lists.filter((other) => other.id !== list.id)}
            isFirst={index === 0}
            isLast={index === lists.length - 1}
            isDisabled={isReadOnly || workflow.isSaving}
            workflow={workflow}
          />
        ))}
      </ol>
      {isReadOnly ? null : (
        <AddListRow
          isDisabled={workflow.isSaving}
          onAdd={(title, status) => workflow.add(title, status)}
        />
      )}
    </div>
  )
}

function WorkflowListRow({
  isDisabled,
  isFirst,
  isLast,
  list,
  otherLists,
  workflow,
}: {
  isDisabled: boolean
  isFirst: boolean
  isLast: boolean
  list: BoardList
  otherLists: BoardList[]
  workflow: BoardWorkflow
}) {
  const [title, setTitle] = useState(list.title)
  const [isDeleting, setIsDeleting] = useState(false)
  const [target, setTarget] = useState(otherLists[0]?.id ?? "")
  const cardCount = list.cards.length

  // A rejected or empty title goes back to the saved one.
  async function saveTitle() {
    if (!(await workflow.rename(list, title))) {
      setTitle(list.title)
    }
  }

  return (
    <li className="grid gap-2 rounded-md border border-zinc-950/10 bg-white p-2 dark:border-white/10 dark:bg-white/[0.04]">
      <div className="flex flex-wrap items-center gap-2">
        <Input
          aria-label={`Title of ${list.title}`}
          value={title}
          disabled={isDisabled}
          onChange={(event) => setTitle(event.target.value)}
          onBlur={() => void saveTitle()}
          onKeyDown={(event) => {
            if (event.key === "Enter") {
              void saveTitle()
            }
          }}
          className="h-8 min-w-40 flex-1 rounded-md text-sm"
        />
        <select
          aria-label={`Status of ${list.title}`}
          value={list.taskStatus}
          disabled={isDisabled}
          onChange={(event) =>
            void workflow.changeStatus(
              list,
              event.target.value as BoardTaskStatus
            )
          }
          className={selectClassName}
        >
          {boardTaskStatuses.map((status) => (
            <option key={status.value} value={status.value}>
              {status.label}
            </option>
          ))}
        </select>
        <span className="w-16 text-xs text-muted-foreground">
          {cardCount} {cardCount === 1 ? "card" : "cards"}
        </span>
        <Button
          type="button"
          variant="ghost"
          size="icon"
          className="size-8 rounded-md"
          aria-label={`Move ${list.title} up`}
          disabled={isDisabled || isFirst}
          onClick={() => void workflow.move(list, -1)}
        >
          <ArrowUp className="size-3.5" />
        </Button>
        <Button
          type="button"
          variant="ghost"
          size="icon"
          className="size-8 rounded-md"
          aria-label={`Move ${list.title} down`}
          disabled={isDisabled || isLast}
          onClick={() => void workflow.move(list, 1)}
        >
          <ArrowDown className="size-3.5" />
        </Button>
        <Button
          type="button"
          variant="ghost"
          size="icon"
          className="size-8 rounded-md text-destructive"
          aria-label={`Delete ${list.title}`}
          disabled={isDisabled}
          onClick={() => setIsDeleting(true)}
        >
          <Trash className="size-3.5" />
        </Button>
      </div>
      {isDeleting ? (
        <div className="flex flex-wrap items-center gap-2 rounded-md bg-zinc-950/[0.035] p-2 text-xs dark:bg-white/[0.05]">
          {cardCount > 0 ? (
            <label className="flex items-center gap-2">
              Move its {cardCount} {cardCount === 1 ? "card" : "cards"} to
              <select
                aria-label={`Move the cards of ${list.title} to`}
                value={target}
                onChange={(event) => setTarget(event.target.value)}
                className={selectClassName}
              >
                {otherLists.map((other) => (
                  <option key={other.id} value={other.id}>
                    {other.title}
                  </option>
                ))}
              </select>
            </label>
          ) : (
            <span>Delete this empty list?</span>
          )}
          <Button
            type="button"
            variant="destructive"
            size="sm"
            className="h-7 rounded-md px-2 text-xs"
            disabled={isDisabled}
            onClick={() =>
              void workflow.remove(list, cardCount > 0 ? target : null)
            }
          >
            Delete list
          </Button>
          <Button
            type="button"
            variant="ghost"
            size="sm"
            className="h-7 rounded-md px-2 text-xs"
            onClick={() => setIsDeleting(false)}
          >
            Cancel
          </Button>
        </div>
      ) : null}
    </li>
  )
}

function AddListRow({
  isDisabled,
  onAdd,
}: {
  isDisabled: boolean
  onAdd: (title: string, status: BoardTaskStatus) => Promise<boolean>
}) {
  const [title, setTitle] = useState("")
  const [status, setStatus] = useState<BoardTaskStatus>("TODO")

  async function handleAdd() {
    if (await onAdd(title, status)) {
      setTitle("")
    }
  }

  return (
    <div className="flex flex-wrap items-center gap-2">
      <Input
        aria-label="New list title"
        placeholder="New list"
        value={title}
        onChange={(event) => setTitle(event.target.value)}
        onKeyDown={(event) => {
          if (event.key === "Enter" && title.trim()) {
            void handleAdd()
          }
        }}
        className="h-8 min-w-40 flex-1 rounded-md text-sm"
      />
      <select
        aria-label="New list status"
        value={status}
        onChange={(event) => setStatus(event.target.value as BoardTaskStatus)}
        className={selectClassName}
      >
        {boardTaskStatuses.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
      <Button
        type="button"
        size="sm"
        className="h-8 rounded-md px-3"
        disabled={isDisabled || !title.trim()}
        onClick={() => void handleAdd()}
      >
        <Plus className="size-3.5" />
        Add list
      </Button>
    </div>
  )
}
