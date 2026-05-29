import * as React from "react"

import { cn } from "@/lib/utils"

function Badge({ className, ...props }: React.ComponentProps<"span">) {
  return (
    <span
      data-slot="badge"
      className={cn(
        "inline-flex w-fit shrink-0 items-center gap-1 rounded-full border border-transparent bg-secondary px-2.5 py-1 text-[11px] font-semibold whitespace-nowrap text-secondary-foreground",
        className
      )}
      {...props}
    />
  )
}

export { Badge }
