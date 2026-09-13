# Library Reservation Engine — Module Pseudocode

Reference doc for both of you while coding. Each module maps to the entities
and interfaces already frozen in `develop` from the Foundation Commit.

---

# PERSON A — Book & Reservation Side

## 1. `feature/book-catalogue`

```
CreateBook(title, isbn, authorName, categoryName):
    author ← FindOrCreateAuthor(authorName)
    category ← FindOrCreateCategory(categoryName)
    book ← new Book { Title, ISBN, AuthorId = author.Id, CategoryId = category.Id }
    Save(book)
    return book

SearchBooks(query):
    return Books.Where(b => b.Title CONTAINS query
                          OR b.Author.Name CONTAINS query
                          OR b.Category.Name CONTAINS query)

GetBookDetails(bookId):
    book ← Books.Include(BookCopies).FirstOrDefault(Id == bookId)
    if book is null: return NotFound
    availableCount ← book.BookCopies.Count(c => c.Status == Available)
    return { book, availableCount, totalCopies: book.BookCopies.Count }

UpdateBook(bookId, changes): ...
DeleteBook(bookId):
    if book.BookCopies.Any(c => c.Status != Available):
        return Fail("Cannot delete: copies are reserved/borrowed")
    Delete(book)
```

**Librarian-only actions:** Create, Update, Delete.
**Member-facing:** Search, GetBookDetails.

---

## 2. `feature/book-copies`

Implements `IBookCopyService` (the interface Person B's Borrowing/Return code
already depends on).

```
AddCopy(bookId, copyCode):
    copy ← new BookCopy { BookId, CopyCode, Status = Available }
    Save(copy)
    return copy

FindAvailableCopyIdAsync(bookId):
    copy ← BookCopies.FirstOrDefault(c => c.BookId == bookId
                                        AND c.Status == Available)
    return copy?.Id  // null if none available

MarkAsReservedAsync(copyId):
    copy ← BookCopies.Find(copyId)
    if copy.Status != Available: return false
    copy.Status ← Reserved
    Save(copy)  // EF Core checks RowVersion here — see Concurrency module
    return true

MarkAsBorrowedAsync(copyId):
    copy.Status ← Borrowed
    Save(copy)
    return true

MarkAsAvailableAsync(copyId):
    copy.Status ← Available
    Save(copy)
    // After this, trigger waitlist check (see Waitlist module)
    return true

SetMaintenance(copyId): copy.Status ← Maintenance
```

---

## 3. `feature/reservation` ⭐

Implements `IReservationService`.

```
CreateReservationAsync(userId, bookId):
    // Step 1 — validate member
    if not IsActiveMember(userId): return Fail("Account not in good standing")

    // Step 2 — check existing reservation for same book
    existing ← Reservations.Any(r => r.UserId == userId
                                   AND r.BookId == bookId
                                   AND r.Status IN (Pending, Active))
    if existing: return Fail("You already have a reservation for this book")

    // Step 3 — find available copy
    copyId ← BookCopyService.FindAvailableCopyIdAsync(bookId)

    if copyId is null:
        // No copy free → redirect to waitlist
        return WaitlistService.JoinWaitlistAsync(userId, bookId)

    // Step 4 — attempt to claim it (concurrency-safe, see Concurrency module)
    marked ← BookCopyService.MarkAsReservedAsync(copyId)
    if not marked:
        // Someone else grabbed it between step 3 and step 4 — retry once
        return CreateReservationAsync(userId, bookId)  // or fall to waitlist after N retries

    // Step 5 — create reservation record
    reservation ← new Reservation {
        UserId, BookId, BookCopyId = copyId,
        Status = Active,
        ExpiresAt = Now + configured hold period (e.g. 24h)
    }
    Save(reservation)
    NotificationService.SendAsync(userId, ReservationReady, "Your reservation is ready for pickup")
    return Ok(reservation)

CancelReservationAsync(reservationId, userId):
    reservation ← Reservations.Find(reservationId)
    if reservation.UserId != userId: return Fail("Not your reservation")
    reservation.Status ← Cancelled
    BookCopyService.MarkAsAvailableAsync(reservation.BookCopyId)
    Save(reservation)
    WaitlistService.PromoteNextInLineAsync(reservation.BookId)
    return Ok()

GetMyReservationsAsync(userId):
    return Reservations.Where(r => r.UserId == userId).OrderByDescending(CreatedAt)
```

---

## 4. `feature/waitlist`

Implements `IWaitlistService`.

```
JoinWaitlistAsync(userId, bookId):
    already ← WaitlistEntries.Any(w => w.UserId == userId
                                     AND w.BookId == bookId
                                     AND w.Status == Waiting)
    if already: return Fail("Already on the waitlist for this book")

    lastPosition ← WaitlistEntries.Where(BookId == bookId, Status == Waiting)
                                   .Max(Position) ?? 0
    entry ← new WaitlistEntry { UserId, BookId, Position = lastPosition + 1 }
    Save(entry)
    return Ok(entry)

LeaveWaitlistAsync(waitlistEntryId, userId):
    entry ← WaitlistEntries.Find(waitlistEntryId)
    if entry.UserId != userId: return Fail("Not your waitlist entry")
    entry.Status ← Cancelled
    Save(entry)
    // Shift everyone below down by one position
    ReorderPositionsAfter(entry.BookId, entry.Position)
    return Ok()

PromoteNextInLineAsync(bookId):
    next ← WaitlistEntries.Where(BookId == bookId, Status == Waiting)
                           .OrderBy(Position)
                           .FirstOrDefault()
    if next is null: return  // nobody waiting, copy just stays Available

    copyId ← BookCopyService.FindAvailableCopyIdAsync(bookId)
    BookCopyService.MarkAsReservedAsync(copyId)

    reservation ← new Reservation {
        UserId = next.UserId, BookId, BookCopyId = copyId,
        Status = Active, ExpiresAt = Now + hold period
    }
    Save(reservation)

    next.Status ← Promoted
    Save(next)
    ReorderPositionsAfter(bookId, next.Position)

    NotificationService.SendAsync(next.UserId, WaitlistPromoted, "A copy is now reserved for you")
```

---

## 5. `feature/concurrency` ⭐

This isn't really a separate module with new entities — it's a set of
safeguards layered onto the Reservation + BookCopy logic above.

```
// Approach 1 — Optimistic concurrency (uses BookCopy.RowVersion, already in DbContext)
MarkAsReservedAsync(copyId):
    try:
        copy ← BookCopies.Find(copyId)
        if copy.Status != Available: return false
        copy.Status ← Reserved
        context.SaveChanges()   // EF Core compares RowVersion automatically
        return true
    catch DbUpdateConcurrencyException:
        // another request updated this row first — this request loses
        return false

// Approach 2 — Database transaction + row lock (alternative/complementary)
BEGIN TRANSACTION
    SELECT copy WITH (UPDLOCK, ROWLOCK) WHERE Id = copyId AND Status = Available
    if no row returned: ROLLBACK, return Fail
    UPDATE copy SET Status = Reserved
COMMIT

// Test scenario to actually write in feature/testing later:
SIMULATE:
    Task A: ReserveBook(bookId) for Rahul
    Task B: ReserveBook(bookId) for Jay
    RUN both concurrently (e.g. Task.WhenAll / parallel test threads)
    ASSERT exactly one succeeds, the other gets Fail or Waitlisted
```

---

# PERSON B — Auth & Borrowing Side

## 1. `feature/authentication`

```
RegisterUser(fullName, email, password, role):
    user ← new ApplicationUser { FullName, Email = email, UserName = email }
    result ← UserManager.CreateAsync(user, password)
    if not result.Succeeded: return Fail(result.Errors)
    UserManager.AddToRoleAsync(user, role)  // "Member" or "Librarian"
    return Ok(user)

LoginUser(email, password):
    result ← SignInManager.PasswordSignInAsync(email, password)
    if not result.Succeeded: return Fail("Invalid credentials")
    return Ok()

LogoutUser():
    SignInManager.SignOutAsync()

SeedRolesAndAdmin():  // run once at startup
    if not RoleExists("Member"): CreateRole("Member")
    if not RoleExists("Librarian"): CreateRole("Librarian")
    if no user has role Librarian:
        RegisterUser("Admin", "librarian@test.com", "SeedPassword123!", "Librarian")

// Controller/page level:
[Authorize(Roles = "Librarian")]
AddBookController(...)   // only librarians can reach this

[Authorize(Roles = "Member")]
ReserveBookController(...)
```

---

## 2. `feature/borrowing`

Implements `IBorrowingService`. Depends on `IBookCopyService` (Person A's
contract, not their code).

```
IssueBookAsync(userId, reservationId):
    reservation ← Reservations.Find(reservationId)
    if reservation is null OR reservation.Status != Active:
        return Fail("No active reservation found")

    activeBorrowCount ← Borrowings.Count(b => b.UserId == userId AND b.Status == Active)
    if activeBorrowCount >= MAX_BORROW_LIMIT (e.g. 5):
        return Fail("Borrowing limit reached")

    borrowing ← new Borrowing {
        UserId, BookCopyId = reservation.BookCopyId,
        IssuedAt = Now, DueDate = Now + loanPeriod (e.g. 14 days),
        Status = Active
    }
    Save(borrowing)

    BookCopyService.MarkAsBorrowedAsync(reservation.BookCopyId)

    reservation.Status ← Fulfilled
    Save(reservation)

    return Ok(borrowing)

GetMyBorrowingsAsync(userId):
    return Borrowings.Where(b => b.UserId == userId).OrderByDescending(IssuedAt)
```

---

## 3. `feature/return`

```
ReturnBookAsync(borrowingId):
    borrowing ← Borrowings.Find(borrowingId)
    if borrowing is null OR borrowing.Status != Active:
        return Fail("Not an active borrowing")

    borrowing.ReturnedAt ← Now
    isLate ← borrowing.ReturnedAt > borrowing.DueDate

    if isLate:
        borrowing.Status ← Overdue  // or keep Returned + flag, per your enum design
        fineAmount ← FineService.CalculateFineAsync(borrowingId)
    else:
        borrowing.Status ← Returned

    Save(borrowing)

    BookCopyService.MarkAsAvailableAsync(borrowing.BookCopyId)

    // trigger waitlist promotion for that book
    bookId ← BookCopies.Find(borrowing.BookCopyId).BookId
    WaitlistService.PromoteNextInLineAsync(bookId)

    return Ok(borrowing)
```

---

## 4. `feature/fines`

Implements `IFineService`.

```
CalculateFineAsync(borrowingId):
    borrowing ← Borrowings.Find(borrowingId)
    overdueDays ← (borrowing.ReturnedAt - borrowing.DueDate).Days
    if overdueDays <= 0: return 0

    ratePerDay ← 5.00  // ₹5/day, could later move to a config entity
    amount ← overdueDays * ratePerDay

    fine ← new Fine {
        BorrowingId = borrowingId,
        Amount = amount,
        OverdueDays = overdueDays,
        Status = Unpaid
    }
    Save(fine)

    NotificationService.SendAsync(borrowing.UserId, FineIssued,
        $"You have a fine of ₹{amount} for {overdueDays} overdue days")

    return amount

MarkFineAsPaidAsync(fineId):
    fine ← Fines.Find(fineId)
    fine.Status ← Paid
    Save(fine)
    return Ok()
```

---

## 5. `feature/notifications`

Implements `INotificationService`.

```
SendAsync(userId, type, message):
    notification ← new Notification {
        UserId, Type = type, Message = message,
        IsRead = false, CreatedAt = Now
    }
    Save(notification)
    // Optional later: also push email/SMS here

GetMyNotificationsAsync(userId):
    return Notifications.Where(n => n.UserId == userId)
                         .OrderByDescending(CreatedAt)

MarkAsReadAsync(notificationId):
    notification ← Notifications.Find(notificationId)
    notification.IsRead ← true
    Save(notification)

// Called from Background Service (Phase 10) — not its own feature branch,
// but worth noting: this is where "reservation expiring soon" reminders
// and "book overdue" reminders will originate from.
```

---

## Cross-cutting note: Background Service (Phase 10)

Not a `feature/` branch of its own — build it after both sides' core logic
is merged, since it calls into both `IReservationService` (expire stale
reservations) and `IBorrowingService`/`INotificationService` (overdue
reminders). Rough shape:

```
class ExpirationBackgroundService : BackgroundService:
    ExecuteAsync(stoppingToken):
        while not stoppingToken.IsCancellationRequested:
            expired ← Reservations.Where(r => r.Status == Active
                                            AND r.ExpiresAt < Now)
            for each reservation in expired:
                reservation.Status ← Expired
                BookCopyService.MarkAsAvailableAsync(reservation.BookCopyId)
                WaitlistService.PromoteNextInLineAsync(reservation.BookId)
            Save()
            await Task.Delay(TimeSpan.FromMinutes(15))
```
