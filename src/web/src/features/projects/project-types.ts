export type Project = {
  id: string
  name: string
  description: string | null
  key: string | null
  icon: string | null
  color: string | null
  workspaceId: string
  projectOwnerId: string
  projectOwnerName: string | null
  status: "ACTIVE" | "ARCHIVED" | "COMPLETED"
  createdAt: string
  updatedAt: string
  archivedAt: string | null
}

export type ProjectInput = {
  name: string
  description: string
  key?: string
  icon?: string
  color?: string
  projectOwnerId?: string
}

export type ProjectUpdateInput = Partial<ProjectInput> & {
  status?: Project["status"]
}
