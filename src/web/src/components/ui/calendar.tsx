import { CaretLeft, CaretRight } from "@phosphor-icons/react"
import { useMemo, useState } from "react"

import { Button } from "@/components/ui/button"
import { cn } from "@/lib/utils"

type CalendarProps = {
  value?: string | null
  onChange: (value: string | null) => void
  minValue?: string
  className?: string
}

const weekdays = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"]

export function Calendar({
  value,
  onChange,
  minValue,
  className,
}: CalendarProps) {
  const selected = value ? fromDateKey(value) : null
  const [visibleMonth, setVisibleMonth] = useState(() => {
    const base = selected ?? new Date()
    return new Date(base.getFullYear(), base.getMonth(), 1)
  })

  const days = useMemo(() => buildMonthGrid(visibleMonth), [visibleMonth])

  function shiftMonth(delta: number) {
    setVisibleMonth(
      (current) =>
        new Date(current.getFullYear(), current.getMonth() + delta, 1)
    )
  }

  return (
    <div
      className={cn(
        "rounded-md border border-zinc-950/10 bg-white p-2 text-xs dark:border-white/10 dark:bg-white/[0.055]",
        className
      )}
    >
      <div className="mb-2 flex h-8 items-center justify-between gap-2">
        <Button
          type="button"
          variant="ghost"
          size="icon"
          className="size-7 rounded-md"
          onClick={() => shiftMonth(-1)}
        >
          <CaretLeft className="size-3.5" />
        </Button>
        <div className="text-xs font-semibold">
          {visibleMonth.toLocaleDateString(undefined, {
            month: "long",
            year: "numeric",
          })}
        </div>
        <Button
          type="button"
          variant="ghost"
          size="icon"
          className="size-7 rounded-md"
          onClick={() => shiftMonth(1)}
        >
          <CaretRight className="size-3.5" />
        </Button>
      </div>
      <div className="grid grid-cols-7 gap-1 text-center">
        {weekdays.map((weekday) => (
          <div
            key={weekday}
            className="py-1 text-[10px] font-medium text-muted-foreground"
          >
            {weekday}
          </div>
        ))}
        {days.map((day) => {
          const key = toDateKey(day.date)
          const isSelected = key === value
          const isDisabled = minValue ? key < minValue : false

          return (
            <button
              key={key}
              type="button"
              className={cn(
                "grid aspect-square place-items-center rounded-md text-[11px] transition-colors hover:bg-zinc-950/5 dark:hover:bg-white/10",
                !day.isCurrentMonth && "text-muted-foreground/45",
                isDisabled &&
                  "cursor-not-allowed text-muted-foreground/25 hover:bg-transparent dark:hover:bg-transparent",
                isSelected &&
                  "bg-primary text-primary-foreground hover:bg-primary"
              )}
              disabled={isDisabled}
              onClick={() => onChange(isSelected ? null : key)}
            >
              {day.date.getDate()}
            </button>
          )
        })}
      </div>
    </div>
  )
}

function buildMonthGrid(month: Date) {
  const firstDay = new Date(month.getFullYear(), month.getMonth(), 1)
  const startOffset = (firstDay.getDay() + 6) % 7
  const start = new Date(firstDay)
  start.setDate(firstDay.getDate() - startOffset)

  return Array.from({ length: 42 }, (_, index) => {
    const date = new Date(start)
    date.setDate(start.getDate() + index)

    return {
      date,
      isCurrentMonth: date.getMonth() === month.getMonth(),
    }
  })
}

function fromDateKey(value: string) {
  const [year, month, day] = value.split("-").map(Number)
  return new Date(year, month - 1, day)
}

function toDateKey(date: Date) {
  const year = date.getFullYear()
  const month = `${date.getMonth() + 1}`.padStart(2, "0")
  const day = `${date.getDate()}`.padStart(2, "0")

  return `${year}-${month}-${day}`
}
