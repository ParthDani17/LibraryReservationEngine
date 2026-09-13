# Library Reservation & Borrowing Engine — Foundation Commit

This is the "frozen contract" both of you build together **before** splitting
into feature branches. Once this is merged into `develop`, avoid editing
these files independently — coordinate first if either of you needs a change.

## Folder structure

```
src/
├── Domain/
│   ├── Entities/        ← Book, BookCopy, Reservation, WaitlistEntry,
│   │                       Borrowing, Fine, Notification, Author,
│   │                       Category, ApplicationUser
│   └── Enums/            ← BookCopyStatus, ReservationStatus,
│                            WaitlistStatus, BorrowingStatus,
│                            FineStatus, NotificationType
├── Application/
│   ├── Common/            ← Result.cs (success/failure wrapper)
│   └── Interfaces/       ← IBookCopyService, IReservationService,
│                            IWaitlistService, IBorrowingService,
│                            IFineService, INotificationService
├── Infrastructure/
│   └── Data/             ← ApplicationDbContext.cs
├── Web/                    ← (empty — this is where the ASP.NET Core
│                              MVC project itself goes)
tests/                       ← (empty — test project goes here)
```

## Why the interfaces matter

Person A owns `IBookCopyService`, `IReservationService`, `IWaitlistService`.
Person B owns `IBorrowingService`, `IFineService`, `INotificationService`.

Person B's `BorrowingService` can depend on `IBookCopyService` and start
coding immediately — it doesn't need Person A's actual implementation to
exist. You just both need to agree the interface shape is right *before*
freezing this commit.

## What to do with these files (step by step)

1. In Visual Studio: **Create a new project** → ASP.NET Core Web App (MVC) →
   name it `LibraryReservationEngine.Web`, and create three more Class
   Library projects: `LibraryReservationEngine.Domain`,
   `LibraryReservationEngine.Application`,
   `LibraryReservationEngine.Infrastructure` (plus a test project). Put them
   all in one Solution.
2. Copy the `Entities/` and `Enums/` files into the `Domain` project.
3. Copy `Common/` and `Interfaces/` into the `Application` project.
4. Copy `ApplicationDbContext.cs` into the `Infrastructure` project.
5. Add project references: `Application` → `Domain`,
   `Infrastructure` → `Application` + `Domain`,
   `Web` → `Infrastructure` + `Application` + `Domain`.
6. Install NuGet packages (on `Infrastructure` and `Web` as needed):
   - `Microsoft.EntityFrameworkCore.SqlServer`
   - `Microsoft.EntityFrameworkCore.Tools`
   - `Microsoft.AspNetCore.Identity.EntityFrameworkCore`
7. In `Web`'s `appsettings.json`, add your SQL Server connection string, and
   register `ApplicationDbContext` + Identity in `Program.cs`.
8. Run your first migration:
   ```
   dotnet ef migrations add InitialCreate
   dotnet ef database update
   ```
9. Commit everything as one commit: **"Foundation: entities, DbContext,
   shared interfaces, initial migration"**. Open a PR into `develop`, both
   review, both merge.

After that commit is merged, you're both clear to branch off `develop` and
work in complete isolation — see the feature split from earlier
(Person A: catalogue → copies → reservation → waitlist → concurrency;
Person B: auth → borrowing → return → fines → notifications).
