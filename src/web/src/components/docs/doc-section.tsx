import type { ReactNode } from "react"

import { cn } from "@/lib/utils"

type DocSectionProps = {
  children: ReactNode
  className?: string
  id: string
}

export function DocSection({ id, className, children }: DocSectionProps) {
  return (
    <section
      id={id}
      className={cn(
        "grid gap-5 border-b border-zinc-950/10 py-12 last:border-b-0 dark:border-white/10",
        className
      )}
    >
      {children}
    </section>
  )
}
