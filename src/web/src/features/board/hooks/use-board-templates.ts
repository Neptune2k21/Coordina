import { useCallback, useEffect, useState } from "react"

import { useAuth } from "@/features/auth/auth-context"
import * as boardApi from "@/features/board/api/board-api"
import type { BoardTemplateDefinition } from "@/types/task"

type BoardTemplatesState = {
  status: "loading" | "ready" | "error"
  templates: BoardTemplateDefinition[]
}

export function useBoardTemplates() {
  const { session } = useAuth()
  const accessToken = session?.accessToken ?? null
  const [attempt, setAttempt] = useState(0)
  const [state, setState] = useState<BoardTemplatesState>({
    status: "loading",
    templates: [],
  })

  useEffect(() => {
    if (!accessToken) {
      return
    }

    let isCurrent = true

    boardApi
      .getBoardTemplates(accessToken)
      .then((templates) => {
        if (isCurrent) {
          setState({ status: "ready", templates })
        }
      })
      .catch(() => {
        if (isCurrent) {
          setState({ status: "error", templates: [] })
        }
      })

    return () => {
      isCurrent = false
    }
  }, [accessToken, attempt])

  const retry = useCallback(() => {
    setState({ status: "loading", templates: [] })
    setAttempt((current) => current + 1)
  }, [])

  return { ...state, retry }
}
