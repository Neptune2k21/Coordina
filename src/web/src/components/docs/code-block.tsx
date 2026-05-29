import { Check, Copy } from "@phosphor-icons/react"
import { useState } from "react"

import { Button } from "@/components/ui/button"

type CodeBlockProps = {
  code: string
  filename?: string
  language?: string
}

export function CodeBlock({
  code,
  filename,
  language = "bash",
}: CodeBlockProps) {
  const [copied, setCopied] = useState(false)

  async function copyCode() {
    await navigator.clipboard.writeText(code)
    setCopied(true)
    window.setTimeout(() => setCopied(false), 1400)
  }

  return (
    <div className="overflow-hidden rounded-md border border-zinc-950/10 bg-zinc-950 text-white shadow-[0_18px_60px_rgba(24,24,27,0.14)] dark:border-white/10">
      <div className="flex items-center justify-between gap-3 border-b border-white/10 bg-white/[0.04] px-4 py-2.5">
        <div className="flex min-w-0 items-center gap-2">
          <span className="size-2.5 rounded-full bg-rose-400" />
          <span className="size-2.5 rounded-full bg-amber-300" />
          <span className="size-2.5 rounded-full bg-teal-300" />
          <span className="ml-2 truncate text-xs font-medium text-white/52">
            {filename ?? language}
          </span>
        </div>
        <Button
          type="button"
          variant="ghost"
          size="sm"
          className="h-8 rounded-md px-2.5 text-xs text-white/72 hover:bg-white/10 hover:text-white"
          onClick={copyCode}
        >
          {copied ? (
            <Check className="size-3.5" />
          ) : (
            <Copy className="size-3.5" />
          )}
          {copied ? "Copied" : "Copy"}
        </Button>
      </div>
      <pre className="overflow-x-auto p-4 text-sm leading-7">
        <code>{code}</code>
      </pre>
    </div>
  )
}
