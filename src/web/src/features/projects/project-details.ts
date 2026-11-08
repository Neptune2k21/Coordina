import { ApiError } from "@/lib/api"
import type { Project } from "@/types/project"

/** The editable description of a project, as its forms hold it. */
export type ProjectDetails = {
  name: string
  description: string
  key: string
  icon: string
  color: string
}

export function detailsOf(project: Project): ProjectDetails {
  return {
    name: project.name,
    description: project.description ?? "",
    key: project.key ?? "",
    icon: project.icon ?? "",
    color: project.color ?? "",
  }
}

/** The first field error the API reports for a project form, or its message. */
export function projectErrorMessage(error: unknown, fallback: string) {
  if (!(error instanceof ApiError)) {
    return fallback
  }

  return (
    error.errors?.Name?.[0] ??
    error.errors?.Description?.[0] ??
    error.errors?.Key?.[0] ??
    error.errors?.Icon?.[0] ??
    error.errors?.Color?.[0] ??
    error.message
  )
}
