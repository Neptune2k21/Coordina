import type { components } from "@/types/api-contract"

/** How the next-work ranking is computed. Each user saves theirs per board. */
export type PlanningPreferences =
  components["schemas"]["BoardPlanningPreferencesResponse"]

export const defaultPlanningPreferences: PlanningPreferences = {
  maxSize: null,
  context: null,
  preferAssigned: true,
  includeUnassigned: true,
}

export function planningQuery(preferences: PlanningPreferences) {
  const params = new URLSearchParams()

  if (preferences.maxSize) {
    params.set("maxSize", preferences.maxSize)
  }

  if (preferences.context) {
    params.set("context", preferences.context)
  }

  // Both default to true on the API, so only a false value needs sending.
  if (!preferences.preferAssigned) {
    params.set("preferAssigned", "false")
  }

  if (!preferences.includeUnassigned) {
    params.set("includeUnassigned", "false")
  }

  const query = params.toString()
  return query ? `?${query}` : ""
}
