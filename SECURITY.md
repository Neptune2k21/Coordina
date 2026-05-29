# Security Policy

Coordina is a pre-MVP project with a workspace-based authorization model. Security issues are taken seriously, especially anything involving authentication, token handling, workspace isolation, invitation codes, or data exposure across tenants.

## Supported Versions

The active development branch is currently supported for security fixes.

| Version | Supported |
| --- | --- |
| `master` | Yes |
| Released packages | Not yet applicable |

## Reporting a Vulnerability

Do not open a public issue for a suspected vulnerability.

Report security concerns privately through GitHub:

```text
https://github.com/Neptune2k21/Coordina/security/advisories/new
```

If GitHub private advisories are unavailable, contact the repository owner directly through the profile associated with the project.

Please include:

| Detail | Why it helps |
| --- | --- |
| Affected area | Authentication, workspace access, API, frontend, CI, dependency, or infrastructure |
| Reproduction steps | Lets the maintainer confirm the issue quickly |
| Expected impact | Helps prioritize the fix |
| Environment | Local, CI, browser, OS, dependency versions |
| Suggested mitigation | Optional, but welcome |

## Response Expectations

Expected handling:

| Step | Target |
| --- | --- |
| Initial acknowledgement | Within 5 business days |
| Triage | As soon as reproducible details are available |
| Fix plan | Based on severity and exploitability |
| Public disclosure | After a fix is available, when appropriate |

These targets are best-effort while the project is maintained by a small team.

## Current Security Model

Current protections are foundation-level, not production hardening:

| Area | Behavior |
| --- | --- |
| Password storage | PBKDF2 with per-password random salt |
| Authentication | JWT bearer tokens |
| Workspace isolation | Queries are scoped by authenticated user membership |
| Invite codes | One-time use, expiration-aware, stored as SHA-256 hashes |
| Owner permissions | Invite creation, member removal, and workspace deletion require owner role |
| Unauthorized access | Protected endpoints require bearer authentication |

## Security Expectations for Contributors

When changing security-sensitive code:

1. Enforce authorization in backend services or endpoint flow.
2. Treat frontend checks as UX only.
3. Avoid logging tokens, passwords, invite codes, or connection strings.
4. Do not commit `.env`, secrets, database dumps, or local certificates.
5. Add tests for access control behavior.
6. Prefer explicit failure modes over broad exception handling.
7. Keep dependencies current and review security advisories.

## Secrets

Local development uses `.env`, which is ignored by Git. The checked-in `.env.example` is documentation only and must not be used as production configuration.

Production deployments must use:

| Secret | Requirement |
| --- | --- |
| `Auth__SigningKey` | High entropy, private, rotated when exposed |
| `POSTGRES_PASSWORD` | Unique per environment |
| Database connection settings | Stored in the deployment secret manager |

## Out of Scope

The current project does not yet include:

| Capability | Status |
| --- | --- |
| Refresh tokens | Not implemented |
| Email verification | Not implemented |
| Password reset | Not implemented |
| Rate limiting | Not implemented |
| Audit logging | Not implemented |
| Production deployment hardening | Not documented yet |

These gaps should be addressed before handling real production users.
