import type {
  BoardCardPriority,
  BoardTemplate,
} from "@/features/tasks/task-types"

export const boardTemplates: Array<{
  id: BoardTemplate
  name: string
  lists: string
  summary: string
}> = [
  {
    id: "BASIC",
    name: "Basic",
    lists: "To Do, In Progress, Done",
    summary: "Simple workflow for small delivery tracks.",
  },
  {
    id: "AGILE_SCRUM",
    name: "Agile Scrum",
    lists: "Backlog, Sprint, In Progress, Review, Done",
    summary: "Sprint workflow with backlog and review stages.",
  },
  {
    id: "BUG_TRACKING",
    name: "Bug Tracking",
    lists: "Reported, Investigating, Fixed, Released",
    summary: "Triage flow for defects and release validation.",
  },
  {
    id: "PRODUCT_ROADMAP",
    name: "Product Roadmap",
    lists: "Ideas, Planned, In Progress, Shipped",
    summary: "Roadmap pipeline from discovery to launch.",
  },
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
