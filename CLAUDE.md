# TowerHaat — project context for Claude Code

TowerHaat is a phone-first PWA where residents of an apartment society discover every home business in the society and order from cloud kitchens and product sellers. Launch society: **Ace Divino** (Noida). The data model is multi-society from day one.

Full spec and plan (source of truth for requirement IDs like C-01, B-03, A-02):
- MVP Spec: https://claude.ai/artifact/LE78jh9Ps16tMrbHoziaa2
- Development Plan: https://claude.ai/artifact/GoQUrZD2RsSnbgr5uSP9BC

## Stack

- **Backend:** ASP.NET Core Web API, .NET 10 (LTS), modular monolith, EF Core 10, SignalR
- **Database:** SQL Server (MonsterASP hosting). Keep code provider-neutral: no SQL Server–specific SQL, so switching to PostgreSQL later is a provider change only.
- **Frontend:** Angular (latest), standalone components, PWA (service worker, web push)
- **Auth:** phone + SMS OTP, JWT access + refresh tokens
- **Storage:** images on disk/blob abstraction (`IFileStorage`), so S3 can be added later
- **Hosting:** MonsterASP Premium (Windows/IIS). API serves the Angular build from `wwwroot`. Custom domain `towerhaat.com`, staging on a subdomain.
- **CI:** GitHub Actions — build + test backend and frontend on every push

## Solution layout (target)

```
src/
  TowerHaat.Domain/          entities, enums, domain rules (no EF, no ASP.NET)
  TowerHaat.Application/     use cases, DTOs, validation, interfaces
  TowerHaat.Infrastructure/  EF Core DbContext, migrations, OTP/SMS, push, file storage
  TowerHaat.Api/             endpoints, auth, SignalR hubs, hosts Angular build
tests/
  TowerHaat.Tests/           xUnit: domain + application + tenancy tests
web/                         Angular PWA
```

## Non-negotiable rules

1. **Tenancy:** every society-owned entity implements `ITenantEntity` with `SocietyId`, including child rows (order items, payments, status history, catalog items, follows, enquiries…), not only the parent. `AppDbContext` applies the named query filter `AppDbContext.TenancyFilter` from `ICurrentSociety`, and on save stamps new rows with the current society and refuses writes to another society. To work in a society other than the user's own (the public sign-up picker, platform admin screens), call `ISocietyScope.Use(societyId)`: the filter stays on and points at that society; residents get 403 for a society that is not theirs. The filter may be ignored, explicitly, with `IgnoreQueryFilters([TenancyFilter.Name])`, only in platform admin endpoints and in `MeService.LoadOwnProfileAsync` (reading the user's own profile to put their society in the token). Entities shared by all societies (Society, User, BusinessType…) must be added to the allow-list in `TenancyModelTests`.
2. **Money never flows through the platform.** UPI is a direct deep link/QR to the business's UPI ID; COD is cash. Platform only tracks payment status.
3. **Snapshots:** orders copy item name, price and delivery address at order time.
4. **Privacy:** a business sees a customer's phone and address only on that customer's orders with that business.
5. **Order status flow:** Placed → Accepted → Preparing → OutForDelivery → Delivered; closed states Rejected, Expired, CancelledByCustomer (only while Placed), CancelledByOwner (reason required). UPI payment is shown only after Accepted.
6. **Idempotency key** on order creation.
7. Business types and categories are data (admin-managed), never hardcoded enums.

## Decisions that refine the spec

- **Tenancy columns:** the spec's data model omits `SocietyId` on child tables; rule 1 above wins, so they get it.
- **Images:** stored on disk behind `IFileStorage` (the spec's "Stored in S3" is superseded).
- **Auto-expiry (M3):** IIS on MonsterASP can unload the app when idle, so expiry must not rely only on an in-process `BackgroundService`. Run it from a MonsterASP scheduled task calling a protected endpoint, and also treat a Placed order older than 30 minutes as Expired when it is read.
- **SMS (until DLT approval):** `LoggingSmsSender` writes OTPs to the log; in Development the API also returns the code and the login screen shows it. Swap in a real `ISmsSender` before the pilot (M6); nothing else changes.
- **Sessions:** 30-minute JWT access token kept in memory by the PWA; 60-day refresh token in an HTTP-only, Secure, SameSite=Strict cookie on `/api/auth`, rotated on every use, and reuse of a rotated token ends all the user's sessions.
- **Platform admins:** phones listed in `Auth:AdminPhones` become admins when they sign in.
- **Consent (DPDP):** asked at sign-up in M1 rather than M6, stored as `ResidentProfile.ConsentedAt`.
- **Photos (M2):** resized in the browser (longest side 1600 px, JPEG) before upload; the server accepts JPEG, PNG or WebP up to 2 MB, checked by their first bytes, and stores them through `IFileStorage` (`DiskFileStorage` under `Storage:RootPath`, default `App_Data/uploads`). `/media/{key}` serves them publicly with a one-year cache, since image tags cannot send a token and keys are random.
- **Businesses (M2):** one business per owner; Pending until a platform admin approves it; a rejected business goes back to Pending when the owner edits it; deactivated businesses cannot be edited. Settings (hours, last order time, minimum order, delivery charge) are columns on `Business`, not a separate table. Hours are daily, in Indian time, and may pass midnight; opening and closing at the same time means open all day.
- **Sold out (M2 → M3):** `SoldToday.ForBusinessAsync` returns nothing sold until M3 counts today's order items there; daily limits and "sold out" already use it.
- **Payment statuses (M4):** include `Disputed` as an owner action alongside confirm, COD collected and refunded (B-08 omits it; the spec's payment table and edge cases include it).

## Conventions

- C# nullable enabled and warnings as errors (`Directory.Build.props`), package versions in `Directory.Packages.props`, file-scoped namespaces, async all the way, `CancellationToken` on public async APIs.
- Minimal APIs grouped per module (`MapGroup("/api/orders")`).
- Ids are `Guid.CreateVersion7()` (set in `Entity`). Stored times are UTC `DateTime` (SQLite cannot compare `DateTimeOffset`); `TimeProvider` gives the clock so tests can move it.
- Use cases live in Application services (`AuthService`, `MeService`, …) over `IAppDbContext`; endpoints stay thin. Expected failures throw `AppException(ErrorKind, message)`, which the API turns into problem details whose `detail` the PWA shows as is.
- Tests run against SQLite in memory (`TestDatabase`) so queries go through real SQL translation; keep the model provider-neutral so this keeps working.
- Every business rule gets a unit test. Tenancy and order rules must have tests before merge.
- Migrations live in `Infrastructure/Persistence/Migrations`; name them by feature (`AddOrders`). CI fails if the model changes without a migration.
- Angular: standalone components, signals, OnPush, lazy routes per area (`customer`, `owner`, `admin`), mobile-first SCSS with the CSS variables in `web/src/styles.scss`. `ng build` writes into `src/TowerHaat.Api/wwwroot` (gitignored). Prettier formats `web/src`. Forms that use `(ngSubmit)` need `[formGroup]`, otherwise the browser submits and reloads the page.
- Times stored in UTC; display in Asia/Kolkata (`IndiaTime` on the server, business hours are compared in Indian time). Money is `decimal(10,2)` rupees.
- Reference requirement IDs in commit messages and PR titles (e.g. `M3: order creation (C-06)`).

## Milestones

M0 Foundation → M1 Login and residents → M2 Businesses and catalog → M3 Ordering → M4 Payments → M5 Live alerts → M6 Pilot launch → M7 Full MVP. Work milestone by milestone; finish tests before moving on.
