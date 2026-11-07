import type { components } from "@/types/api-contract"

export type Project = components["schemas"]["ProjectResponse"]
export type ProjectStatus = Project["status"]

export type ProjectInput = {
  name: string
  description: string
  key?: string
  icon?: string
  color?: string
  projectOwnerId?: string
}

export type ProjectUpdateInput = Partial<ProjectInput> & {
  status?: ProjectStatus
}
