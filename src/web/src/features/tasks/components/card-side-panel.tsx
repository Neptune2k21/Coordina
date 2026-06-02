import {
  CalendarBlank,
  CheckCircle,
  CircleNotch,
  CopySimple,
  Plus,
  Tag,
  Trash,
  X,
} from "@phosphor-icons/react"
import { useEffect, useMemo, useState } from "react"

import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Calendar } from "@/components/ui/calendar"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"
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
  BoardCardSubtask,
} from "@/types/task"
import type { WorkspaceMember } from "@/types/workspace"

type CardSidePanelProps = {
  availableLabels: string[]
  canDelete: boolean
  card: BoardCard | null
  dependencyCandidates: BoardCard[]
  isReadOnly: boolean
  isSaving: boolean
  members: WorkspaceMember[]
  onAddComment: (card: BoardCard, body: string) => Promise<void>
  onAddDependency: (card: BoardCard, dependsOnCardId: string) => Promise<void>
  onClose: () => void
  onCreateSubtask: (card: BoardCard, title: string) => Promise<void>
  onDelete: (card: BoardCard) => void
  onDeleteDependency: (
    card: BoardCard,
    dependsOnCardId: string
  ) => Promise<void>
  onDeleteSubtask: (card: BoardCard, subtask: BoardCardSubtask) => Promise<void>
  onDuplicate: (card: BoardCard) => void
  onSave: (card: BoardCard, input: BoardCardInput) => Promise<void>
  onUpdateSubtask: (
    card: BoardCard,
    subtask: BoardCardSubtask,
    input: { title?: string; isCompleted?: boolean }
  ) => Promise<void>
}

export function CardSidePanel({
  availableLabels,
  canDelete,
  card,
  dependencyCandidates,
  isReadOnly,
  isSaving,
  members,
  onAddComment,
  onAddDependency,
  onClose,
  onCreateSubtask,
  onDelete,
  onDeleteDependency,
  onDeleteSubtask,
  onDuplicate,
  onSave,
  onUpdateSubtask,
}: CardSidePanelProps) {
  const [title, setTitle] = useState("")
  const [description, setDescription] = useState("")
  const [priority, setPriority] = useState<BoardCardPriority | "">("")
  const [dueDate, setDueDate] = useState("")
  const [labels, setLabels] = useState<string[]>([])
  const [labelInput, setLabelInput] = useState("")
  const [assigneeIds, setAssigneeIds] = useState<string[]>([])
  const [isCompleted, setIsCompleted] = useState(false)
  const [commentInput, setCommentInput] = useState("")
  const [subtaskInput, setSubtaskInput] = useState("")
  const [dependencyId, setDependencyId] = useState("")
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
      isCompleted !== card.isCompleted ||
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
    isCompleted,
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
  const completedSubtasks =
    card?.subtasks.filter((item) => item.isCompleted).length ?? 0
  const subtaskProgress =
    card && card.subtasks.length > 0
      ? Math.round(((completedSubtasks ?? 0) / card.subtasks.length) * 100)
      : 0
  const existingDependencyIds = new Set(
    card?.dependencies.map((dependency) => dependency.cardId) ?? []
  )
  const availableDependencies = dependencyCandidates.filter(
    (candidate) => !existingDependencyIds.has(candidate.id)
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
      setIsCompleted(card?.isCompleted ?? false)
      setCommentInput("")
      setSubtaskInput("")
      setDependencyId("")
      setIsCalendarOpen(false)
      setIsLabelMenuOpen(false)
      setSaveState("idle")
      setError(null)
    })
  }, [card])

  if (!card) {
    return null
  }

  async function submit(nextCompleted = isCompleted) {
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
    setIsCompleted(nextCompleted)

    try {
      await onSave(currentCard, {
        title,
        description,
        priority,
        dueDate: dueDate || null,
        labels: submittedLabels,
        assigneeIds,
        isCompleted: nextCompleted,
      })
      setSaveState("saved")
      window.setTimeout(() => setSaveState("idle"), 1400)
    } catch {
      setSaveState("idle")
    }
  }

  async function addComment() {
    const currentCard = card

    if (isReadOnly || !currentCard || !commentInput.trim()) {
      return
    }

    setError(null)

    try {
      await onAddComment(currentCard, commentInput)
      setCommentInput("")
    } catch {
      setError("Comment could not be added.")
    }
  }

  async function addSubtask() {
    const currentCard = card

    if (isReadOnly || !currentCard || !subtaskInput.trim()) {
      return
    }

    setError(null)

    try {
      await onCreateSubtask(currentCard, subtaskInput)
      setSubtaskInput("")
    } catch {
      setError("Subtask could not be added.")
    }
  }

  async function addDependency() {
    const currentCard = card

    if (isReadOnly || !currentCard || !dependencyId) {
      return
    }

    setError(null)

    try {
      await onAddDependency(currentCard, dependencyId)
      setDependencyId("")
    } catch {
      setError("Dependency could not be added.")
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
    <Dialog open={card !== null} onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="grid h-[calc(100svh-2rem)] max-h-[calc(100svh-2rem)] max-w-5xl grid-rows-[auto_minmax(0,1fr)_auto] gap-0 overflow-hidden p-0">
        <DialogHeader className="min-w-0 border-b border-zinc-950/10 bg-zinc-50 px-5 py-4 dark:border-white/10 dark:bg-white/[0.035]">
          <div className="flex items-start justify-between gap-3">
            <div className="min-w-0">
              <DialogTitle className="truncate">{card.title}</DialogTitle>
              <DialogDescription>
                {isReadOnly
                  ? "Read only"
                  : hasChanges
                    ? "Unsaved changes"
                    : "Up to date"}
              </DialogDescription>
            </div>
            <Badge
              className={
                card.isCompleted
                  ? "border-teal-500/20 bg-teal-500/10 text-teal-700 dark:text-teal-200"
                  : "border-zinc-500/20 bg-zinc-500/10 text-zinc-700 dark:text-zinc-200"
              }
            >
              {card.isCompleted ? "COMPLETED" : "OPEN"}
            </Badge>
          </div>
        </DialogHeader>

        <div className="grid min-h-0 overflow-y-auto overscroll-contain lg:grid-cols-[minmax(0,1fr)_320px]">
          <div className="grid gap-4 p-5">
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
              <div className="grid gap-3 sm:grid-cols-2">
                <Field>
                  <FieldLabel>Priority</FieldLabel>
                  <div className="flex flex-wrap gap-1.5">
                    {boardCardPriorities.map((item) => (
                      <Button
                        key={item.value || "none"}
                        type="button"
                        variant={
                          priority === item.value ? "default" : "outline"
                        }
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
                        className="absolute right-0 z-20 mt-1 w-full max-w-[288px] shadow-xl"
                        onChange={(date) => {
                          setDueDate(date ?? "")
                          setIsCalendarOpen(false)
                        }}
                      />
                    ) : null}
                  </div>
                </Field>
              </div>
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
                    <div className="absolute top-10 right-0 left-0 z-30 rounded-md border border-zinc-950/10 bg-white p-1.5 shadow-xl dark:border-white/10 dark:bg-zinc-900">
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
                        {!isReadOnly ? (
                          <X className="ml-1 inline size-3" />
                        ) : null}
                      </button>
                    ))}
                  </div>
                ) : null}
              </Field>
              {error ? <FieldError>{error}</FieldError> : null}
            </FieldGroup>

            <section className="grid gap-2 rounded-md border border-zinc-950/10 bg-zinc-50 p-3 dark:border-white/10 dark:bg-white/[0.035]">
              <div className="flex items-center justify-between gap-2">
                <div>
                  <h3 className="text-sm font-semibold">Subtasks</h3>
                  <p className="text-xs text-muted-foreground">
                    {card.subtasks.length > 0
                      ? `${completedSubtasks}/${card.subtasks.length} done`
                      : "No subtasks"}
                  </p>
                </div>
                {card.subtasks.length > 0 ? (
                  <span className="rounded-sm bg-zinc-950/5 px-2 py-1 text-xs font-semibold dark:bg-white/10">
                    {subtaskProgress}%
                  </span>
                ) : null}
              </div>
              {card.subtasks.length > 0 ? (
                <div className="h-1.5 overflow-hidden rounded-full bg-zinc-200 dark:bg-zinc-800">
                  <div
                    className="h-full bg-teal-500"
                    style={{ width: `${subtaskProgress}%` }}
                  />
                </div>
              ) : null}
              <div className="grid gap-1.5">
                {card.subtasks.map((subtask) => (
                  <div
                    key={subtask.id}
                    className="flex items-center gap-2 rounded-md border border-zinc-950/10 bg-white p-2 dark:border-white/10 dark:bg-zinc-950"
                  >
                    <input
                      type="checkbox"
                      checked={subtask.isCompleted}
                      disabled={isReadOnly || isSaving}
                      className="size-4"
                      onChange={() =>
                        void onUpdateSubtask(card, subtask, {
                          isCompleted: !subtask.isCompleted,
                        })
                      }
                    />
                    <Input
                      defaultValue={subtask.title}
                      disabled={isReadOnly || isSaving}
                      className="h-7 flex-1 border-0 bg-transparent px-1 text-xs shadow-none focus-visible:ring-0"
                      onBlur={(event) => {
                        const next = event.currentTarget.value.trim()
                        if (next && next !== subtask.title) {
                          void onUpdateSubtask(card, subtask, { title: next })
                        }
                      }}
                    />
                    {subtask.completedBy ? (
                      <span className="hidden text-[10px] text-muted-foreground md:inline">
                        by {userLabel(subtask.completedBy)}
                      </span>
                    ) : null}
                    {!isReadOnly ? (
                      <Button
                        type="button"
                        variant="ghost"
                        size="icon"
                        className="size-7 rounded-md"
                        disabled={isSaving}
                        onClick={() => void onDeleteSubtask(card, subtask)}
                      >
                        <X className="size-3.5" />
                      </Button>
                    ) : null}
                  </div>
                ))}
              </div>
              {!isReadOnly ? (
                <div className="flex gap-2">
                  <Input
                    value={subtaskInput}
                    onChange={(event) => setSubtaskInput(event.target.value)}
                    placeholder="Add a subtask"
                    className="h-8 rounded-md bg-white text-xs dark:bg-white/[0.06]"
                    onKeyDown={(event) => {
                      if (event.key === "Enter") {
                        void addSubtask()
                      }
                    }}
                  />
                  <Button
                    type="button"
                    size="sm"
                    className="h-8 rounded-md px-2"
                    disabled={!subtaskInput.trim() || isSaving}
                    onClick={() => void addSubtask()}
                  >
                    <Plus className="size-3.5" />
                  </Button>
                </div>
              ) : null}
            </section>

            <section className="grid gap-2">
              <h3 className="text-sm font-semibold">Comments</h3>
              <div className="grid gap-2">
                {card.comments.length > 0 ? (
                  card.comments.map((comment) => (
                    <div
                      key={comment.id}
                      className="grid gap-1 rounded-md border border-zinc-950/10 bg-white p-3 text-sm dark:border-white/10 dark:bg-zinc-950"
                    >
                      <div className="flex items-center gap-2 text-xs text-muted-foreground">
                        <span className="grid size-6 place-items-center rounded-full bg-zinc-950 text-[10px] font-semibold text-white dark:bg-white dark:text-zinc-950">
                          {initials(
                            comment.author.name ?? comment.author.email
                          )}
                        </span>
                        <span className="font-medium text-zinc-800 dark:text-zinc-100">
                          {userLabel(comment.author)}
                        </span>
                        <span>{formatDateTime(comment.createdAt)}</span>
                      </div>
                      <p className="leading-6 whitespace-pre-wrap">
                        {comment.body}
                      </p>
                    </div>
                  ))
                ) : (
                  <p className="rounded-md border border-dashed border-zinc-950/10 px-3 py-4 text-sm text-muted-foreground dark:border-white/10">
                    No comments yet.
                  </p>
                )}
              </div>
              {!isReadOnly ? (
                <div className="grid gap-2">
                  <Textarea
                    value={commentInput}
                    onChange={(event) => setCommentInput(event.target.value)}
                    placeholder="Write a comment"
                    className="min-h-20 rounded-md bg-white text-sm dark:bg-white/[0.06]"
                  />
                  <Button
                    type="button"
                    className="h-8 justify-self-start rounded-md px-3 text-xs"
                    disabled={!commentInput.trim() || isSaving}
                    onClick={() => void addComment()}
                  >
                    <Plus className="size-3.5" />
                    Comment
                  </Button>
                </div>
              ) : null}
            </section>
          </div>

          <aside className="grid content-start gap-4 border-t border-zinc-950/10 bg-zinc-50 p-5 lg:border-t-0 lg:border-l dark:border-white/10 dark:bg-white/[0.035]">
            <section className="grid gap-2">
              <h3 className="text-sm font-semibold">Status</h3>
              <Button
                type="button"
                variant={isCompleted ? "outline" : "default"}
                className="h-9 justify-start rounded-md"
                disabled={isReadOnly || isSaving}
                onClick={() => void submit(!isCompleted)}
              >
                {isSaving ? (
                  <CircleNotch className="size-4 animate-spin" />
                ) : (
                  <CheckCircle className="size-4" weight="fill" />
                )}
                {isCompleted ? "Reopen card" : "Mark completed"}
              </Button>
              {card.completedBy ? (
                <p className="text-xs leading-5 text-muted-foreground">
                  Completed by {userLabel(card.completedBy)}
                  {card.completedAt
                    ? ` on ${formatDateTime(card.completedAt)}`
                    : ""}
                </p>
              ) : null}
            </section>

            <section className="grid gap-2">
              <h3 className="text-sm font-semibold">Assignees</h3>
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
                          ? "border-teal-500/30 bg-teal-500/10 dark:border-teal-300/35"
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
            </section>

            <section className="grid gap-2">
              <h3 className="text-sm font-semibold">Dependencies</h3>
              {card.dependencies.length > 0 ? (
                <div className="grid gap-1.5">
                  {card.dependencies.map((dependency) => (
                    <div
                      key={dependency.cardId}
                      className="flex items-center gap-2 rounded-md border border-zinc-950/10 bg-white p-2 text-xs dark:border-white/10 dark:bg-zinc-950"
                    >
                      <CheckCircle
                        className={`size-4 ${
                          dependency.isCompleted
                            ? "text-teal-600 dark:text-teal-300"
                            : "text-muted-foreground"
                        }`}
                        weight={dependency.isCompleted ? "fill" : "regular"}
                      />
                      <span className="min-w-0 flex-1 truncate">
                        {dependency.title}
                      </span>
                      {!isReadOnly ? (
                        <Button
                          type="button"
                          variant="ghost"
                          size="icon"
                          className="size-7 rounded-md"
                          disabled={isSaving}
                          onClick={() =>
                            void onDeleteDependency(card, dependency.cardId)
                          }
                        >
                          <X className="size-3.5" />
                        </Button>
                      ) : null}
                    </div>
                  ))}
                </div>
              ) : (
                <p className="text-xs leading-5 text-muted-foreground">
                  No dependency.
                </p>
              )}
              {!isReadOnly ? (
                <div className="flex gap-2">
                  <select
                    value={dependencyId}
                    disabled={availableDependencies.length === 0 || isSaving}
                    className="h-8 min-w-0 flex-1 rounded-md border border-zinc-950/10 bg-white px-2 text-xs dark:border-white/10 dark:bg-zinc-950"
                    onChange={(event) => setDependencyId(event.target.value)}
                  >
                    <option value="">Select card</option>
                    {availableDependencies.map((candidate) => (
                      <option key={candidate.id} value={candidate.id}>
                        {candidate.title}
                      </option>
                    ))}
                  </select>
                  <Button
                    type="button"
                    size="sm"
                    className="h-8 rounded-md px-2"
                    disabled={!dependencyId || isSaving}
                    onClick={() => void addDependency()}
                  >
                    <Plus className="size-3.5" />
                  </Button>
                </div>
              ) : null}
            </section>
          </aside>
        </div>

        <DialogFooter className="min-w-0 border-t border-zinc-950/10 bg-zinc-50 p-4 dark:border-white/10 dark:bg-white/[0.035]">
          <div className="mr-auto flex gap-2">
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
            ) : null}
            {!isReadOnly ? (
              <Button
                type="button"
                variant="outline"
                className="h-9 rounded-md px-3"
                disabled={isSaving}
                onClick={() => onDuplicate(card)}
              >
                <CopySimple className="size-4" />
                Duplicate
              </Button>
            ) : null}
          </div>
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
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

function userLabel(user: { name: string | null; email: string | null }) {
  return user.name ?? user.email ?? "Workspace member"
}

function formatDateTime(value: string) {
  return new Intl.DateTimeFormat(undefined, {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(new Date(value))
}
