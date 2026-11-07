/// <reference types="vite/client" />

// Without these, every import.meta.env value is `any`.
interface ImportMetaEnv {
  readonly VITE_API_URL?: string
  readonly VITE_DOCS_URL?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
