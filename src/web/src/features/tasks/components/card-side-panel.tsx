import {
  CalendarBlank,
  CheckCircle,
  CircleNotch,
  Plus,
  Tag,
  Trash,
  X,
} from "@phosphor-icons/react"
import { useEffect, useMemo, useState } from "react"

import { Button } from "@/components/ui/button"
import { Calendar } from "@/components/ui/calendar"
import {
  Field,
  FieldError,
  FieldGroup,
  FieldLabel,
} from "@/components/ui/field"
import { Input } from "@/components/ui/input"
import { Textarea } from "@/components/ui/textarea"
import { boardCardPriorities } from "@/features/tasks/board-constants"
import {
  initials,
  normalizeLabel,
  normalizeLabels,
  panelLabelClass,
  sameLabels,
  sameStrings,
  shortDate,
  todayDateKey,
} from "@/features/tasks/board-utils"
import type {
  BoardCard,
  BoardCardInput,
  BoardCardPriority,
} from "@/features/tasks/task-types"
import type { WorkspaceMember } from "@/features/workspaces/workspace-types"

type CardSidePanelProps = {
  availableLabels: string[]
  canDelete: boolean
  card: BoardCard | null
  isReadOnly: boolean
  isSaving: boolean
  members: WorkspaceMember[]
  onClose: () => void
  onDelete: (card: BoardCard) => void
  onSave: (card: BoardCard, input: BoardCardInput) => Promise<void>
}

export function CardSidePanel({
  availableLabels,
  canDelete,
  card,
  isReadOnly,
  isSaving,
  members,
  onClose,
  onDelete,
  onSave,
}: CardSidePanelProps) {
  const [title, setTitle] = useState("")
  const [description, setDescription] = useState("")
  const [priority, setPriority] = useState<BoardCardPriority | "">("")
  const [dueDate, setDueDate] = useState("")
  const [labels, setLabels] = useState<string[]>([])
  const [labelInput, setLabelInput] = useState("")
  const [assigneeIds, setAssigneeIds] = useState<string[]>([])
  const [error, setError] = useState<string | null>(null)
  const [isCalendarOpen, setIsCalendarOpen] = useState(false)
  const [isLabelMenuOpen, setIsLabelMenuOpen] = useState(false)
  const [saveState, setSaveState] = useState<"idle" | "saving" | "saved">(
    "idle"
  )
  const hasChanges = useMemo(() => {
    if (!card) {
      return false
    }

    const nextLabels = normalizeLabels([...labels, labelInput])
    const currentLabels = normalizeLabels(card.labels)

    return (
      title.trim() !== card.title ||
      description.trim() !== (card.description ?? "") ||
      priority !== (card.priority ?? "") ||
      dueDate !== (card.dueDate ?? "") ||
      !sameLabels(nextLabels, currentLabels) ||
      !sameStrings(
        [...assigneeIds].sort(),
        card.assignees.map((assignee) => assignee.userId).sort()
      )
    )
  }, [
    assigneeIds,
    card,
    description,
    dueDate,
    labelInput,
    labels,
    priority,
    title,
  ])

  const labelSuggestions = useMemo(() => {
    const query = normalizeLabel(labelInput)?.toLowerCase() ?? ""

    return availableLabels
      .filter(
        (label) =>
          !labels.some(
            (selectedLabel) =>
              selectedLabel.toLowerCase() === label.toLowerCase()
          )
      )
      .filter((label) => !query || label.toLowerCase().includes(query))
      .slice(0, 10)
  }, [availableLabels, labelInput, labels])
  const normalizedLabelInput = normalizeLabel(labelInput)
  const canCreateLabel = Boolean(
    normalizedLabelInput &&
    !availableLabels.some(
      (label) => label.toLowerCase() === normalizedLabelInput.toLowerCase()
    ) &&
    !labels.some(
      (label) => label.toLowerCase() === normalizedLabelInput.toLowerCase()
    )
  )

  useEffect(() => {
    queueMicrotask(() => {
      setTitle(card?.title ?? "")
      setDescription(card?.description ?? "")
      setPriority(card?.priority ?? "")
      setDueDate(card?.dueDate ?? "")
      setLabels(normalizeLabels(card?.labels ?? []))
      setLabelInput("")
      setAssigneeIds(card?.assignees.map((assignee) => assignee.userId) ?? [])
      setIsCalendarOpen(false)
      setIsLabelMenuOpen(false)
      setSaveState("idle")
      setError(null)
    })
  }, [card])

  if (!card) {
    return null
  }

  async function submit() {
    const currentCard = card

    if (isReadOnly || !currentCard) {
      return
    }

    if (!title.trim()) {
      setError("Card title is required.")
      return
    }

    if (dueDate && dueDate < todayDateKey()) {
      setError("Due date cannot be in the past.")
      return
    }

    const submittedLabels = normalizeLabels([...labels, labelInput]).slice(0, 8)
    setLabels(submittedLabels)
    setLabelInput("")
    setError(null)
    setSaveState("saving")

    try {
      await onSave(currentCard, {
        title,
        description,
        priority,
        dueDate: dueDate || null,
        labels: submittedLabels,
        assigneeIds,
      })
      setSaveState("saved")
      window.setTimeout(() => setSaveState("idle"), 1400)
    } catch {
      setSaveState("idle")
    }
  }

  function addLabel() {
    if (isReadOnly) {
      return
    }

    const next = normalizeLabel(labelInput)

    if (!next) {
      return
    }

    setLabels((current) => normalizeLabels([...current, next]).slice(0, 8))
    setLabelInput("")
    setIsLabelMenuOpen(false)
  }

  function toggleLabel(label: string) {
    if (isReadOnly) {
      return
    }

    setLabels((current) => {
      const exists = current.some(
        (candidate) => candidate.toLowerCase() === label.toLowerCase()
      )

      if (exists) {
        return current.filter(
          (candidate) => candidate.toLowerCase() !== label.toLowerCase()
        )
      }

      return normalizeLabels([...current, label]).slice(0, 8)
    })
    setLabelInput("")
    setIsLabelMenuOpen(false)
  }

  return (
    <aside className="fixed top-0 right-0 z-50 flex h-svh w-full max-w-[420px] flex-col border-l border-zinc-950/10 bg-white shadow-2xl dark:border-white/10 dark:bg-zinc-950">
      <div className="flex h-12 shrink-0 items-center justify-between gap-2 border-b border-zinc-950/10 px-3 dark:border-white/10">
        <div className="min-w-0">
          <p className="truncate text-sm font-semibold">{card.title}</p>
          <p className="text-[11px] text-muted-foreground">
            {isReadOnly
              ? "Read only"
              : hasChanges
                ? "Unsaved changes"
                : "Up to date"}
          </p>
        </div>
        <Button
          type="button"
          variant="ghost"
          size="icon"
          className="size-8 rounded-md"
          onClick={onClose}
        >
          <X className="size-4" />
        </Button>
      </div>
      <div className="min-h-0 flex-1 overflow-y-auto p-3">
        <FieldGroup>
          <Field>
            <FieldLabel htmlFor="card-title">Title</FieldLabel>
            <Input
              id="card-title"
              value={title}
              disabled={isReadOnly}
              onChange={(event) => setTitle(event.target.value)}
              className="h-9 rounded-md bg-white text-sm dark:bg-white/[0.06]"
            />
          </Field>
          <Field>
            <FieldLabel htmlFor="card-description">Description</FieldLabel>
            <Textarea
              id="card-description"
              value={description}
              disabled={isReadOnly}
              onChange={(event) => setDescription(event.target.value)}
              className="min-h-28 rounded-md bg-white text-sm dark:bg-white/[0.06]"
            />
          </Field>
          <Field>
            <FieldLabel>Priority</FieldLabel>
            <div className="flex flex-wrap gap-1.5">
              {boardCardPriorities.map((item) => (
                <Button
                  key={item.value || "none"}
                  type="button"
                  variant={priority === item.value ? "default" : "outline"}
                  size="sm"
                  className="h-8 rounded-md px-2 text-xs"
                  disabled={isReadOnly}
                  onClick={() => setPriority(item.value)}
                >
                  {item.label}
                </Button>
              ))}
            </div>
          </Field>
          <Field>
            <div className="flex items-center justify-between gap-2">
              <FieldLabel>Due date</FieldLabel>
              {dueDate && !isReadOnly ? (
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  className="h-7 rounded-md px-2 text-xs"
                  onClick={() => setDueDate("")}
                >
                  Clear
                </Button>
              ) : null}
            </div>
            <div className="relative">
              <Button
                type="button"
                variant="outline"
                className="h-9 w-full justify-start rounded-md bg-white px-2 text-sm dark:bg-white/[0.06]"
                disabled={isReadOnly}
                onClick={() => setIsCalendarOpen((current) => !current)}
              >
                <CalendarBlank className="size-4" />
                {dueDate ? shortDate(dueDate) : "Pick a due date"}
              </Button>
              {isCalendarOpen ? (
                <Calendar
                  value={dueDate}
                  minValue={todayDateKey()}
                  className="absolute right-0 z-20 mt-1 w-[260px] shadow-xl"
                  onChange={(date) => {
                    setDueDate(date ?? "")
                    setIsCalendarOpen(false)
                  }}
                />
              ) : null}
            </div>
          </Field>
          <Field>
            <FieldLabel htmlFor="card-labels">Labels</FieldLabel>
            <div className="relative flex gap-2">
              <Input
                id="card-labels"
                value={labelInput}
                disabled={isReadOnly}
                onChange={(event) => setLabelInput(event.target.value)}
                onFocus={() => setIsLabelMenuOpen(true)}
                placeholder="frontend"
                className="h-9 rounded-md bg-white text-sm dark:bg-white/[0.06]"
                onKeyDown={(event) => {
                  if (event.key === "Enter" || event.key === ",") {
                    event.preventDefault()
                    addLabel()
                  }
                }}
              />
              <Button
                type="button"
                variant="outline"
                size="icon"
                className="size-9 rounded-md"
                aria-label="Show existing labels"
                disabled={isReadOnly}
                onClick={() => setIsLabelMenuOpen((current) => !current)}
              >
                <Tag className="size-4" />
              </Button>
              {isLabelMenuOpen && !isReadOnly ? (
                <div className="absolute top-10 right-0 left-0 z-30 rounded-md border border-zinc-950/10 bg-white p-1.5 shadow-xl dark:border-white/10 dark:bg-zinc-950">
                  {labelSuggestions.length > 0 ? (
                    <div className="grid gap-1">
                      {labelSuggestions.map((label) => (
                        <button
                          key={label}
                          type="button"
                          className="flex h-8 items-center justify-between rounded-sm px-2 text-left text-xs hover:bg-zinc-950/5 dark:hover:bg-white/10"
                          onMouseDown={(event) => event.preventDefault()}
                          onClick={() => toggleLabel(label)}
                        >
                          <span
                            className={`rounded-sm px-2 py-0.5 font-semibold ${panelLabelClass(label)}`}
                          >
                            {label}
                          </span>
                          <Plus className="size-3.5 text-muted-foreground" />
                        </button>
                      ))}
                    </div>
                  ) : null}
                  {canCreateLabel ? (
                    <button
                      type="button"
                      className="mt-1 flex h-8 w-full items-center gap-2 rounded-sm px-2 text-left text-xs hover:bg-zinc-950/5 dark:hover:bg-white/10"
                      onMouseDown={(event) => event.preventDefault()}
                      onClick={addLabel}
                    >
                      <Plus className="size-3.5" />
                      Create "{normalizedLabelInput}"
                    </button>
                  ) : labelSuggestions.length === 0 ? (
                    <div className="px-2 py-2 text-xs text-muted-foreground">
                      No labels yet
                    </div>
                  ) : null}
                </div>
              ) : null}
            </div>
            {labels.length > 0 ? (
              <div className="flex flex-wrap gap-1.5">
                {labels.map((label) => (
                  <button
                    key={label}
                    type="button"
                    className={`rounded-sm px-2 py-1 text-[11px] font-semibold disabled:pointer-events-none ${panelLabelClass(label)}`}
                    disabled={isReadOnly}
                    onClick={() =>
                      setLabels((current) =>
                        current.filter((candidate) => candidate !== label)
                      )
                    }
                  >
                    {label}
                    {!isReadOnly ? <X className="ml-1 inline size-3" /> : null}
                  </button>
                ))}
              </div>
            ) : null}
          </Field>
          <Field>
            <FieldLabel>Assignees</FieldLabel>
            <div className="grid gap-1">
              {members.map((member) => {
                const active = assigneeIds.includes(member.userId)

                return (
                  <button
                    key={member.userId}
                    type="button"
                    disabled={isReadOnly}
                    className={`flex h-9 items-center gap-2 rounded-md border px-2 text-left text-xs disabled:pointer-events-none disabled:opacity-80 ${
                      active
                        ? "border-teal-500/30 bg-teal-500/10"
                        : "border-zinc-950/10 bg-white dark:border-white/10 dark:bg-white/[0.04]"
                    }`}
                    onClick={() =>
                      setAssigneeIds((current) =>
                        active
                          ? current.filter((id) => id !== member.userId)
                          : [...current, member.userId]
                      )
                    }
                  >
                    <span className="grid size-6 place-items-center rounded-full bg-zinc-950 text-[10px] font-semibold text-white dark:bg-white dark:text-zinc-950">
                      {initials(member.name ?? member.email)}
                    </span>
                    <span className="min-w-0 flex-1 truncate">
                      {member.name ?? member.email ?? "Workspace member"}
                    </span>
                  </button>
                )
              })}
            </div>
          </Field>
          {error ? <FieldError>{error}</FieldError> : null}
        </FieldGroup>
      </div>
      <div className="flex shrink-0 justify-between gap-2 border-t border-zinc-950/10 p-3 dark:border-white/10">
        {canDelete && !isReadOnly ? (
          <Button
            type="button"
            variant="destructive"
            className="h-9 rounded-md px-3"
            disabled={isSaving}
            onClick={() => onDelete(card)}
          >
            <Trash className="size-4" />
            Delete
          </Button>
        ) : (
          <span />
        )}
        <Button
          type="button"
          className="h-9 rounded-md px-4"
          disabled={
            isReadOnly || isSaving || saveState === "saving" || !hasChanges
          }
          onClick={() => void submit()}
        >
          {saveState === "saving" || isSaving ? (
            <CircleNotch className="size-4 animate-spin" />
          ) : saveState === "saved" ? (
            <CheckCircle className="size-4" weight="fill" />
          ) : null}
          {saveState === "saved" ? "Saved" : "Save"}
        </Button>
      </div>
    </aside>
  )
}
