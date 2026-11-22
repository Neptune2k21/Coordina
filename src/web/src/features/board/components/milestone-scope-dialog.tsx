import { useEffect, useRef, useState } from "react"

import { Button } from "@/components/ui/button"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"
import type { MilestoneScopeActions } from "@/features/board/hooks/use-milestone-scope"
import { shortDate } from "@/features/board/model/board-format"
import {
  describeAlsoRemoved,
  describeChance,
} from "@/features/board/model/milestone-scope"
import { ApiError } from "@/lib/api"
import type {
  BoardMilestone,
  BoardMilestoneCut,
  BoardMilestoneScope,
} from "@/types/task"

type MilestoneScopeDialogProps = {
  milestone: BoardMilestone
  actions: MilestoneScopeActions
  isReadOnly: boolean
  onClose: () => void
}

function describeTakeOut(count: number) {
  if (count === 0) {
    return "Choose cards to take out"
  }

  return `Take out ${count} ${count === 1 ? "card" : "cards"}`
}

function messageOf(error: unknown) {
  return error instanceof ApiError
    ? error.message
    : "Coordina could not measure this cut."
}

export function MilestoneScopeDialog({
  milestone,
  actions,
  isReadOnly,
  onClose,
}: MilestoneScopeDialogProps) {
  const [proposal, setProposal] = useState<BoardMilestoneScope | null>(null)
  const [measure, setMeasure] = useState<BoardMilestoneScope | null>(null)
  const [chosen, setChosen] = useState<string[]>([])
  const [error, setError] = useState<string | null>(null)
  const [isMeasuring, setIsMeasuring] = useState(false)
  const [isApplying, setIsApplying] = useState(false)
  // Only the measure of the latest choice is shown.
  const latestMeasure = useRef(0)

  useEffect(() => {
    let isCurrent = true

    actions
      .propose(milestone.cardId)
      .then((proposed) => {
        if (isCurrent) {
          setProposal(proposed)
          setMeasure(proposed)
          setChosen(proposed.cuts.map((cut) => cut.cardId))
        }
      })
      .catch((requestError: unknown) => {
        if (isCurrent) {
          setError(messageOf(requestError))
        }
      })

    return () => {
      isCurrent = false
    }
  }, [actions, milestone.cardId])

  const toggle = (cardId: string) => {
    const next = chosen.includes(cardId)
      ? chosen.filter((id) => id !== cardId)
      : [...chosen, cardId]
    const request = ++latestMeasure.current
    setChosen(next)
    setIsMeasuring(true)

    actions
      .evaluate(milestone.cardId, next)
      .then((measured) => {
        if (request === latestMeasure.current) {
          setMeasure(measured)
        }
      })
      .catch((requestError: unknown) => {
        if (request === latestMeasure.current) {
          setError(messageOf(requestError))
        }
      })
      .finally(() => {
        if (request === latestMeasure.current) {
          setIsMeasuring(false)
        }
      })
  }

  const apply = async () => {
    setIsApplying(true)

    try {
      await actions.cut(milestone.cardId, chosen)
      onClose()
    } catch (requestError) {
      setError(messageOf(requestError))
      setIsApplying(false)
    }
  }

  return (
    <Dialog open onOpenChange={(isOpen) => (isOpen ? undefined : onClose())}>
      <DialogContent className="max-w-[520px]">
        <DialogHeader>
          <DialogTitle>Cut the scope of {milestone.title}</DialogTitle>
          <DialogDescription>
            Target {shortDate(milestone.targetDate)}. Nothing is deleted: the
            milestone stops waiting for the cards you take out.
          </DialogDescription>
        </DialogHeader>
        {error ? (
          <p className="text-sm text-destructive" role="alert">
            {error}
          </p>
        ) : null}
        {proposal && measure ? (
          <ScopeSummary
            proposal={proposal}
            measure={measure}
            chosen={chosen}
            isDisabled={isApplying}
            onToggle={toggle}
          />
        ) : error ? null : (
          <p className="text-sm text-muted-foreground">
            Looking for what to cut…
          </p>
        )}
        <DialogFooter>
          <Button type="button" variant="outline" onClick={onClose}>
            {isReadOnly ? "Close" : "Cancel"}
          </Button>
          {isReadOnly ? null : (
            <Button
              type="button"
              disabled={chosen.length === 0 || isMeasuring || isApplying}
              onClick={() => void apply()}
            >
              {describeTakeOut(chosen.length)}
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

type ScopeSummaryProps = {
  proposal: BoardMilestoneScope
  measure: BoardMilestoneScope
  chosen: string[]
  isDisabled: boolean
  onToggle: (cardId: string) => void
}

function ScopeSummary({
  proposal,
  measure,
  chosen,
  isDisabled,
  onToggle,
}: ScopeSummaryProps) {
  if (proposal.cuts.length === 0) {
    return (
      <p className="text-sm text-muted-foreground">
        Nothing is worth cutting: the cards that could go would not bring the
        date in, or the others are in progress.
      </p>
    )
  }

  // What leaves with a card depends on the other cards chosen.
  const current = (cut: BoardMilestoneCut) =>
    measure.cuts.find((measured) => measured.cardId === cut.cardId) ?? cut

  return (
    <div className="grid gap-3">
      <div className="grid gap-0.5">
        <p className="text-sm">
          Chance of shipping on time:{" "}
          <span className="font-semibold">
            {describeChance(measure.onTimeBefore)}
          </span>{" "}
          →{" "}
          <span className="font-semibold">
            {describeChance(measure.onTimeAfter)}
          </span>
        </p>
        <p className="text-xs text-muted-foreground">
          Forecast {shortDate(measure.forecastDate)} (85%:{" "}
          {shortDate(measure.commitDate)})
        </p>
      </div>
      <ul className="grid gap-2">
        {proposal.cuts.map((cut) => {
          const alsoRemoved = describeAlsoRemoved(current(cut))

          return (
            <li key={cut.cardId}>
              <label className="flex items-start gap-2 text-sm">
                <input
                  type="checkbox"
                  className="mt-1"
                  checked={chosen.includes(cut.cardId)}
                  disabled={isDisabled}
                  onChange={() => onToggle(cut.cardId)}
                />
                <span className="grid">
                  <span className="font-medium">{cut.title}</span>
                  {alsoRemoved ? (
                    <span className="text-xs text-muted-foreground">
                      {alsoRemoved}
                    </span>
                  ) : null}
                </span>
              </label>
            </li>
          )
        })}
      </ul>
    </div>
  )
}
