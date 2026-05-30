export type BoardTemplate =
  | "BASIC"
  | "AGILE_SCRUM"
  | "BUG_TRACKING"
  | "PRODUCT_ROADMAP"
  | "CUSTOM"

export type BoardCardPriority = "LOW" | "MEDIUM" | "HIGH"

export type Board = {
  id: string
  projectId: string
  name: string
  template: BoardTemplate
  createdAt: string
  updatedAt: string
  lists: BoardList[]
}

export type BoardList = {
  id: string
  boardId: string
  title: string
  position: number
  createdAt: string
  updatedAt: string
  cards: BoardCard[]
}

export type BoardCard = {
  id: string
  boardId: string
  listId: string
  title: string
  description: string | null
  priority: BoardCardPriority | null
  dueDate: string | null
  labels: string[]
  position: number
  createdAt: string
  updatedAt: string
  assignees: BoardCardAssignee[]
}

export type BoardCardAssignee = {
  userId: string
  name: string | null
  email: string | null
}

export type BoardCardInput = {
  title: string
  description?: string
  priority?: BoardCardPriority | ""
  dueDate?: string | null
  labels?: string[]
  assigneeIds?: string[]
}
