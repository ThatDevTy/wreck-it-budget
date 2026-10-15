/* ============================================================================
   Wreck-It Budget · WIB-7 schema validation
   ----------------------------------------------------------------------------
   Creates a scratch database, builds all six tables, seeds valid rows, then
   deliberately tries to insert bad data to prove each constraint bites.

   Run the whole thing with F5 and read the MESSAGES tab, not Results.
   Every negative test prints OK (rejected) or FAIL (it got through).
   ============================================================================ */

USE master;
GO

IF DB_ID('WreckItBudget_SchemaTest') IS NOT NULL
BEGIN
    ALTER DATABASE WreckItBudget_SchemaTest SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE WreckItBudget_SchemaTest;
END
GO

CREATE DATABASE WreckItBudget_SchemaTest;
GO

USE WreckItBudget_SchemaTest;
GO

/* ---------------------------------------------------------------------------
   TABLES — order matters. A foreign key cannot reference a table that does
   not exist yet, so Users comes before IncomeSources, and Debts and Bills
   come before SpendingLogEntries.
   --------------------------------------------------------------------------- */

CREATE TABLE Users (
    Id           INT          IDENTITY(1,1) PRIMARY KEY,
    Username     VARCHAR(20)  NOT NULL UNIQUE,
    PasswordHash VARCHAR(120) NOT NULL,
    CreatedAt    DATETIME2    NOT NULL DEFAULT SYSUTCDATETIME()
);
GO

CREATE TABLE Bills (
    Id        INT           IDENTITY(1,1) PRIMARY KEY,
    Name      NVARCHAR(50)  NOT NULL UNIQUE,
    AmountDue DECIMAL(10,2) NOT NULL CHECK (AmountDue > 0),
    PayCycle  NVARCHAR(10)  NOT NULL CHECK (PayCycle IN ('monthly', 'weekly', 'yearly')),
    DueDay    TINYINT       NOT NULL CHECK (DueDay BETWEEN 1 AND 31),
    Category  NVARCHAR(50)  NULL
);
GO

CREATE TABLE Debts (
    Id             INT           IDENTITY(1,1) PRIMARY KEY,
    Name           NVARCHAR(50)  NOT NULL UNIQUE,
    CurrentBalance DECIMAL(10,2) NOT NULL CHECK (CurrentBalance >= 0),
    InterestRate   DECIMAL(5,2)  NOT NULL CHECK (InterestRate   >= 0),
    DueDay         TINYINT       NOT NULL CHECK (DueDay       BETWEEN 1 AND 31),
    StatementDay   TINYINT       NULL     CHECK (StatementDay BETWEEN 1 AND 31),
    MinPayment     DECIMAL(10,2) NOT NULL CHECK (MinPayment     >= 0),
    IsCleared      BIT           NOT NULL DEFAULT 0,
    ClearedAt      DATETIME2     NULL,
    Priority       TINYINT       NULL     CHECK (Priority   BETWEEN 1 AND 25),
    ColorIndex     TINYINT       NOT NULL CHECK (ColorIndex BETWEEN 0 AND 24)
);
GO

CREATE TABLE IncomeSources (
    Id             INT           IDENTITY(1,1) PRIMARY KEY,
    Name           NVARCHAR(50)  NOT NULL UNIQUE,
    OwnerUserId    INT           NOT NULL,
    PayType        NVARCHAR(12)  NOT NULL
                                 CHECK (PayType IN ('weekly', 'biweekly', 'semi-monthly')),
    AmountPerCheck DECIMAL(10,2) NOT NULL CHECK (AmountPerCheck > 0),

    PayWeekday     TINYINT       NULL CHECK (PayWeekday BETWEEN 0 AND 6),
    FirstPayDate   DATE          NULL,
    PayDay1        TINYINT       NULL CHECK (PayDay1 BETWEEN 1 AND 31),
    PayDay2        TINYINT       NULL CHECK (PayDay2 BETWEEN 1 AND 31),

    IsActive       BIT           NOT NULL DEFAULT 1,

    CONSTRAINT FK_IncomeSources_Users
        FOREIGN KEY (OwnerUserId) REFERENCES Users(Id),

    CONSTRAINT CK_IncomeSources_PayDayOrder CHECK (PayDay2 > PayDay1),

    CONSTRAINT CK_IncomeSources_Schedule CHECK (
           (PayType = 'weekly'       AND PayWeekday IS NOT NULL AND FirstPayDate IS NULL
                                     AND PayDay1    IS NULL     AND PayDay2      IS NULL)
        OR (PayType = 'biweekly'     AND PayWeekday IS NOT NULL AND FirstPayDate IS NOT NULL
                                     AND PayDay1    IS NULL     AND PayDay2      IS NULL)
        OR (PayType = 'semi-monthly' AND PayWeekday IS NULL     AND FirstPayDate IS NULL
                                     AND PayDay1    IS NOT NULL AND PayDay2      IS NOT NULL)
    )
);
GO

CREATE TABLE SpendingLogEntries (
    Id               INT           IDENTITY(1,1) PRIMARY KEY,
    Category         NVARCHAR(5)   NOT NULL CHECK (Category IN ('debt', 'bill', 'other')),

    DebtId           INT           NULL,
    BillId           INT           NULL,
    Description      NVARCHAR(50)  NULL,

    Direction        NVARCHAR(7)   NULL CHECK (Direction IN ('payment', 'charge')),
    MinPaymentAtTime DECIMAL(10,2) NULL     CHECK (MinPaymentAtTime >= 0),

    PlannedAmount    DECIMAL(10,2) NULL     CHECK (PlannedAmount    >  0),
    ActualAmount     DECIMAL(10,2) NOT NULL CHECK (ActualAmount     >  0),
    Notes            NVARCHAR(500) NULL,

    LoggedByUserId   INT           NOT NULL,
    LoggedAt         DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT FK_SpendingLogEntries_Debts FOREIGN KEY (DebtId)         REFERENCES Debts(Id),
    CONSTRAINT FK_SpendingLogEntries_Bills FOREIGN KEY (BillId)         REFERENCES Bills(Id),
    CONSTRAINT FK_SpendingLogEntries_Users FOREIGN KEY (LoggedByUserId) REFERENCES Users(Id),

    CONSTRAINT CK_SpendingLogEntries_Target CHECK (
           (Category = 'debt'  AND Direction = 'payment'
                AND DebtId IS NOT NULL AND BillId IS NULL
                AND Description IS NULL AND MinPaymentAtTime IS NOT NULL)
        OR (Category = 'debt'  AND Direction = 'charge'
                AND DebtId IS NOT NULL AND BillId IS NULL
                AND Description IS NULL AND MinPaymentAtTime IS NULL)
        OR (Category = 'bill'  AND Direction IS NULL
                AND DebtId IS NULL     AND BillId IS NOT NULL
                AND Description IS NULL AND MinPaymentAtTime IS NULL)
        OR (Category = 'other' AND Direction IS NULL
                AND DebtId IS NULL     AND BillId IS NULL
                AND Description IS NOT NULL AND MinPaymentAtTime IS NULL)
    )
);
GO

CREATE TABLE Snapshots (
    Id                     INT           IDENTITY(1,1) PRIMARY KEY,
    CapturedAt             DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),

    TotalDebtBalance       DECIMAL(10,2) NOT NULL CHECK (TotalDebtBalance       >= 0),
    MonthlyIncome          DECIMAL(10,2) NOT NULL CHECK (MonthlyIncome          >= 0),
    MonthlyBills           DECIMAL(10,2) NOT NULL CHECK (MonthlyBills           >= 0),
    MonthlyMinDebtPayments DECIMAL(10,2) NOT NULL CHECK (MonthlyMinDebtPayments >= 0)
);
GO

PRINT '=== All six tables created ===';
GO

/* ---------------------------------------------------------------------------
   SEED — valid rows. If any of these fail, a constraint is too strict.
   --------------------------------------------------------------------------- */

INSERT INTO Users (Username, PasswordHash) VALUES
    ('ty',     'AQAAAAIAAYagAAAAE-not-a-real-hash'),
    ('spouse', 'AQAAAAIAAYagAAAAE-also-not-real');

INSERT INTO Bills (Name, AmountDue, PayCycle, DueDay, Category) VALUES
    ('Electric', 142.50, 'monthly', 15, 'Utilities'),
    ('Internet',  79.99, 'monthly',  1, NULL);

INSERT INTO Debts (Name, CurrentBalance, InterestRate, DueDay, StatementDay,
                   MinPayment, Priority, ColorIndex) VALUES
    ('Visa',        2400.00, 22.99, 20,   28,  50.00, 1, 0),
    ('Car Loan',   11500.00,  6.40,  5, NULL, 312.00, 2, 1),
    ('0% Promo',     800.00,  0.00, 12, NULL,  25.00, 3, 2);

INSERT INTO IncomeSources (Name, OwnerUserId, PayType, AmountPerCheck,
                           PayWeekday, FirstPayDate, PayDay1, PayDay2) VALUES
    ('Main Job',      1, 'biweekly',     1842.00,    5, '2026-09-11', NULL, NULL),
    ('Spouse Salary', 2, 'semi-monthly', 2100.00, NULL, NULL,            1,   15),
    ('Weekend Gig',   1, 'weekly',        320.00,    5, NULL,         NULL, NULL);

INSERT INTO SpendingLogEntries (Category, DebtId, BillId, Description, Direction,
                                MinPaymentAtTime, PlannedAmount, ActualAmount,
                                LoggedByUserId) VALUES
    ('debt',    1, NULL, NULL,          'payment', 50.00,   75.00,  75.00, 1),
    ('debt',    1, NULL, NULL,          'charge',   NULL,    NULL,  42.10, 2),
    ('bill', NULL,    1, NULL,           NULL,      NULL,  142.50, 142.50, 1),
    ('other',NULL, NULL, 'Car repair',   NULL,      NULL,  200.00, 318.75, 2);

INSERT INTO Snapshots (TotalDebtBalance, MonthlyIncome, MonthlyBills,
                       MonthlyMinDebtPayments) VALUES
    (14700.00, 8184.00, 222.49, 387.00),
    (       0, 8184.00, 222.49,       0);   -- debt-free: zeroes must be allowed

PRINT '=== Seed data inserted ===';
GO

/* ---------------------------------------------------------------------------
   NEGATIVE TESTS — each of these SHOULD fail. TRY/CATCH lets the script keep
   going so you see every result instead of stopping at the first one.
   --------------------------------------------------------------------------- */

PRINT '';
PRINT '=== Negative tests (every line should say OK) ===';
GO

BEGIN TRY
    INSERT INTO Debts (Name, CurrentBalance, InterestRate, DueDay, MinPayment, ColorIndex)
        VALUES ('Bad Color', 100, 10, 1, 10, 25);
    PRINT 'FAIL - ColorIndex 25 was accepted (valid range is 0-24)';
END TRY BEGIN CATCH
    PRINT 'OK   - ColorIndex 25 rejected';
END CATCH
GO

BEGIN TRY
    INSERT INTO IncomeSources (Name, OwnerUserId, PayType, AmountPerCheck, PayWeekday)
        VALUES ('Ghost Job', 999, 'weekly', 100, 5);
    PRINT 'FAIL - OwnerUserId 999 was accepted (no such user)';
END TRY BEGIN CATCH
    PRINT 'OK   - OwnerUserId 999 rejected by the foreign key';
END CATCH
GO

BEGIN TRY
    INSERT INTO IncomeSources (Name, OwnerUserId, PayType, AmountPerCheck, PayWeekday, PayDay1)
        VALUES ('Confused Job', 1, 'weekly', 100, 5, 15);
    PRINT 'FAIL - weekly income with a PayDay1 was accepted';
END TRY BEGIN CATCH
    PRINT 'OK   - weekly income carrying a PayDay1 rejected by the schedule check';
END CATCH
GO

BEGIN TRY
    INSERT INTO IncomeSources (Name, OwnerUserId, PayType, AmountPerCheck, PayDay1, PayDay2)
        VALUES ('Backwards Days', 1, 'semi-monthly', 100, 15, 1);
    PRINT 'FAIL - pay days out of order were accepted';
END TRY BEGIN CATCH
    PRINT 'OK   - PayDay2 before PayDay1 rejected';
END CATCH
GO

BEGIN TRY
    INSERT INTO SpendingLogEntries (Category, DebtId, Direction, ActualAmount, LoggedByUserId)
        VALUES ('debt', 1, 'payment', 60.00, 1);
    PRINT 'FAIL - debt payment with no MinPaymentAtTime was accepted';
END TRY BEGIN CATCH
    PRINT 'OK   - debt payment without MinPaymentAtTime rejected (D-017)';
END CATCH
GO

BEGIN TRY
    INSERT INTO SpendingLogEntries (Category, DebtId, BillId, Direction,
                                    MinPaymentAtTime, ActualAmount, LoggedByUserId)
        VALUES ('debt', 1, 1, 'payment', 50.00, 60.00, 1);
    PRINT 'FAIL - entry targeting both a debt and a bill was accepted';
END TRY BEGIN CATCH
    PRINT 'OK   - entry targeting both a debt and a bill rejected';
END CATCH
GO

BEGIN TRY
    INSERT INTO Bills (Name, AmountDue, PayCycle, DueDay)
        VALUES ('Free Bill', 0, 'monthly', 1);
    PRINT 'FAIL - a $0 bill was accepted';
END TRY BEGIN CATCH
    PRINT 'OK   - $0 bill rejected';
END CATCH
GO

BEGIN TRY
    INSERT INTO Users (Username, PasswordHash) VALUES ('ty', 'duplicate');
    PRINT 'FAIL - duplicate username was accepted';
END TRY BEGIN CATCH
    PRINT 'OK   - duplicate username rejected';
END CATCH
GO

BEGIN TRY
    DELETE FROM Users WHERE Id = 1;
    PRINT 'FAIL - deleted a user who still owns income and log entries';
END TRY BEGIN CATCH
    PRINT 'OK   - deleting a referenced user rejected (no orphans)';
END CATCH
GO

/* ---------------------------------------------------------------------------
   POSITIVE TESTS — these MUST succeed. A schema that rejects everything is as
   broken as one that rejects nothing.
   --------------------------------------------------------------------------- */

PRINT '';
PRINT '=== Positive tests (every line should say OK) ===';
GO

BEGIN TRY
    UPDATE Debts SET CurrentBalance = 0, IsCleared = 1, ClearedAt = SYSUTCDATETIME(),
                     Priority = NULL
    WHERE Name = 'Visa';
    PRINT 'OK   - a debt can be paid down to exactly 0 and cleared';
END TRY BEGIN CATCH
    PRINT 'FAIL - could not pay a debt off: ' + ERROR_MESSAGE();
END CATCH
GO

BEGIN TRY
    INSERT INTO Debts (Name, CurrentBalance, InterestRate, DueDay, MinPayment, ColorIndex)
        VALUES ('Deferred Loan', 5000, 0, 1, 0, 3);
    PRINT 'OK   - a debt with a $0 minimum payment is allowed (deferred loans)';
END TRY BEGIN CATCH
    PRINT 'FAIL - $0 minimum payment rejected: ' + ERROR_MESSAGE();
END CATCH
GO

/* ---------------------------------------------------------------------------
   RESULTS — eyeball the data, then clean up.
   --------------------------------------------------------------------------- */

SELECT 'Users' AS TableName, COUNT(*) AS Rows FROM Users
UNION ALL SELECT 'Bills',              COUNT(*) FROM Bills
UNION ALL SELECT 'Debts',              COUNT(*) FROM Debts
UNION ALL SELECT 'IncomeSources',      COUNT(*) FROM IncomeSources
UNION ALL SELECT 'SpendingLogEntries', COUNT(*) FROM SpendingLogEntries
UNION ALL SELECT 'Snapshots',          COUNT(*) FROM Snapshots;
GO

/* Uncomment to throw the scratch database away when you're done poking at it.

USE master;
GO
ALTER DATABASE WreckItBudget_SchemaTest SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
DROP DATABASE WreckItBudget_SchemaTest;
GO

*/
