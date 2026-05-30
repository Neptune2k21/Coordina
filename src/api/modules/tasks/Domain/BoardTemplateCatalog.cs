namespace Coordina.Api.Modules.Tasks.Domain;

public static class BoardTemplateCatalog
{
  public static IReadOnlyCollection<BoardListTemplate> GetLists(
    BoardTemplate template)
  {
    return template switch
    {
      BoardTemplate.Basic => [
        new("To Do", [
          new("Clarify scope", "Write the target outcome and owner.", BoardCardPriority.Medium, ["planning"]),
          new("Create first card", "Drag this card as work progresses.", null, ["starter"])
        ]),
        new("In Progress", [
          new("Review board workflow", "Move one active item here while it is being worked on.", BoardCardPriority.Low, ["workflow"])
        ]),
        new("Done", [
          new("Board created", "Keep completed work here for fast scanning.", null, ["done"])
        ])
      ],
      BoardTemplate.AgileScrum => [
        new("Backlog", [
          new("Define user story", "As a user, I want a clear outcome so the team can estimate it.", BoardCardPriority.Medium, ["story"]),
          new("Refine acceptance criteria", "Add testable conditions before moving into Sprint.", BoardCardPriority.High, ["ready"])
        ]),
        new("Sprint", [
          new("Plan sprint commitment", "Pick the work that fits the current sprint capacity.", BoardCardPriority.Medium, ["sprint"])
        ]),
        new("In Progress", [
          new("Implement first vertical slice", "Keep one focused implementation card active.", BoardCardPriority.High, ["dev"])
        ]),
        new("Review", [
          new("Code review checklist", "Check behavior, tests, and UX before Done.", BoardCardPriority.Medium, ["review"])
        ]),
        new("Done", [
          new("Sprint board initialized", "Archive or keep starter cards as examples.", null, ["done"])
        ])
      ],
      BoardTemplate.BugTracking => [
        new("Reported", [
          new("Crash on login", "Add browser, account, steps to reproduce, and expected behavior.", BoardCardPriority.High, ["bug", "triage"]),
          new("Visual regression", "Attach screenshot and affected viewport.", BoardCardPriority.Medium, ["ui"])
        ]),
        new("Investigating", [
          new("Reproduce issue locally", "Confirm whether the bug is environment-specific.", BoardCardPriority.High, ["debug"])
        ]),
        new("Fixed", [
          new("Add regression test", "Protect the fix before release.", BoardCardPriority.Medium, ["test"])
        ]),
        new("Released", [
          new("Patch published", "Track release notes and customer confirmation.", null, ["release"])
        ])
      ],
      BoardTemplate.ProductRoadmap => [
        new("Ideas", [
          new("Collect customer signal", "Summarize who asked for it and why it matters.", BoardCardPriority.Low, ["discovery"]),
          new("Estimate impact", "Score reach, confidence, and effort.", BoardCardPriority.Medium, ["prioritization"])
        ]),
        new("Planned", [
          new("Write product brief", "Capture problem, audience, scope, and non-goals.", BoardCardPriority.High, ["brief"])
        ]),
        new("In Progress", [
          new("Build MVP slice", "Keep the first measurable delivery thin.", BoardCardPriority.High, ["mvp"])
        ]),
        new("Shipped", [
          new("Measure adoption", "Review usage and feedback after launch.", BoardCardPriority.Medium, ["metrics"])
        ])
      ],
      _ => []
    };
  }
}

public sealed record BoardListTemplate(
  string Title,
  IReadOnlyCollection<BoardCardTemplate> Cards);

public sealed record BoardCardTemplate(
  string Title,
  string? Description,
  BoardCardPriority? Priority,
  IReadOnlyCollection<string> Labels);
