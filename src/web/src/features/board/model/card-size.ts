import type { CardSize } from "@/types/task"

export type { CardSize }

export const cardSizes: readonly CardSize[] = ["XS", "S", "M", "L", "XL", "XXL"]

/** What each size means, in words; the graph engine owns the time ranges. */
export const cardSizeHints: Record<CardSize, string> = {
  XS: "Under an hour",
  S: "A few hours",
  M: "About a day",
  L: "A few days",
  XL: "About a week",
  XXL: "More than a week: split it",
}

/** The focus slots the planning panel offers, as the biggest size that fits. */
export const focusSlotOptions: ReadonlyArray<{
  value: CardSize | null
  label: string
}> = [
  { value: null, label: "Any size" },
  { value: "S", label: "A quick task (XS–S)" },
  { value: "M", label: "Half a day (up to M)" },
]

export function focusSlotLabel(maxSize: CardSize | null) {
  return (
    focusSlotOptions.find((option) => option.value === maxSize)?.label ??
    `Up to ${maxSize}`
  )
}
