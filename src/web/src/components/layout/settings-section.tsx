type SettingsSectionProps = {
  title: string
  description: string
  children: React.ReactNode
}

/** One titled block of a settings page. */
export function SettingsSection({
  children,
  description,
  title,
}: SettingsSectionProps) {
  return (
    <section
      aria-label={title}
      className="grid gap-4 rounded-md border border-zinc-950/[0.08] bg-white/84 p-4 shadow-[0_12px_28px_rgba(24,24,27,0.06)] backdrop-blur-xl dark:border-white/10 dark:bg-white/[0.055]"
    >
      <header className="grid gap-1">
        <h2 className="text-sm font-semibold">{title}</h2>
        <p className="text-sm text-muted-foreground">{description}</p>
      </header>
      {children}
    </section>
  )
}
