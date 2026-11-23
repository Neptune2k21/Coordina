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
import type { MilestoneStaffingActions } from "@/features/board/hooks/use-milestone-staffing"
import { shortDate, userLabel } from "@/features/board/model/board-format"
import {
  chosenChanges,
  describeChange,
  describeGain,
  describeReason,
} from "@/features/board/model/milestone-staffing"
import { ApiError } from "@/lib/api"
import type { BoardMilestone, BoardMilestoneStaffing } from "@/types/task"
import type { WorkspaceMember } from "@/types/workspace"

type MilestoneStaffingDialogProps = {
  milestone: BoardMilestone
  members: WorkspaceMember[]
  actions: MilestoneStaffingActions
  isReadOnly: boolean
  onClose: () => void
}

function describeApply(count: number) {
  if (count === 0) {
    return "Choose changes to apply"
  }

  return `Apply ${count} ${count === 1 ? "change" : "changes"}`
}

function messageOf(error: unknown) {
  return error instanceof ApiError
    ? error.message
    : "Coordina could not plan this milestone."
}

export function MilestoneStaffingDialog({
  milestone,
  members,
  actions,
  isReadOnly,
  onClose,
}: MilestoneStaffingDialogProps) {
  // Null plans with the board's team, as the first proposal names it.
  const [chosenPeople, setChosenPeople] = useState<string[] | null>(null)
  const [allowReassignment, setAllowReassignment] = useState(false)
  const [proposal, setProposal] = useState<BoardMilestoneStaffing | null>(null)
  const [measure, setMeasure] = useState<BoardMilestoneStaffing | null>(null)
  const [chosen, setChosen] = useState<string[]>([])
  const [error, setError] = useState<string | null>(null)
  const [answeredKey, setAnsweredKey] = useState<string | null>(null)
  const [isMeasuring, setIsMeasuring] = useState(false)
  const [isApplying, setIsApplying] = useState(false)
  // Only the answer to the latest request is shown.
  const latestRequest = useRef(0)
  const peopleKey = chosenPeople?.join(",") ?? ""
  const requestKey = `${milestone.cardId}|${peopleKey}|${allowReassignment}`
  const isProposing = answeredKey !== requestKey
  const people =
    chosenPeople ?? proposal?.people.map((person) => person.userId) ?? []

  useEffect(() => {
    let isCurrent = true
    const request = ++latestRequest.current
    const answer = (update: () => void) => {
      if (isCurrent && request === latestRequest.current) {
        update()
        setAnsweredKey(requestKey)
      }
    }

    actions
      .propose(milestone.cardId, {
        people: peopleKey ? peopleKey.split(",") : undefined,
        allowReassignment,
      })
      .then((proposed) =>
        answer(() => {
          setProposal(proposed)
          setMeasure(proposed)
          setChosen(proposed.changes.map((change) => change.cardId))
          setError(null)
        })
      )
      .catch((requestError: unknown) =>
        answer(() => setError(messageOf(requestError)))
      )

    return () => {
      isCurrent = false
    }
  }, [actions, allowReassignment, milestone.cardId, peopleKey, requestKey])

  const toggle = (cardId: string) => {
    if (!proposal) {
      return
    }

    const next = chosen.includes(cardId)
      ? chosen.filter((id) => id !== cardId)
      : [...chosen, cardId]
    const request = ++latestRequest.current
    setChosen(next)
    setIsMeasuring(true)

    actions
      .evaluate(milestone.cardId, chosenChanges(proposal, next))
      .then((measured) => {
        if (request === latestRequest.current) {
          setMeasure(measured)
        }
      })
      .catch((requestError: unknown) => {
        if (request === latestRequest.current) {
          setError(messageOf(requestError))
        }
      })
      .finally(() => {
        if (request === latestRequest.current) {
          setIsMeasuring(false)
        }
      })
  }

  const togglePerson = (userId: string) => {
    const next = people.includes(userId)
      ? people.filter((id) => id !== userId)
      : [...people, userId]

    if (next.length > 0) {
      setChosenPeople(next)
    }
  }

  const apply = async () => {
    if (!proposal) {
      return
    }

    setIsApplying(true)

    try {
      await actions.apply(milestone.cardId, chosenChanges(proposal, chosen))
      onClose()
    } catch (requestError) {
      setError(messageOf(requestError))
      setIsApplying(false)
    }
  }

  return (
    <Dialog open onOpenChange={(isOpen) => (isOpen ? undefined : onClose())}>
      <DialogContent className="max-w-[560px]">
        <DialogHeader>
          <DialogTitle>Optimize the plan for {milestone.title}</DialogTitle>
          <DialogDescription>
            Who should take which card so it ships sooner. Nothing changes until
            you apply.
          </DialogDescription>
        </DialogHeader>
        <PlanOptions
          members={members}
          people={people}
          allowReassignment={allowReassignment}
          isDisabled={isApplying || proposal === null}
          onTogglePerson={togglePerson}
          onAllowReassignment={setAllowReassignment}
        />
        {error ? (
          <p className="text-sm text-destructive" role="alert">
            {error}
          </p>
        ) : null}
        {proposal && measure && !isProposing ? (
          <StaffingSummary
            proposal={proposal}
            measure={measure}
            chosen={chosen}
            isDisabled={isApplying}
            onToggle={toggle}
          />
        ) : error ? null : (
          <p className="text-sm text-muted-foreground">
            Looking for a better plan…
          </p>
        )}
        <DialogFooter>
          <Button type="button" variant="outline" onClick={onClose}>
            {isReadOnly ? "Close" : "Cancel"}
          </Button>
          {isReadOnly ? null : (
            <Button
              type="button"
              disabled={
                chosen.length === 0 || isProposing || isMeasuring || isApplying
              }
              onClick={() => void apply()}
            >
              {describeApply(chosen.length)}
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

type PlanOptionsProps = {
  members: WorkspaceMember[]
  people: string[]
  allowReassignment: boolean
  isDisabled: boolean
  onTogglePerson: (userId: string) => void
  onAllowReassignment: (isAllowed: boolean) => void
}

function PlanOptions({
  members,
  people,
  allowReassignment,
  isDisabled,
  onTogglePerson,
  onAllowReassignment,
}: PlanOptionsProps) {
  return (
    <div className="grid gap-2">
      <div className="flex flex-wrap items-center gap-1 text-xs">
        <span className="text-muted-foreground">Plan with</span>
        {members.map((member) => {
          const isChosen = people.includes(member.userId)

          return (
            <button
              key={member.userId}
              type="button"
              aria-pressed={isChosen}
              disabled={isDisabled}
              className={`rounded-sm px-1.5 py-0.5 font-medium ${
                isChosen
                  ? "bg-zinc-900 text-white dark:bg-white dark:text-zinc-900"
                  : "bg-zinc-950/5 hover:bg-zinc-950/10 dark:bg-white/10 dark:hover:bg-white/15"
              }`}
              onClick={() => onTogglePerson(member.userId)}
            >
              {userLabel(member)}
            </button>
          )
        })}
      </div>
      <label className="flex items-center gap-2 text-xs">
        <input
          type="checkbox"
          checked={allowReassignment}
          disabled={isDisabled}
          onChange={(event) => onAllowReassignment(event.target.checked)}
        />
        Also move cards that already have someone
      </label>
    </div>
  )
}

type StaffingSummaryProps = {
  proposal: BoardMilestoneStaffing
  measure: BoardMilestoneStaffing
  chosen: string[]
  isDisabled: boolean
  onToggle: (cardId: string) => void
}

function StaffingSummary({
  proposal,
  measure,
  chosen,
  isDisabled,
  onToggle,
}: StaffingSummaryProps) {
  if (proposal.changes.length === 0) {
    return (
      <p className="text-sm text-muted-foreground">
        No better plan found: the cards that could move would not bring the date
        in. Try planning with more people, or let assigned cards move.
      </p>
    )
  }

  return (
    <div className="grid gap-3">
      <div className="grid gap-0.5">
        <p className="text-sm">
          <span className="font-semibold">{describeGain(measure)}</span> (85%:{" "}
          {shortDate(measure.commitDateBefore)} →{" "}
          {shortDate(measure.commitDateAfter)})
        </p>
        <p className="text-xs text-muted-foreground">
          Forecast {shortDate(measure.forecastDateBefore)} →{" "}
          {shortDate(measure.forecastDateAfter)}
        </p>
      </div>
      <ul className="grid gap-2">
        {proposal.changes.map((change) => (
          <li key={change.cardId}>
            <label className="flex items-start gap-2 text-sm">
              <input
                type="checkbox"
                className="mt-1"
                checked={chosen.includes(change.cardId)}
                disabled={isDisabled}
                onChange={() => onToggle(change.cardId)}
              />
              <span className="grid">
                <span className="font-medium">{describeChange(change)}</span>
                <span className="text-xs text-muted-foreground">
                  {describeReason(change)}
                </span>
              </span>
            </label>
          </li>
        ))}
      </ul>
    </div>
  )
}
