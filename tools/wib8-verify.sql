/* ============================================================================
   WIB-8 verification - does the EF-generated database enforce what schema.md says?

   Runs against the REAL database created by `dotnet ef database update`.
   Seeds a little data, then tries to insert garbage and confirms each
   constraint rejects it. Cleans up after itself.
   ============================================================================ */

USE WreckItBudget;
GO

SET NOCOUNT ON;
GO

PRINT '=== Tables created by EF ===';
SELECT name FROM sys.tables WHERE name <> '__EFMigrationsHistory' ORDER BY name;
GO

PRINT '';
PRINT '=== Constraint counts (should match schema.md) ===';
SELECT
    (SELECT COUNT(*) FROM sys.check_constraints)                          AS CheckConstraints,
    (SELECT COUNT(*) FROM sys.foreign_keys)                               AS ForeignKeys,
    (SELECT COUNT(*) FROM sys.indexes i JOIN sys.tables t ON t.object_id = i.object_id
      WHERE i.is_unique = 1 AND i.is_primary_key = 0)                      AS UniqueIndexes;
GO

/* ----------------------------------------------------------------- seed */
DELETE FROM SpendingLogEntries;
DELETE FROM Snapshots;
DELETE FROM IncomeSources;
DELETE FROM Debts;
DELETE FROM Bills;
DELETE FROM Users;
GO

INSERT INTO Users (Username, PasswordHash) VALUES ('ty', 'not-a-real-hash'), ('spouse', 'nope');
INSERT INTO Bills (Name, AmountDue, PayCycle, DueDay) VALUES ('Electric', 142.50, 'monthly', 15);
INSERT INTO Debts (Name, CurrentBalance, InterestRate, DueDay, MinPayment, IsCleared, ColorIndex)
     VALUES ('Visa', 2400.00, 22.99, 20, 50.00, 0, 0);
GO

DECLARE @u INT = (SELECT MIN(Id) FROM Users);
DECLARE @d INT = (SELECT MIN(Id) FROM Debts);
DECLARE @b INT = (SELECT MIN(Id) FROM Bills);

PRINT '';
PRINT '=== Negative tests (every line should say OK) ===';

BEGIN TRY
    INSERT INTO Debts (Name, CurrentBalance, InterestRate, DueDay, MinPayment, IsCleared, ColorIndex)
    VALUES ('Bad Color', 100, 10, 1, 10, 0, 25);
    PRINT 'FAIL - ColorIndex 25 accepted';
END TRY BEGIN CATCH PRINT 'OK   - ColorIndex 25 rejected'; END CATCH

BEGIN TRY
    INSERT INTO IncomeSources (Name, OwnerUserId, PayType, AmountPerCheck, PayWeekday, IsActive)
    VALUES ('Ghost Job', 999999, 'weekly', 100, 5, 1);
    PRINT 'FAIL - nonexistent OwnerUserId accepted';
END TRY BEGIN CATCH PRINT 'OK   - nonexistent OwnerUserId rejected by the foreign key'; END CATCH

BEGIN TRY
    INSERT INTO IncomeSources (Name, OwnerUserId, PayType, AmountPerCheck, PayWeekday, PayDay1, IsActive)
    VALUES ('Confused Job', @u, 'weekly', 100, 5, 15, 1);
    PRINT 'FAIL - weekly income carrying a PayDay1 accepted';
END TRY BEGIN CATCH PRINT 'OK   - weekly income with a PayDay1 rejected by the schedule check'; END CATCH

BEGIN TRY
    INSERT INTO IncomeSources (Name, OwnerUserId, PayType, AmountPerCheck, PayDay1, PayDay2, IsActive)
    VALUES ('Backwards Days', @u, 'semi-monthly', 100, 15, 1, 1);
    PRINT 'FAIL - pay days out of order accepted';
END TRY BEGIN CATCH PRINT 'OK   - PayDay2 before PayDay1 rejected'; END CATCH

BEGIN TRY
    INSERT INTO SpendingLogEntries (Category, DebtId, Direction, ActualAmount, LoggedByUserId)
    VALUES ('debt', @d, 'payment', 60.00, @u);
    PRINT 'FAIL - debt payment with no MinPaymentAtTime accepted';
END TRY BEGIN CATCH PRINT 'OK   - debt payment without MinPaymentAtTime rejected (D-017)'; END CATCH

BEGIN TRY
    INSERT INTO SpendingLogEntries (Category, DebtId, BillId, Direction, MinPaymentAtTime, ActualAmount, LoggedByUserId)
    VALUES ('debt', @d, @b, 'payment', 50.00, 60.00, @u);
    PRINT 'FAIL - entry targeting both a debt and a bill accepted';
END TRY BEGIN CATCH PRINT 'OK   - entry targeting both a debt and a bill rejected'; END CATCH

BEGIN TRY
    INSERT INTO Bills (Name, AmountDue, PayCycle, DueDay) VALUES ('Free Bill', 0, 'monthly', 1);
    PRINT 'FAIL - $0 bill accepted';
END TRY BEGIN CATCH PRINT 'OK   - $0 bill rejected'; END CATCH

BEGIN TRY
    INSERT INTO Users (Username, PasswordHash) VALUES ('ty', 'duplicate');
    PRINT 'FAIL - duplicate username accepted';
END TRY BEGIN CATCH PRINT 'OK   - duplicate username rejected by the unique index'; END CATCH

PRINT '';
PRINT '=== Positive tests (every line should say OK) ===';

BEGIN TRY
    UPDATE Debts SET CurrentBalance = 0, IsCleared = 1, ClearedAt = SYSUTCDATETIME(), Priority = NULL
    WHERE Id = @d;
    PRINT 'OK   - a debt can be paid down to exactly 0 and cleared';
END TRY BEGIN CATCH PRINT 'FAIL - could not pay a debt off: ' + ERROR_MESSAGE(); END CATCH

BEGIN TRY
    INSERT INTO Debts (Name, CurrentBalance, InterestRate, DueDay, MinPayment, IsCleared, ColorIndex)
    VALUES ('Deferred Loan', 5000, 0, 1, 0, 0, 3);
    PRINT 'OK   - a debt with a $0 minimum payment is allowed';
END TRY BEGIN CATCH PRINT 'FAIL - $0 minimum rejected: ' + ERROR_MESSAGE(); END CATCH

BEGIN TRY
    INSERT INTO IncomeSources (Name, OwnerUserId, PayType, AmountPerCheck, PayWeekday, FirstPayDate, IsActive)
    VALUES ('Main Job', @u, 'biweekly', 1842.00, 5, '2026-09-11', 1);
    PRINT 'OK   - a valid biweekly income source is accepted';
END TRY BEGIN CATCH PRINT 'FAIL - valid biweekly rejected: ' + ERROR_MESSAGE(); END CATCH

-- This one must run AFTER the income source above exists, or there is
-- nothing referencing the user and the delete is correctly allowed.
PRINT '';
PRINT '=== Orphan protection (runs last, once the user owns a row) ===';
BEGIN TRY
    DELETE FROM Users WHERE Id = @u;
    PRINT 'FAIL - deleted a user who still owns rows';
END TRY BEGIN CATCH PRINT 'OK   - deleting a referenced user rejected (Restrict, not Cascade)'; END CATCH
GO

PRINT '';
PRINT '=== Column types: precision must match schema.md ===';
SELECT
    t.name AS TableName,
    c.name AS ColumnName,
    ty.name AS DataType,
    CASE WHEN ty.name = 'decimal' THEN CONCAT(c.precision, ',', c.scale) ELSE CAST(c.max_length AS VARCHAR) END AS Detail
FROM sys.columns c
JOIN sys.tables  t  ON t.object_id = c.object_id
JOIN sys.types   ty ON ty.user_type_id = c.user_type_id
WHERE ty.name IN ('decimal','varchar','nvarchar','tinyint')
  AND t.name <> '__EFMigrationsHistory'
ORDER BY t.name, c.column_id;
GO

/* ------------------------------------------------------------- clean up */
DELETE FROM SpendingLogEntries;
DELETE FROM Snapshots;
DELETE FROM IncomeSources;
DELETE FROM Debts;
DELETE FROM Bills;
DELETE FROM Users;
GO

PRINT '';
PRINT '=== Test data removed. Database is empty and ready for Setup. ===';
GO
