import { apiRequest } from "@/lib/api"
import type { MyWork } from "@/types/work"

export async function getMyWork(accessToken: string, workspaceId: string) {
  return apiRequest<MyWork>(`/me/work?workspaceId=${workspaceId}`, {
    accessToken,
    errorMessage: "Your work could not be loaded.",
  })
}
