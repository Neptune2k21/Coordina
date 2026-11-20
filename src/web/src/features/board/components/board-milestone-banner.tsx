import { FlagPennant, WarningCircle } from "@phosphor-icons/react"
import { useState } from "react"

import { Button } from "@/components/ui/button"
import { shortDate } from "@/features/board/model/board-format"
import {
  describeDriver,
  describeSlip,
  describeUnsized,
  milestoneStatusClass,
  milestoneStatusLabels,
  orderMilestones,
} from "@/features/board/model/milestones"
import type { BoardMilestone } from "@/types/task"

const collapsedCount = 3

type BoardMilestoneBannerProps = {
  milestones: BoardMilestone[]
  onSelectCard: (cardId: string) => void
}

export function BoardMilestoneBanner({
  milestones,
  onSelectCard,
}: BoardMilestoneBannerProps) {
  const [isExpanded, setIsExpanded] = useState(false)

  if (milestones.length === 0) {
    return null
  }

  const ordered = orderMilestones(milestones)
  const visible = isExpanded ? ordered : ordered.slice(0, collapsedCount)

  return (
    <section aria-label="Milestones" className="grid gap-1">
      {visible.map((milestone) => (
        <MilestoneRow
          key={milestone.cardId}
          milestone={milestone}
          onSelectCard={onSelectCard}
        />
      ))}
      {ordered.length > collapsedCount ? (
        <Button
          type="button"
          variant="ghost"
          size="sm"
          className="h-6 justify-self-start rounded-md px-2 text-[11px] text-muted-foreground"
          onClick={() => setIsExpanded((current) => !current)}
        >
          {isExpanded
            ? "Show fewer milestones"
            : `Show all ${ordered.length} milestones`}
        </Button>
      ) : null}
    </section>
  )
}

type MilestoneRowProps = {
  milestone: BoardMilestone
  onSelectCard: (cardId: string) => void
}

function MilestoneRow({ milestone, onSelectCard }: MilestoneRowProps) {
  const slip = describeSlip(milestone.slipDays ?? null)
  const unsized = describeUnsized(milestone)

  return (
    <div className="flex flex-wrap items-center gap-x-2 gap-y-1 rounded-md border border-zinc-950/10 bg-zinc-50 px-2 py-1.5 text-xs dark:border-white/10 dark:bg-white/[0.04]">
      <FlagPennant
        className="size-3.5 shrink-0 text-muted-foreground"
        weight="fill"
      />
      <button
        type="button"
        className="truncate font-semibold hover:underline"
        onClick={() => onSelectCard(milestone.cardId)}
      >
        {milestone.title}
      </button>
      <span className="text-muted-foreground">
        target {shortDate(milestone.targetDate)}
      </span>
      <span
        className={`rounded-sm px-1.5 py-0.5 text-[10px] font-semibold ${milestoneStatusClass(milestone.status)}`}
      >
        {milestoneStatusLabels[milestone.status]}
      </span>
      {milestone.forecastDate ? (
        <span>
          Forecast{" "}
          <span className="font-semibold">
            {shortDate(milestone.forecastDate)}
          </span>
          {milestone.commitDate ? (
            <span className="text-muted-foreground">
              {" "}
              (85%: {shortDate(milestone.commitDate)})
            </span>
          ) : null}
        </span>
      ) : null}
      {slip ? <span className="text-muted-foreground">{slip}</span> : null}
      {milestone.drivers.length > 0 ? (
        <span className="flex flex-wrap items-center gap-1">
          <span className="text-muted-foreground">Drivers:</span>
          {milestone.drivers.map((driver) => (
            <button
              key={driver.cardId}
              type="button"
              title={describeDriver(driver)}
              className="rounded-sm bg-zinc-950/5 px-1.5 py-0.5 font-medium hover:bg-zinc-950/10 dark:bg-white/10 dark:hover:bg-white/15"
              onClick={() => onSelectCard(driver.cardId)}
            >
              {driver.title}
            </button>
          ))}
        </span>
      ) : null}
      {unsized ? (
        <span className="inline-flex items-center gap-1 text-amber-700 dark:text-amber-200">
          <WarningCircle className="size-3.5" weight="fill" />
          {unsized}
        </span>
      ) : null}
    </div>
  )
}
