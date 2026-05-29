import type { ComponentType } from "react"

export type DocsIcon = ComponentType<{
  className?: string
  weight?: "regular" | "bold" | "fill"
}>

export type DocsNavItem = {
  id: string
  label: string
}
