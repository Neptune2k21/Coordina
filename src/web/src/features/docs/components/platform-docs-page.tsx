import { BookOpenText, BracketsCurly } from "@phosphor-icons/react"
import { useMemo } from "react"

import { CodeBlock } from "@/components/docs/code-block"
import { DocHeading } from "@/components/docs/doc-heading"
import { DocLink } from "@/components/docs/doc-link"
import { DocSection } from "@/components/docs/doc-section"
import { DocsShell } from "@/components/docs/docs-shell"
import {
  apiResources,
  curlCode,
  docsApiBaseUrl,
  docsNavItems,
  docsSections,
  getDocsLlmContext,
} from "@/features/docs/docs-content"

export function PlatformDocsPage() {
  const llmContext = useMemo(() => getDocsLlmContext(), [])

  return (
    <DocsShell
      apiDocsUrl={`${docsApiBaseUrl}/api-docs`}
      badgeIcon={BookOpenText}
      badgeText="Platform guide"
      copyText={llmContext}
      description="A compact guide for working on the React app, authentication flow, local environment, and API contract."
      navItems={docsNavItems}
      primaryAction={{ href: "#quickstart", label: "Start building" }}
      secondaryAction={{
        href: `${docsApiBaseUrl}/api-docs`,
        label: "Open API tester",
      }}
      subtitle="Documentation"
      title="Build with Coordina without losing the shape of the product."
    >
      {docsSections.map((section) => (
        <DocSection id={section.id} key={section.id}>
          <DocHeading icon={section.icon} title={section.title} />
          <p className="max-w-2xl text-sm leading-7 text-muted-foreground">
            {section.body}
          </p>
          <CodeBlock
            code={section.code.value}
            filename={section.code.filename}
            language={section.code.language}
          />
        </DocSection>
      ))}

      <DocSection id="api">
        <DocHeading icon={BracketsCurly} title="API testing" />
        <p className="max-w-2xl text-sm leading-7 text-muted-foreground">
          Use the interactive OpenAPI UI to test auth and health endpoints, or
          copy a curl request directly.
        </p>
        <div className="grid gap-3 sm:grid-cols-2">
          {apiResources.map((resource) => (
            <DocLink
              description={resource.description}
              href={resource.href}
              icon={resource.icon}
              key={resource.href}
              title={resource.title}
            />
          ))}
        </div>
        <CodeBlock filename="register.sh" code={curlCode} />
      </DocSection>
    </DocsShell>
  )
}
