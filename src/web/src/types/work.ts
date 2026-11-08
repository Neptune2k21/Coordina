import type { components } from "@/types/api-contract"

type Schemas = components["schemas"]

export type MyWork = Schemas["MyWorkResponse"]
export type MyWorkItem = Schemas["MyWorkItemResponse"]
