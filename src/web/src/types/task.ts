import type { components } from "@/types/api-contract"

// Shapes the API returns come from the generated contract (make contract), so
// a change on the API side breaks the web build until the web app follows.
type Schemas = components["schemas"]

export type Board = Schemas["BoardResponse"]
export type BoardList = Schemas["BoardListResponse"]
export type BoardCard = Schemas["BoardCardResponse"]
export type BoardCardUser = Schemas["BoardCardUserResponse"]
export type BoardCardAssignee = Schemas["BoardCardAssigneeResponse"]
export type BoardCardComment = Schemas["BoardCardCommentResponse"]
export type BoardCardSubtask = Schemas["BoardCardSubtaskResponse"]
export type BoardCardDependency = Schemas["BoardCardDependencyResponse"]
export type BoardGraph = Schemas["BoardGraphResponse"]
export type BoardGraphCard = Schemas["BoardGraphCardResponse"]
export type BoardGraphPlanItem = Schemas["BoardGraphPlanItemResponse"]
export type BoardCardDependencyAnalysis =
  Schemas["BoardCardDependencyAnalysisResponse"]
export type BoardTemplateDefinition = Schemas["BoardTemplateResponse"]
export type BoardTemplateList = Schemas["BoardTemplateListResponse"]
export type BoardTemplateCard = Schemas["BoardTemplateCardResponse"]

export type BoardTemplate = Board["template"]
export type BoardCardPriority = NonNullable<BoardCard["priority"]>
export type BoardTaskStatus = BoardList["taskStatus"]

// What the web app sends. These are form shapes, so they stay hand-written.
export type BoardCreationInput = {
  template: BoardTemplate
  customListTitles?: string[]
  includeExampleCards?: boolean
}

export type BoardCardInput = {
  title: string
  description?: string
  priority?: BoardCardPriority | ""
  dueDate?: string | null
  estimatedMinutes?: number | null
  labels?: string[]
  assigneeIds?: string[]
  isCompleted?: boolean
}
