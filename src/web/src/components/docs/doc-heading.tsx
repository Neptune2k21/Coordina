import type { DocsIcon } from "@/components/docs/docs-types"

type DocHeadingProps = {
  icon: DocsIcon
  title: string
}

export function DocHeading({ icon: Icon, title }: DocHeadingProps) {
  return (
    <div className="flex items-center gap-3">
      <span className="grid size-10 place-items-center rounded-md border border-zinc-950/10 bg-zinc-50 dark:border-white/10 dark:bg-white/[0.06]">
        <Icon
          className="size-5 text-teal-600 dark:text-teal-300"
          weight="bold"
        />
      </span>
      <h2 className="text-2xl font-semibold tracking-normal">{title}</h2>
    </div>
  )
}
