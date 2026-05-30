import {
  Bug,
  CalendarBlank,
  ChartLineUp,
  Code,
  Database,
  Flag,
  FolderOpen,
  GlobeHemisphereWest,
  Kanban,
  Lightning,
  Megaphone,
  RocketLaunch,
  ShieldCheck,
  Sparkle,
  Target,
  UsersThree,
} from "@phosphor-icons/react"
import type { ComponentType } from "react"

import { Button } from "@/components/ui/button"
import { cn } from "@/lib/utils"

type IconComponent = ComponentType<{
  className?: string
  weight?: "regular" | "bold" | "fill" | "duotone" | "thin" | "light"
}>

type ProjectIconOption = {
  icon: IconComponent
  label: string
  value: string
}

type ProjectColorOption = {
  background: string
  border: string
  label: string
  ring: string
  swatch: string
  text: string
  value: string
}

const projectIconOptions = [
  { value: "kanban", label: "Kanban", icon: Kanban },
  { value: "rocket", label: "Launch", icon: RocketLaunch },
  { value: "target", label: "Goal", icon: Target },
  { value: "calendar", label: "Timeline", icon: CalendarBlank },
  { value: "code", label: "Engineering", icon: Code },
  { value: "database", label: "Data", icon: Database },
  { value: "bug", label: "Bugs", icon: Bug },
  { value: "chart", label: "Metrics", icon: ChartLineUp },
  { value: "users", label: "Team", icon: UsersThree },
  { value: "shield", label: "Security", icon: ShieldCheck },
  { value: "sparkle", label: "Ideas", icon: Sparkle },
  { value: "folder", label: "Folder", icon: FolderOpen },
  { value: "globe", label: "Global", icon: GlobeHemisphereWest },
  { value: "lightning", label: "Fast work", icon: Lightning },
  { value: "flag", label: "Milestone", icon: Flag },
  { value: "megaphone", label: "Marketing", icon: Megaphone },
] satisfies ProjectIconOption[]

const projectColorOptions = [
  {
    value: "slate",
    label: "Slate",
    background: "bg-slate-100 dark:bg-slate-400/16",
    border: "border-slate-200 dark:border-slate-300/20",
    text: "text-slate-700 dark:text-slate-100",
    ring: "ring-slate-400/50",
    swatch: "bg-slate-500",
  },
  {
    value: "teal",
    label: "Teal",
    background: "bg-teal-100 dark:bg-teal-400/16",
    border: "border-teal-200 dark:border-teal-300/20",
    text: "text-teal-700 dark:text-teal-100",
    ring: "ring-teal-400/50",
    swatch: "bg-teal-500",
  },
  {
    value: "sky",
    label: "Sky",
    background: "bg-sky-100 dark:bg-sky-400/16",
    border: "border-sky-200 dark:border-sky-300/20",
    text: "text-sky-700 dark:text-sky-100",
    ring: "ring-sky-400/50",
    swatch: "bg-sky-500",
  },
  {
    value: "indigo",
    label: "Indigo",
    background: "bg-indigo-100 dark:bg-indigo-400/16",
    border: "border-indigo-200 dark:border-indigo-300/20",
    text: "text-indigo-700 dark:text-indigo-100",
    ring: "ring-indigo-400/50",
    swatch: "bg-indigo-500",
  },
  {
    value: "rose",
    label: "Rose",
    background: "bg-rose-100 dark:bg-rose-400/16",
    border: "border-rose-200 dark:border-rose-300/20",
    text: "text-rose-700 dark:text-rose-100",
    ring: "ring-rose-400/50",
    swatch: "bg-rose-500",
  },
  {
    value: "amber",
    label: "Amber",
    background: "bg-amber-100 dark:bg-amber-400/16",
    border: "border-amber-200 dark:border-amber-300/20",
    text: "text-amber-700 dark:text-amber-100",
    ring: "ring-amber-400/50",
    swatch: "bg-amber-500",
  },
  {
    value: "emerald",
    label: "Emerald",
    background: "bg-emerald-100 dark:bg-emerald-400/16",
    border: "border-emerald-200 dark:border-emerald-300/20",
    text: "text-emerald-700 dark:text-emerald-100",
    ring: "ring-emerald-400/50",
    swatch: "bg-emerald-500",
  },
  {
    value: "violet",
    label: "Violet",
    background: "bg-violet-100 dark:bg-violet-400/16",
    border: "border-violet-200 dark:border-violet-300/20",
    text: "text-violet-700 dark:text-violet-100",
    ring: "ring-violet-400/50",
    swatch: "bg-violet-500",
  },
] satisfies ProjectColorOption[]

function getProjectIconOption(icon?: string | null) {
  return projectIconOptions.find((option) => option.value === icon)
}

function getProjectColorOption(color?: string | null) {
  return projectColorOptions.find((option) => option.value === color)
}

function ProjectIconMark({
  className,
  color,
  icon,
}: {
  className?: string
  color?: string | null
  icon?: string | null
}) {
  const iconOption = getProjectIconOption(icon)
  const colorOption = getProjectColorOption(color)
  const Icon = iconOption?.icon

  return (
    <span
      className={cn(
        "grid size-8 shrink-0 place-items-center rounded-2xl border text-sm",
        colorOption
          ? [colorOption.background, colorOption.border, colorOption.text]
          : "border-zinc-950/10 bg-zinc-950/[0.06] text-zinc-700 dark:border-white/10 dark:bg-white/[0.08] dark:text-zinc-100",
        className
      )}
      title={iconOption?.label}
    >
      {Icon ? (
        <Icon className="size-4" weight="bold" />
      ) : (
        <span className="text-xs">{icon || "⌁"}</span>
      )}
    </span>
  )
}

function ProjectIconPicker({
  onChange,
  value,
}: {
  onChange: (icon: string) => void
  value: string
}) {
  return (
    <div className="grid grid-cols-4 gap-2 sm:grid-cols-8">
      {projectIconOptions.map((option) => {
        const Icon = option.icon
        const isSelected = value === option.value

        return (
          <Button
            key={option.value}
            type="button"
            variant="outline"
            size="icon-lg"
            aria-label={option.label}
            aria-pressed={isSelected}
            title={option.label}
            onClick={() => onChange(option.value)}
            className={cn(
              "h-11 w-full rounded-2xl bg-white/70 dark:bg-white/[0.06]",
              isSelected &&
                "border-zinc-950 bg-zinc-950 text-white hover:bg-zinc-900 dark:border-white dark:bg-white dark:text-zinc-950"
            )}
          >
            <Icon className="size-4" weight={isSelected ? "bold" : "regular"} />
          </Button>
        )
      })}
    </div>
  )
}

function ProjectColorPicker({
  onChange,
  value,
}: {
  onChange: (color: string) => void
  value: string
}) {
  return (
    <div className="grid grid-cols-4 gap-2 sm:grid-cols-8">
      {projectColorOptions.map((option) => {
        const isSelected = value === option.value

        return (
          <button
            key={option.value}
            type="button"
            aria-label={option.label}
            aria-pressed={isSelected}
            title={option.label}
            onClick={() => onChange(option.value)}
            className={cn(
              "flex h-10 items-center justify-center rounded-2xl border transition-all outline-none focus-visible:ring-[3px]",
              option.background,
              option.border,
              option.ring,
              isSelected && "ring-[3px]"
            )}
          >
            <span
              className={cn(
                "size-3 rounded-full border border-white/80 shadow-sm",
                option.swatch
              )}
            />
          </button>
        )
      })}
    </div>
  )
}

export { ProjectColorPicker, ProjectIconMark, ProjectIconPicker }
