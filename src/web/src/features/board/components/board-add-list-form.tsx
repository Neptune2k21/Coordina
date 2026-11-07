import { Plus, X } from "@phosphor-icons/react"
import { useState } from "react"

import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { boardTaskStatuses } from "@/features/board/model/board-constants"
import type { BoardTaskStatus } from "@/types/task"

type BoardAddListFormProps = {
  isSaving: boolean
  onCancel: () => void
  onSubmit: (title: string, taskStatus: BoardTaskStatus) => void
}

// Keeps what is being typed here, so typing does not re-render the whole board.
export function BoardAddListForm({
  isSaving,
  onCancel,
  onSubmit,
}: BoardAddListFormProps) {
  const [title, setTitle] = useState("")
  const [taskStatus, setTaskStatus] = useState<BoardTaskStatus>("TODO")

  return (
    <div className="grid w-72 shrink-0 gap-2 self-start rounded-md border border-zinc-950/10 bg-white p-2 shadow-xs dark:border-white/10 dark:bg-zinc-950">
      <div className="flex gap-2">
        <Input
          value={title}
          onChange={(event) => setTitle(event.target.value)}
          placeholder="New list"
          className="h-8 rounded-md text-xs"
          onKeyDown={(event) => {
            if (event.key === "Enter") {
              onSubmit(title, taskStatus)
            }
          }}
        />
        <Button
          type="button"
          variant="ghost"
          size="icon"
          className="size-8 rounded-md"
          aria-label="Cancel"
          onClick={onCancel}
        >
          <X className="size-3.5" />
        </Button>
        <Button
          type="button"
          size="sm"
          className="h-8 rounded-md px-2"
          aria-label="Add list"
          disabled={!title.trim() || isSaving}
          onClick={() => onSubmit(title, taskStatus)}
        >
          <Plus className="size-3.5" />
        </Button>
      </div>
      <label className="flex items-center justify-between gap-2 text-xs text-muted-foreground">
        Status
        <select
          className="h-7 min-w-0 rounded-md border border-zinc-950/10 bg-white px-1.5 text-[11px] text-foreground dark:border-white/10 dark:bg-zinc-950"
          value={taskStatus}
          onChange={(event) =>
            setTaskStatus(event.target.value as BoardTaskStatus)
          }
        >
          {boardTaskStatuses.map((status) => (
            <option key={status.value} value={status.value}>
              {status.label}
            </option>
          ))}
        </select>
      </label>
    </div>
  )
}
