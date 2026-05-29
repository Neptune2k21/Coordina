import type { DocsIcon } from "@/components/docs/docs-types"

type DocLinkProps = {
  description: string
  href: string
  icon: DocsIcon
  title: string
}

export function DocLink({
  icon: Icon,
  title,
  description,
  href,
}: DocLinkProps) {
  return (
    <a
      href={href}
      className="group rounded-md border border-zinc-950/10 p-4 transition-colors hover:bg-zinc-50 dark:border-white/10 dark:hover:bg-white/[0.06]"
    >
      <Icon className="size-5 text-teal-600 dark:text-teal-300" weight="bold" />
      <p className="mt-4 text-sm font-semibold">{title}</p>
      <p className="mt-1 text-xs leading-5 break-all text-muted-foreground">
        {description}
      </p>
    </a>
  )
}
