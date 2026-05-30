import type {
  Board,
  BoardCard,
  BoardCardPriority,
} from "@/features/tasks/task-types"

export function countBoardCards(board: Board | null) {
  return board?.lists.reduce((total, list) => total + list.cards.length, 0) ?? 0
}

export function getBoardLabels(board: Board | null) {
  return normalizeLabels(
    board?.lists.flatMap((list) => list.cards.flatMap((card) => card.labels)) ??
      []
  )
}

export function filterBoard(board: Board | null, search: string) {
  if (!board || !search.trim()) {
    return board
  }

  const query = search.trim().toLowerCase()

  return {
    ...board,
    lists: board.lists.map((list) => ({
      ...list,
      cards: list.cards.filter((card) =>
        [
          card.title,
          card.description ?? "",
          card.priority ?? "",
          card.labels.join(" "),
          card.assignees.map((assignee) => assignee.name).join(" "),
        ]
          .join(" ")
          .toLowerCase()
          .includes(query)
      ),
    })),
  }
}

export function findCard(board: Board, cardId: string) {
  return board.lists
    .flatMap((list) => list.cards)
    .find((card) => card.id === cardId)
}

export function moveCardInBoard(
  board: Board,
  cardId: string,
  listId: string
): Board {
  let movedCard: BoardCard | null = null

  const listsWithoutCard = board.lists.map((list) => {
    const cards = list.cards.filter((card) => {
      if (card.id === cardId) {
        movedCard = { ...card, listId }
        return false
      }

      return true
    })

    return { ...list, cards }
  })

  if (!movedCard) {
    return board
  }

  return {
    ...board,
    lists: listsWithoutCard.map((list) =>
      list.id === listId
        ? { ...list, cards: [...list.cards, movedCard as BoardCard] }
        : list
    ),
  }
}

export function removeCardFromBoard(board: Board, cardId: string): Board {
  return {
    ...board,
    lists: board.lists.map((list) => ({
      ...list,
      cards: list.cards.filter((card) => card.id !== cardId),
    })),
  }
}

export function renameListInBoard(
  board: Board,
  listId: string,
  title: string
): Board {
  return {
    ...board,
    lists: board.lists.map((list) =>
      list.id === listId ? { ...list, title } : list
    ),
  }
}

export function priorityDot(priority: BoardCardPriority | null) {
  return priority === "HIGH"
    ? "bg-rose-500"
    : priority === "MEDIUM"
      ? "bg-amber-500"
      : priority === "LOW"
        ? "bg-teal-500"
        : "bg-zinc-300 dark:bg-zinc-600"
}

export function priorityBar(priority: BoardCardPriority | null) {
  return priority === "HIGH"
    ? "bg-rose-500"
    : priority === "MEDIUM"
      ? "bg-amber-500"
      : priority === "LOW"
        ? "bg-teal-500"
        : "bg-zinc-300 dark:bg-zinc-700"
}

export function labelClass(label: string) {
  return [
    "bg-sky-500/10 text-sky-800 dark:bg-sky-400/15 dark:text-sky-100",
    "bg-violet-500/10 text-violet-800 dark:bg-violet-400/15 dark:text-violet-100",
    "bg-teal-500/10 text-teal-800 dark:bg-teal-400/15 dark:text-teal-100",
    "bg-fuchsia-500/10 text-fuchsia-800 dark:bg-fuchsia-400/15 dark:text-fuchsia-100",
  ][labelTone(label)]
}

export function panelLabelClass(label: string) {
  return [
    "bg-sky-100 text-sky-900 dark:bg-sky-300/20 dark:text-sky-100",
    "bg-violet-100 text-violet-900 dark:bg-violet-300/20 dark:text-violet-100",
    "bg-teal-100 text-teal-900 dark:bg-teal-300/20 dark:text-teal-100",
    "bg-fuchsia-100 text-fuchsia-900 dark:bg-fuchsia-300/20 dark:text-fuchsia-100",
  ][labelTone(label)]
}

export function dateBadgeClass(value: string) {
  if (value < todayDateKey()) {
    return "bg-rose-500/10 text-rose-700 dark:text-rose-200"
  }

  if (value === todayDateKey()) {
    return "bg-amber-500/10 text-amber-700 dark:text-amber-200"
  }

  return "bg-teal-500/10 text-teal-700 dark:text-teal-200"
}

export function normalizeLabels(labels: string[]) {
  return labels
    .map(normalizeLabel)
    .filter((label): label is string => Boolean(label))
    .filter(
      (label, index, allLabels) =>
        allLabels.findIndex(
          (candidate) => candidate.toLowerCase() === label.toLowerCase()
        ) === index
    )
}

export function normalizeLabel(label: string) {
  const normalized = label.trim().replace(/^#/, "").replace(/\s+/g, "-")
  return normalized ? normalized.slice(0, 32) : null
}

export function sameLabels(left: string[], right: string[]) {
  return sameStrings(
    left.map((label) => label.toLowerCase()).sort(),
    right.map((label) => label.toLowerCase()).sort()
  )
}

export function sameStrings(left: string[], right: string[]) {
  if (left.length !== right.length) {
    return false
  }

  return left.every((value, index) => value === right[index])
}

export function todayDateKey() {
  const date = new Date()
  const year = date.getFullYear()
  const month = `${date.getMonth() + 1}`.padStart(2, "0")
  const day = `${date.getDate()}`.padStart(2, "0")

  return `${year}-${month}-${day}`
}

export function initials(value?: string | null) {
  if (!value) {
    return "?"
  }

  return value
    .split(/\s|@/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join("")
}

export function shortDate(value: string) {
  return new Intl.DateTimeFormat(undefined, {
    month: "short",
    day: "numeric",
  }).format(dateFromKey(value))
}

function labelTone(label: string) {
  return (
    [...label].reduce((sum, character) => sum + character.charCodeAt(0), 0) % 4
  )
}

function dateFromKey(value: string) {
  const [year, month, day] = value.split("-").map(Number)

  return new Date(year, (month ?? 1) - 1, day ?? 1)
}
