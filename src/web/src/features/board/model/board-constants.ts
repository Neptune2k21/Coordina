import type { BoardCardPriority, BoardTaskStatus } from "@/types/task"

export const boardTaskStatuses: Array<{
  value: BoardTaskStatus
  label: string
}> = [
  { value: "TODO", label: "To do" },
  { value: "IN_PROGRESS", label: "In progress" },
  { value: "DONE", label: "Done" },
]

export const boardCardPriorities: Array<{
  value: BoardCardPriority | ""
  label: string
}> = [
  { value: "", label: "None" },
  { value: "LOW", label: "Low" },
  { value: "MEDIUM", label: "Medium" },
  { value: "HIGH", label: "High" },
]

export function priorityLabel(priority: BoardCardPriority) {
  return (
    boardCardPriorities.find((item) => item.value === priority)?.label ??
    priority
  )
}
