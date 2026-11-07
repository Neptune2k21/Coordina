import type { BoardTemplateDefinition } from "@/types/task"

export function countTemplateCards(template: BoardTemplateDefinition) {
  return template.lists.reduce((total, list) => total + list.cards.length, 0)
}

export function countTemplateDependencies(template: BoardTemplateDefinition) {
  return template.lists
    .flatMap((list) => list.cards)
    .reduce((total, card) => total + card.dependsOn.length, 0)
}
