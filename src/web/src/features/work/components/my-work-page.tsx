import {
  CircleNotch,
  Clock,
  Lightning,
  LockSimple,
} from "@phosphor-icons/react"

import { Badge } from "@/components/ui/badge"
import { ProjectIconMark } from "@/features/projects"
import { useMyWork } from "@/features/work/use-my-work"
import { useWorkspaces } from "@/features/workspaces"
import type { MyWorkItem } from "@/types/work"

type MyWorkPageProps = {
  onOpenCard: (projectId: string, cardId: string) => void
}

/** The landing page: what to work on now, next, and what is waiting on others. */
export function MyWorkPage({ onOpenCard }: MyWorkPageProps) {
  const { activeWorkspace } = useWorkspaces()
  const { work, isLoading, error } = useMyWork()

  return (
    <div className="mx-auto grid w-full max-w-4xl gap-4">
      <header className="grid gap-1">
        <h1 className="text-2xl font-semibold text-zinc-950 dark:text-white">
          My work
        </h1>
        <p className="text-sm text-muted-foreground">
          Your cards across {activeWorkspace?.name ?? "the workspace"}, ranked
          like each board&apos;s Next work.
        </p>
      </header>

      {isLoading ? (
        <p className="flex items-center gap-2 text-sm text-muted-foreground">
          <CircleNotch className="size-4 animate-spin" />
          Ranking your work
        </p>
      ) : error ? (
        <p role="alert" className="text-sm text-destructive">
          {error}
        </p>
      ) : work ? (
        <>
          <WorkSection
            title="Now"
            icon={Clock}
            empty="Nothing in progress. Move a card you are assigned to into an In progress list."
            items={work.now}
            onOpenCard={onOpenCard}
          />
          <WorkSection
            title="Next"
            icon={Lightning}
            empty="Nothing ready for you. Cards assigned to you, or to nobody, show up here once their dependencies are done."
            items={work.next}
            more={work.nextTotal - work.next.length}
            onOpenCard={onOpenCard}
          />
          <WorkSection
            title="Blocked"
            icon={LockSimple}
            empty="None of your cards is waiting on another."
            items={work.blocked}
            onOpenCard={onOpenCard}
          />
        </>
      ) : null}
    </div>
  )
}

function WorkSection({
  empty,
  icon: Icon,
  items,
  more = 0,
  onOpenCard,
  title,
}: {
  empty: string
  icon: React.ComponentType<{ className?: string }>
  items: MyWorkItem[]
  more?: number
  onOpenCard: (projectId: string, cardId: string) => void
  title: string
}) {
  return (
    <section
      aria-label={title}
      className="grid gap-3 rounded-md border border-zinc-950/[0.08] bg-white/84 p-4 shadow-[0_12px_28px_rgba(24,24,27,0.06)] backdrop-blur-xl dark:border-white/10 dark:bg-white/[0.055]"
    >
      <h2 className="flex items-center gap-2 text-sm font-semibold">
        <Icon className="size-4 text-muted-foreground" />
        {title}
        <span className="text-xs font-normal text-muted-foreground">
          {items.length + more}
        </span>
      </h2>
      {items.length === 0 ? (
        <p className="text-sm text-muted-foreground">{empty}</p>
      ) : (
        <ol className="grid gap-2">
          {items.map((item) => (
            <li key={item.plan.card.id}>
              <WorkItem item={item} onOpenCard={onOpenCard} />
            </li>
          ))}
        </ol>
      )}
      {more > 0 ? (
        <p className="text-xs text-muted-foreground">
          {more} more ready across your boards.
        </p>
      ) : null}
    </section>
  )
}

function WorkItem({
  item,
  onOpenCard,
}: {
  item: MyWorkItem
  onOpenCard: (projectId: string, cardId: string) => void
}) {
  return (
    <button
      type="button"
      className="grid w-full gap-1.5 rounded-md border border-zinc-950/10 bg-white p-3 text-left transition-colors hover:bg-zinc-50 dark:border-white/10 dark:bg-white/[0.04] dark:hover:bg-white/[0.07]"
      onClick={() => onOpenCard(item.project.id, item.plan.card.id)}
    >
      <span className="flex items-center gap-2">
        <ProjectIconMark icon={item.project.icon} color={item.project.color} />
        <span className="min-w-0 flex-1 truncate text-sm font-medium">
          {item.plan.card.title}
        </span>
        {item.isUnassigned ? (
          <Badge className="h-5 rounded-sm px-1.5 text-[10px]">
            Unassigned
          </Badge>
        ) : null}
      </span>
      <span className="flex flex-wrap items-center gap-1.5 text-xs text-muted-foreground">
        <span className="font-medium">
          {item.project.key ?? item.project.name}
        </span>
        {item.plan.reasons.map((reason) => (
          <span
            key={reason}
            className="rounded-sm bg-zinc-950/5 px-1.5 py-0.5 dark:bg-white/10"
          >
            {reason}
          </span>
        ))}
      </span>
    </button>
  )
}
