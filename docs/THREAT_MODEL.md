# Threat model (STRIDE per element)

- Date: 2026-10-26
- Scope: `main` at d197e09 (after the activity timeline, PR #10): ASP.NET Core API, React frontend, SQL Server, GitHub Actions CI.
- Method: STRIDE per element, using the [Fabric](https://github.com/danielmiessler/Fabric) `create_stride_threat_model` pattern, checked against the code.
- Current exposure: development only. The API listens on `localhost:5137`, the Vite dev server on localhost, and SQL Server on `127.0.0.1:1433`. Most of the high-severity threats below become real only once the app is deployed, so they are requirements for the next roadmap item (sign-in and roles), which comes before any deploy.

# ASSETS

- Ticket data: titles, descriptions, resolution summaries and comments. Help-desk text often contains internal hostnames and error output, and sometimes credentials that users paste in.
- User directory: names, emails and roles (User / Technician).
- Activity history: an accurate record of who created, assigned, worked, resolved, closed, deleted or commented on each ticket.
- Technician privileges: the ability to assign, resolve and close tickets.
- Database credentials: the connection string, kept in .NET user secrets in development.
- The SQL Server database and its backups.
- The source repository (public) and the CI pipeline.
- Future: session cookies or tokens, once sign-in exists.

# TRUST BOUNDARIES

- TB1: browser ↔ Vite dev server (later, the deployed web host). Untrusted user input enters here.
- TB2: dev server proxy ↔ ASP.NET API.
- TB3: API ↔ SQL Server (a Docker container in development).
- TB4: developer machine ↔ public GitHub repository and GitHub Actions runners, which pull npm and NuGet packages.
- TB5 (future): the internet ↔ the deployed host. It doesn't exist yet: nothing is deployed.

# DATA FLOWS

- DF1: browser → dev server, UI assets. Crosses TB1.
- DF2: browser → `/api` through the proxy → API: create ticket, comment, assign/start/resolve/close/delete, create user (JSON). Crosses TB1 and TB2.
- DF3: API → browser: ticket list, deleted tickets, activity, user list including emails. Crosses TB2 and TB1.
- DF4: API ↔ SQL Server through EF Core. Crosses TB3.
- DF5: developer push → GitHub; Actions checks out the code, runs `npm ci` and `dotnet test`, and installs `dotnet-ef`. Crosses TB4.

# THREAT MODEL

| THREAT ID | COMPONENT NAME | THREAT NAME | STRIDE CATEGORY | WHY APPLICABLE | HOW MITIGATED | MITIGATION | LIKELIHOOD EXPLANATION | IMPACT EXPLANATION | RISK SEVERITY |
|---|---|---|---|---|---|---|---|---|---|
| 0001 | Tickets + Users API | Anyone who can reach the API can call every endpoint without identifying themselves | Spoofing | `Program.cs` has `UseAuthorization` but no authentication scheme, and no controller uses `[Authorize]` | Network only: everything listens on localhost. Not mitigated in the design | ASP.NET Core Identity or OIDC (e.g. Entra ID) with a fallback policy of `RequireAuthenticatedUser`. Required before any deploy (next roadmap item) | Low while localhost-only; near-certain once internet-facing, since scanners find new hosts within hours | Full read/write of every ticket and user | Critical |
| 0002 | `POST /api/tickets`, `POST /api/tickets/{id}/comments` | A caller files tickets or posts comments as someone else by sending another user's `CreatedByUserId` / `AuthorUserId` | Spoofing | Identity comes from the request body (`CreateTicketRequest`, `AddCommentRequest`) | The API only checks that the user exists | Remove these fields from the DTOs and take the user id from the signed-in principal | High after deploy: one curl call or the Swagger UI | Forged entries in the audit trail; phishing inside the tool ("the tech says reply with your password") | High |
| 0003 | `POST /api/users` | Anyone creates a user with Role = Technician, then assigns tickets to themselves and resolves or closes them | Elevation of Privilege | `CreateUserRequest` accepts `Role` from the caller | Not mitigated | Only an Admin role creates users or changes roles. Self sign-up, if added, always creates Role = User | High after deploy | Attacker gets technician powers | High |
| 0004 | assign / start / resolve / close / delete endpoints | An ordinary user, or anyone at all, assigns, resolves, closes or deletes any ticket | Elevation of Privilege | Nothing checks who is calling | Partly: the `Ticket` entity enforces state transitions and that the assignee is a Technician, but not the caller's role | Role policies: Technician for assign/start/resolve/close, only the assigned technician resolves, Admin for delete, reporter or technician for comments | High after deploy | Tickets closed or deleted to hide problems; ticket metrics become meaningless | High |
| 0005 | `GET /api/users`, `GET /api/tickets`, `GET /api/tickets/deleted` | Every caller can list all users' emails, every ticket's contents and every deleted ticket | Information Disclosure | The endpoints return whole entities with no filtering by user | Not mitigated | Users see only their own tickets; technicians see all. A user DTO without email for non-technicians. `/deleted` for Admin only. A "never paste passwords" hint on the form | High after deploy | Personal data leak and internal reconnaissance from ticket text | High |
| 0006 | Ticket activity log | Nobody can prove who started, resolved, closed or deleted a ticket | Repudiation | Those activity types record no actor yet (there is no sign-in to say who it was) | Partly: Created, Assigned and Comment name a user; deletes are soft, so the data remains; activities are append-only (no edit or delete endpoints) | Record the signed-in user's id on every activity; log sign-in and failed sign-in events | Medium: disputes over who closed what are routine at help desks | No accountability; weak evidence for an audit | Medium |
| 0007 | API (all routes) | Flooding writes, or repeatedly fetching the unpaged lists, slows or fills the app and database | Denial of Service | No rate limiting; `GetAllTickets` and `GetDeletedTickets` return the whole table | Partly: field lengths are capped (255 / 2000) and Kestrel's 30 MB body limit applies | `AddRateLimiter` on writes per IP or user; paging with a maximum page size of 100; a much lower `MaxRequestBodySize` (about 64 KB) | Medium after deploy: spam bots post to open forms | Database growth and a slow UI; cheap to clean up | Medium |
| 0008 | Browser ↔ API, once cookie sign-in exists | Another website makes a signed-in technician's browser close or delete a ticket (CSRF) | Tampering | Becomes relevant the moment sign-in uses cookies | Not applicable yet: there are no cookies | `SameSite=Lax` or `Strict` cookies plus ASP.NET antiforgery tokens on unsafe methods (or bearer tokens). Keep the UI and API on one origin, with no CORS | Medium if it gets missed while building sign-in | Unwanted actions under a technician's identity | Medium |
| 0009 | Deployed host configuration | Deploying with `ASPNETCORE_ENVIRONMENT=Development` exposes Swagger UI and developer exception pages with stack traces | Information Disclosure | Every launch profile sets Development, and `AllowedHosts` is `*` | Swagger is only enabled in Development (`Program.cs`) | Set Production on the host; add `UseExceptionHandler` and HSTS; set `AllowedHosts` to the real domain | Medium: a common first-deploy mistake | Hands attackers a full API map and internals | Medium |
| 0010 | Browser ↔ API transport | Ticket contents and future session cookies are read or changed on the network | Tampering | The `http` profile serves plain HTTP; `UseHttpsRedirection` has no HTTPS port to send traffic to | Localhost-only today | Terminate TLS at the host or a reverse proxy; HSTS; `Secure` cookies | Low on a managed host (TLS by default); higher if self-hosted over plain HTTP | Session hijack | Medium |
| 0011 | Public GitHub repository | Secrets or private notes get pushed to the public repo | Information Disclosure | The repo is public, and development uses a real SQL Server password | Mitigated: the connection string lives in user secrets; `.gitignore` covers `appsettings.Development.json` and `.env`; seed data uses `example.com`; local working notes are excluded with `.git/info/exclude` | Turn on GitHub secret scanning with push protection; check `git status` before each commit | Low | Medium: a leaked database password, or private details | Low |
| 0012 | SQL Server | If the app connects as `sa` (sysadmin), any app compromise or future SQL injection gets full control of the server | Elevation of Privilege | The development setup uses a single SQL login | EF Core parameterizes queries; the container binds only `127.0.0.1` | A dedicated login with `db_datareader` + `db_datawriter`, plus a separate login for migrations. In production, a managed database reachable only from the app | Low | High: every database on the server | Medium |
| 0013 | GitHub Actions CI | A compromised third-party action, or an over-permissioned token, changes the repo | Tampering | Actions are pinned by moving tags (`@v5`) and the workflow has no `permissions:` block | `npm ci` uses the lockfile; `dotnet-ef` is pinned to 9.0.20; pull requests from forks get a read-only token | Add `permissions: contents: read`; pin actions to commit SHAs; add a `dependabot.yml` for npm, NuGet and actions | Low | Medium: repository integrity | Low |
| 0014 | React frontend | A ticket title or comment containing script runs in a technician's browser (stored XSS) | Tampering | Untrusted user text is shown to privileged users | React escapes text, and nothing in `frontend/src` uses `dangerouslySetInnerHTML` | Keep it that way (lint rule `react/no-danger`); add a Content-Security-Policy header when the API serves the built UI | Low | High if it did happen: technician session takeover | Low |
| 0015 | API ↔ SQL Server | SQL injection through ticket or user fields | Tampering | All input reaches the database | All access goes through EF Core LINQ (parameterized); the migration backfill SQL is static | Never build `FromSqlRaw` strings by concatenation | Low | High | Low |
| 0016 | Ticket and user IDs | Guessing ids to reach other people's tickets | Information Disclosure | Ids appear in URLs | Ids are random GUIDs, so they can't be guessed | No separate control needed: the real fix is authorization (0005), and the list endpoint shows everything anyway | Low | Low | Low |

Notes on threats with no new control: 0015 and 0016 are already covered by the design. Compromise of the developer's machine is out of scope for the application.

# QUESTIONS & ASSUMPTIONS

- Deployment target is still open: a public host (e.g. Azure App Service) or a private network only (VPN / tailnet). A private-network-only deploy would sharply lower the likelihood of 0001–0010 and could serve as a stopgap demo before sign-in is done.
- Audience is still open: real users, or a public demo. A public demo should reset its data on a schedule rather than leave writes open.
- Assumed: development connects with a single SQL login that has broad rights (0012).
- Assumed: only the repository owner can push to `main`.
- Sign-in choice: ASP.NET Core Identity (self-contained) or OIDC with Entra ID.

# CHECKLIST FOR THE SIGN-IN WORK

Pulled from the table above. Each item closes a threat before the app is deployed:

- [ ] Authentication on every endpoint by default (0001)
- [ ] Identity taken from the session, never from the request body: drop `CreatedByUserId` and `AuthorUserId` (0002)
- [ ] Admin-only user creation and role changes (0003)
- [ ] Role and ownership policies on workflow steps, deletes and comments (0004)
- [ ] Ticket and user lists filtered by role; `/deleted` for Admin only (0005)
- [ ] An actor recorded on every activity (0006)
- [ ] CSRF protection if sign-in uses cookies (0008)

Before deploy: rate limiting and paging (0007), Production settings (0009), TLS (0010), a least-privilege database login (0012).
