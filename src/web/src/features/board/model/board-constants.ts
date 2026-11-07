import type { BoardCardPriority } from "@/types/task"

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
