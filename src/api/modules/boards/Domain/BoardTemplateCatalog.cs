namespace Coordina.Api.Modules.Boards.Domain;

/// <summary>
/// The only definition of board templates. The API creates boards from it and
/// the web app renders its preview from <c>GET /board-templates</c>.
/// </summary>
public static class BoardTemplateCatalog
{
  private static readonly BoardTemplateDefinition Basic = new(
    BoardTemplate.Basic,
    "Basic",
    "Simple workflow for small delivery tracks.",
    [
      new("To Do", BoardTaskStatus.Todo, [
        new("access", "Give the team access to shared tools", "Make sure everyone can open the documents and tools this work needs.", BoardCardPriority.Medium, 15, ["setup"]),
        new("feedback", "Share the plan for feedback", "Send the plan to the people affected and collect objections before work starts.", BoardCardPriority.Medium, 30, ["planning"])
        {
          DependsOn = ["plan"],
          DueInDays = 2
        },
        new("deliver", "Deliver the main piece of work", "Do the work described in the plan. Split it up if it takes more than a day.", BoardCardPriority.High, 240, ["delivery"])
        {
          Subtasks = ["Prepare what the work needs", "Complete a first version", "Fix what the first review finds"],
          DependsOn = ["feedback"],
          DueInDays = 7
        },
        new("check", "Check the result against the goal", "Compare what was delivered with the goal and note any gaps.", BoardCardPriority.Medium, 45, ["review"])
        {
          DependsOn = ["deliver"],
          DueInDays = 8
        },
        new("announce", "Announce the outcome", "Tell the people affected what changed and where to ask questions.", BoardCardPriority.Low, 20, ["communication"])
        {
          DependsOn = ["check"]
        }
      ]),
      new("In Progress", BoardTaskStatus.InProgress, [
        new("plan", "Write the plan", "List the steps, who does each one, and when each should finish.", BoardCardPriority.High, 60, ["planning"])
        {
          Subtasks = ["List the steps", "Name an owner for each step", "Set target dates"],
          DependsOn = ["goal"],
          DueInDays = 1
        }
      ]),
      new("Done", BoardTaskStatus.Done, [
        new("goal", "Agree on the goal", "One sentence the whole team agrees on: what changes when this work is finished.", BoardCardPriority.Medium, 30, ["planning"])
      ])
    ]);

  // "Sprint Backlog" is committed work that nobody has started, so it stays TODO.
  private static readonly BoardTemplateDefinition AgileScrum = new(
    BoardTemplate.AgileScrum,
    "Agile Scrum",
    "Sprint workflow with backlog and review stages.",
    [
      new("Backlog", BoardTaskStatus.Todo, [
        new("sso", "Compare single sign-on options", "Time-boxed spike: list the options and recommend one for a later sprint.", BoardCardPriority.Low, 240, ["spike"]),
        new("audit", "Log account security events", "As an admin, I can see password resets and failed sign-ins for my workspace.", BoardCardPriority.Medium, 480, ["story", "security"])
        {
          DependsOn = ["rate-limit"]
        }
      ]),
      new("Sprint Backlog", BoardTaskStatus.Todo, [
        new("reset-form", "Build the reset password form", "As a user, I can choose a new password from the link in my email.", BoardCardPriority.High, 240, ["story", "frontend"])
        {
          Subtasks = ["Validate the password rules", "Handle expired links", "Show a confirmation"],
          DependsOn = ["reset-api"],
          DueInDays = 5
        },
        new("rate-limit", "Rate limit reset requests", "Stop one address from requesting unlimited reset emails.", BoardCardPriority.Medium, 120, ["backend", "security"])
        {
          DependsOn = ["reset-api"],
          DueInDays = 6
        },
        new("e2e", "Test the reset flow end to end", "Request a reset, open the email, set a password and sign in with it.", BoardCardPriority.Medium, 120, ["test"])
        {
          DependsOn = ["reset-form", "reset-email"],
          DueInDays = 8
        },
        new("demo", "Prepare the sprint demo", "Pick what to show and check that it works on the demo environment.", BoardCardPriority.Low, 45, ["ceremony"])
        {
          DependsOn = ["e2e"],
          DueInDays = 10
        }
      ]),
      new("In Progress", BoardTaskStatus.InProgress, [
        new("reset-email", "Write the reset email", "Plain, short and translated. The link expires after one hour.", BoardCardPriority.Medium, 90, ["story", "backend"])
        {
          Subtasks = ["Write the text", "Add the signed link", "Send a test to each mail provider"],
          DependsOn = ["flow"],
          DueInDays = 3
        }
      ]),
      new("Review", BoardTaskStatus.InProgress, [
        new("reset-api", "Add the password reset API", "Create and consume single-use reset tokens. Waiting for code review.", BoardCardPriority.High, 240, ["story", "backend"])
        {
          Subtasks = ["Create reset tokens", "Expire tokens after one hour", "Cover with integration tests"],
          DependsOn = ["flow"],
          DueInDays = 2
        }
      ]),
      new("Done", BoardTaskStatus.Done, [
        new("planning", "Hold sprint planning", "Agree on the sprint goal and the work that fits the team's capacity.", BoardCardPriority.Medium, 60, ["ceremony"]),
        new("flow", "Agree on the reset flow", "Screens, emails and error cases reviewed with design and support.", BoardCardPriority.High, 90, ["design"])
        {
          DependsOn = ["planning"]
        }
      ])
    ]);

  // "Verifying" replaces "Fixed": a fix is not delivered until it is released,
  // so the list stays IN_PROGRESS and its name no longer suggests otherwise.
  private static readonly BoardTemplateDefinition BugTracking = new(
    BoardTemplate.BugTracking,
    "Bug Tracking",
    "Triage flow for defects and release validation.",
    [
      new("Reported", BoardTaskStatus.Todo, [
        new("login-fix", "Fix the Safari login crash", "Ready to start as soon as the cause is confirmed.", BoardCardPriority.High, 120, ["bug", "frontend"])
        {
          DependsOn = ["login-crash"],
          DueInDays = 2
        },
        new("export", "Export fails above 10,000 rows", "Reported by two customers. The download stops without an error message.", BoardCardPriority.High, 60, ["bug", "backend"])
        {
          Subtasks = ["Reproduce with a large workspace", "Attach the failing request"],
          DueInDays = 3
        },
        new("login-test", "Add a regression test for the Safari login", "Fail the build if the crash comes back.", BoardCardPriority.Medium, 60, ["test"])
        {
          DependsOn = ["login-fix"]
        },
        new("notes", "Prepare notes for the next patch", "List the fixes in words customers will recognize.", BoardCardPriority.Medium, 30, ["release"])
        {
          DependsOn = ["login-fix", "logout-fix"],
          DueInDays = 5
        },
        new("notify", "Tell the customers who reported the crash", "Reply to each support ticket once the patch is out.", BoardCardPriority.Low, 15, ["support"])
        {
          DependsOn = ["notes"]
        },
        new("legend", "Chart legend unreadable in dark mode", "Text and background are almost the same color. Screenshot in the ticket.", BoardCardPriority.Low, 30, ["bug", "ui"])
      ]),
      new("Investigating", BoardTaskStatus.InProgress, [
        new("login-crash", "Find why login crashes on Safari", "Started with the last release. Confirm the cause before anyone writes a fix.", BoardCardPriority.High, 120, ["bug", "triage"])
        {
          Subtasks = ["Reproduce on Safari 18", "Capture the console error", "Identify the release that introduced it"],
          DueInDays = 1
        },
        new("search", "Find why search times out", "Only large workspaces are affected. Check the slow query log first.", BoardCardPriority.Medium, 240, ["bug", "backend"])
      ]),
      new("Verifying", BoardTaskStatus.InProgress, [
        new("logout-fix", "Verify the fix for logouts on refresh", "The fix is merged. Confirm it on staging before it goes in the patch.", BoardCardPriority.High, 45, ["bug", "qa"])
        {
          Subtasks = ["Test on Chrome and Firefox", "Check that expired sessions still sign out"],
          DueInDays = 2
        }
      ]),
      new("Released", BoardTaskStatus.Done, [
        new("invoice", "Stop duplicate invoice emails", "Retries sent the email again. Each invoice now sends once.", BoardCardPriority.High, 120, ["bug", "backend"]),
        new("patch", "Publish the last patch", "Released with notes, and support was told what changed.", BoardCardPriority.Medium, 30, ["release"])
        {
          DependsOn = ["invoice"]
        }
      ])
    ]);

  private static readonly BoardTemplateDefinition ProductRoadmap = new(
    BoardTemplate.ProductRoadmap,
    "Product Roadmap",
    "Roadmap pipeline from discovery to launch.",
    [
      new("Ideas", BoardTaskStatus.Todo, [
        new("score", "Score the next ideas", "Rate each one on reach, confidence and effort so the next pick is easy to explain.", BoardCardPriority.Medium, 60, ["prioritization"]),
        new("calendar", "Explore a calendar integration", "Three customers asked for it. Find out which calendars they use and why.", BoardCardPriority.Low, 120, ["discovery"]),
        new("adoption", "Review adoption after launch", "Compare the results with the metrics agreed before launch and decide what is next.", BoardCardPriority.Medium, 60, ["metrics"])
        {
          DependsOn = ["launch"]
        }
      ]),
      new("Planned", BoardTaskStatus.Todo, [
        new("metrics", "Define adoption metrics", "Choose what success looks like before the beta, and check that it can be measured.", BoardCardPriority.High, 60, ["metrics"])
        {
          DueInDays = 4
        },
        new("beta", "Run a private beta with ten teams", "Watch the first session of each team and collect what confused them.", BoardCardPriority.High, 480, ["beta"])
        {
          Subtasks = ["Invite ten teams", "Watch each first session", "Sort the feedback by theme"],
          DependsOn = ["mvp"],
          DueInDays = 14
        },
        new("launch", "Prepare the public launch", "Announcement, updated pricing page and a briefing for support.", BoardCardPriority.Medium, 240, ["launch"])
        {
          DependsOn = ["beta", "metrics", "help"],
          DueInDays = 21
        }
      ]),
      new("In Progress", BoardTaskStatus.InProgress, [
        new("mvp", "Build the guided onboarding", "Keep the first version thin: one path, three steps, no settings.", BoardCardPriority.High, 480, ["mvp"])
        {
          Subtasks = ["Create the welcome step", "Import sample data", "Track where people stop"],
          DependsOn = ["brief"],
          DueInDays = 7
        },
        new("help", "Write the onboarding help pages", "Answer the questions from the customer interviews.", BoardCardPriority.Medium, 120, ["docs"])
        {
          DependsOn = ["brief"],
          DueInDays = 10
        }
      ]),
      new("Shipped", BoardTaskStatus.Done, [
        new("interviews", "Interview five new customers", "Find where people give up during their first week.", BoardCardPriority.High, 240, ["discovery"]),
        new("brief", "Write the onboarding brief", "Problem, audience, scope and non-goals, approved by the team.", BoardCardPriority.High, 120, ["brief"])
        {
          Subtasks = ["Describe the problem", "List what is out of scope"],
          DependsOn = ["interviews"]
        }
      ])
    ]);

  public static IReadOnlyCollection<BoardTemplateDefinition> All { get; } =
    [Basic, AgileScrum, BugTracking, ProductRoadmap];

  public static IReadOnlyCollection<BoardListTemplate> GetLists(
    BoardTemplate template) =>
    All.FirstOrDefault(definition => definition.Id == template)?.Lists ?? [];
}

public sealed record BoardTemplateDefinition(
  BoardTemplate Id,
  string Name,
  string Summary,
  IReadOnlyCollection<BoardListTemplate> Lists);

public sealed record BoardListTemplate(
  string Title,
  BoardTaskStatus TaskStatus,
  IReadOnlyCollection<BoardCardTemplate> Cards);

public sealed record BoardCardTemplate(
  string Key,
  string Title,
  string Description,
  BoardCardPriority Priority,
  int EstimatedMinutes,
  IReadOnlyCollection<string> Labels)
{
  public IReadOnlyCollection<string> Subtasks { get; init; } = [];

  /// <summary>Keys of the cards in the same template that this card waits for.</summary>
  public IReadOnlyCollection<string> DependsOn { get; init; } = [];

  /// <summary>Due date relative to the day the board is created.</summary>
  public int? DueInDays { get; init; }
}
