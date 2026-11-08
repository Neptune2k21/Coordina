import { Field, FieldLabel } from "@/components/ui/field"
import { Input } from "@/components/ui/input"
import { Textarea } from "@/components/ui/textarea"
import {
  ProjectColorPicker,
  ProjectIconMark,
  ProjectIconPicker,
} from "@/features/projects/components/project-personalization"
import type { ProjectDetails } from "@/features/projects/project-details"

type ProjectDetailsFieldsProps = {
  idPrefix: string
  value: ProjectDetails
  onChange: (value: ProjectDetails) => void
  placeholders?: Partial<Pick<ProjectDetails, "name" | "description" | "key">>
}

/** Name, description, key, icon and color, shared by every project form. */
export function ProjectDetailsFields({
  idPrefix,
  onChange,
  placeholders = {},
  value,
}: ProjectDetailsFieldsProps) {
  const set = (changes: Partial<ProjectDetails>) =>
    onChange({ ...value, ...changes })

  return (
    <>
      <Field>
        <FieldLabel htmlFor={`${idPrefix}-name`}>Project name</FieldLabel>
        <Input
          id={`${idPrefix}-name`}
          value={value.name}
          placeholder={placeholders.name}
          autoComplete="off"
          onChange={(event) => set({ name: event.target.value })}
          className="h-9 rounded-md bg-white px-3 text-sm dark:bg-white/[0.06]"
        />
      </Field>
      <Field>
        <FieldLabel htmlFor={`${idPrefix}-description`}>Description</FieldLabel>
        <Textarea
          id={`${idPrefix}-description`}
          value={value.description}
          placeholder={placeholders.description}
          onChange={(event) => set({ description: event.target.value })}
          className="rounded-md bg-white px-3 text-sm dark:bg-white/[0.06]"
        />
      </Field>
      <div className="grid gap-3 sm:grid-cols-[1fr_2fr]">
        <Field>
          <FieldLabel htmlFor={`${idPrefix}-key`}>Key</FieldLabel>
          <Input
            id={`${idPrefix}-key`}
            value={value.key}
            placeholder={placeholders.key}
            autoComplete="off"
            onChange={(event) => set({ key: event.target.value })}
            className="h-9 rounded-md bg-white px-3 text-sm uppercase dark:bg-white/[0.06]"
          />
        </Field>
        <Field>
          <FieldLabel>Preview</FieldLabel>
          <div className="flex h-9 items-center gap-3 rounded-md border border-zinc-950/10 bg-white px-3 dark:border-white/10 dark:bg-white/[0.06]">
            <ProjectIconMark icon={value.icon} color={value.color} />
            <span className="truncate text-sm font-medium">
              {value.name || placeholders.name || "Project"}
            </span>
          </div>
        </Field>
      </div>
      <Field>
        <FieldLabel>Icon</FieldLabel>
        <ProjectIconPicker
          value={value.icon}
          onChange={(icon) => set({ icon })}
        />
      </Field>
      <Field>
        <FieldLabel>Color</FieldLabel>
        <ProjectColorPicker
          value={value.color}
          onChange={(color) => set({ color })}
        />
      </Field>
    </>
  )
}
