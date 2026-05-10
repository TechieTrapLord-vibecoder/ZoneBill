IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260402104804_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260402104804_InitialCreate', N'8.0.26');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260402110818_AddEntireSchema'
)
BEGIN
    CREATE TABLE [SubscriptionPlans] (
        [PlanId] int NOT NULL IDENTITY,
        [PlanName] nvarchar(50) NOT NULL,
        [MonthlyPrice] decimal(18,2) NOT NULL,
        [MaxTablesAllowed] int NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_SubscriptionPlans] PRIMARY KEY ([PlanId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260402110818_AddEntireSchema'
)
BEGIN
    CREATE TABLE [Businesses] (
        [BusinessId] int NOT NULL IDENTITY,
        [PlanId] int NOT NULL,
        [BusinessName] nvarchar(100) NOT NULL,
        [DomainPrefix] nvarchar(50) NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (GETDATE()),
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Businesses] PRIMARY KEY ([BusinessId]),
        CONSTRAINT [FK_Businesses_SubscriptionPlans_PlanId] FOREIGN KEY ([PlanId]) REFERENCES [SubscriptionPlans] ([PlanId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260402110818_AddEntireSchema'
)
BEGIN
    CREATE TABLE [ChartOfAccounts] (
        [AccountId] int NOT NULL IDENTITY,
        [BusinessId] int NOT NULL,
        [AccountName] nvarchar(100) NOT NULL,
        [AccountType] nvarchar(50) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_ChartOfAccounts] PRIMARY KEY ([AccountId]),
        CONSTRAINT [FK_ChartOfAccounts_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([BusinessId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260402110818_AddEntireSchema'
)
BEGIN
    CREATE TABLE [Customers] (
        [CustomerId] int NOT NULL IDENTITY,
        [BusinessId] int NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Email] nvarchar(256) NULL,
        [Phone] nvarchar(20) NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (GETDATE()),
        CONSTRAINT [PK_Customers] PRIMARY KEY ([CustomerId]),
        CONSTRAINT [FK_Customers_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([BusinessId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260402110818_AddEntireSchema'
)
BEGIN
    CREATE TABLE [JournalEntries] (
        [JournalEntryId] int NOT NULL IDENTITY,
        [BusinessId] int NOT NULL,
        [ReferenceId] int NULL,
        [ReferenceType] nvarchar(50) NULL,
        [EntryDate] datetime2 NOT NULL DEFAULT (GETDATE()),
        [Description] nvarchar(255) NULL,
        CONSTRAINT [PK_JournalEntries] PRIMARY KEY ([JournalEntryId]),
        CONSTRAINT [FK_JournalEntries_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([BusinessId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260402110818_AddEntireSchema'
)
BEGIN
    CREATE TABLE [MenuItems] (
        [ItemId] int NOT NULL IDENTITY,
        [BusinessId] int NOT NULL,
        [ItemName] nvarchar(100) NOT NULL,
        [CurrentPrice] decimal(18,2) NOT NULL,
        [StockAvailable] int NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_MenuItems] PRIMARY KEY ([ItemId]),
        CONSTRAINT [FK_MenuItems_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([BusinessId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260402110818_AddEntireSchema'
)
BEGIN
    CREATE TABLE [Spaces] (
        [SpaceId] int NOT NULL IDENTITY,
        [BusinessId] int NOT NULL,
        [SpaceName] nvarchar(50) NOT NULL,
        [CurrentHourlyRate] decimal(18,2) NOT NULL,
        [CurrentStatus] nvarchar(20) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Spaces] PRIMARY KEY ([SpaceId]),
        CONSTRAINT [FK_Spaces_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([BusinessId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260402110818_AddEntireSchema'
)
BEGIN
    CREATE TABLE [Users] (
        [UserId] int NOT NULL IDENTITY,
        [BusinessId] int NULL,
        [UserRole] nvarchar(20) NOT NULL,
        [FirstName] nvarchar(50) NOT NULL,
        [LastName] nvarchar(50) NOT NULL,
        [EmailAddress] nvarchar(256) NOT NULL,
        [PasswordHash] nvarchar(max) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([UserId]),
        CONSTRAINT [FK_Users_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([BusinessId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260402110818_AddEntireSchema'
)
BEGIN
    CREATE TABLE [JournalEntryLines] (
        [JournalLineId] int NOT NULL IDENTITY,
        [JournalEntryId] int NOT NULL,
        [AccountId] int NOT NULL,
        [Debit] decimal(18,2) NOT NULL,
        [Credit] decimal(18,2) NOT NULL,
        CONSTRAINT [PK_JournalEntryLines] PRIMARY KEY ([JournalLineId]),
        CONSTRAINT [FK_JournalEntryLines_ChartOfAccounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [ChartOfAccounts] ([AccountId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_JournalEntryLines_JournalEntries_JournalEntryId] FOREIGN KEY ([JournalEntryId]) REFERENCES [JournalEntries] ([JournalEntryId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260402110818_AddEntireSchema'
)
BEGIN
    CREATE TABLE [Bookings] (
        [BookingId] int NOT NULL IDENTITY,
        [BusinessId] int NOT NULL,
        [SpaceId] int NOT NULL,
        [CustomerId] int NULL,
        [StartTime] datetime2 NOT NULL,
        [EndTime] datetime2 NULL,
        [DurationHours] decimal(10,2) NULL,
        [LockedHourlyRate] decimal(18,2) NOT NULL,
        [BookingStatus] nvarchar(20) NOT NULL,
        [ReferenceCode] nvarchar(20) NULL,
        CONSTRAINT [PK_Bookings] PRIMARY KEY ([BookingId]),
        CONSTRAINT [FK_Bookings_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([BusinessId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Bookings_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([CustomerId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Bookings_Spaces_SpaceId] FOREIGN KEY ([SpaceId]) REFERENCES [Spaces] ([SpaceId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260402110818_AddEntireSchema'
)
BEGIN
    CREATE TABLE [Invoices] (
        [InvoiceId] int NOT NULL IDENTITY,
        [BusinessId] int NOT NULL,
        [BookingId] int NOT NULL,
        [TotalAmount] decimal(18,2) NOT NULL,
        [TaxRateApplied] decimal(5,4) NOT NULL,
        [PaymentStatus] nvarchar(20) NOT NULL,
        [GeneratedDate] datetime2 NOT NULL DEFAULT (GETDATE()),
        CONSTRAINT [PK_Invoices] PRIMARY KEY ([InvoiceId]),
        CONSTRAINT [FK_Invoices_Bookings_BookingId] FOREIGN KEY ([BookingId]) REFERENCES [Bookings] ([BookingId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Invoices_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([BusinessId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260402110818_AddEntireSchema'
)
BEGIN
    CREATE TABLE [Orders] (
        [OrderId] int NOT NULL IDENTITY,
        [BusinessId] int NOT NULL,
        [BookingId] int NOT NULL,
        [CashierId] int NOT NULL,
        [OrderTime] datetime2 NOT NULL DEFAULT (GETDATE()),
        CONSTRAINT [PK_Orders] PRIMARY KEY ([OrderId]),
        CONSTRAINT [FK_Orders_Bookings_BookingId] FOREIGN KEY ([BookingId]) REFERENCES [Bookings] ([BookingId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Orders_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([BusinessId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Orders_Users_CashierId] FOREIGN KEY ([CashierId]) REFERENCES [Users] ([UserId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260402110818_AddEntireSchema'
)
BEGIN
    CREATE TABLE [Adjustments] (
        [AdjustmentId] int NOT NULL IDENTITY,
        [InvoiceId] int NOT NULL,
        [AdjustmentType] nvarchar(10) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Reason] nvarchar(255) NULL,
        CONSTRAINT [PK_Adjustments] PRIMARY KEY ([AdjustmentId]),
        CONSTRAINT [FK_Adjustments_Invoices_InvoiceId] FOREIGN KEY ([InvoiceId]) REFERENCES [Invoices] ([InvoiceId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260402110818_AddEntireSchema'
)
BEGIN
    CREATE TABLE [InvoiceItems] (
        [InvoiceItemId] int NOT NULL IDENTITY,
        [InvoiceId] int NOT NULL,
        [ItemType] nvarchar(20) NOT NULL,
        [Description] nvarchar(100) NOT NULL,
        [Quantity] int NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        [Total] decimal(18,2) NOT NULL,
        CONSTRAINT [PK_InvoiceItems] PRIMARY KEY ([InvoiceItemId]),
        CONSTRAINT [FK_InvoiceItems_Invoices_InvoiceId] FOREIGN KEY ([InvoiceId]) REFERENCES [Invoices] ([InvoiceId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260402110818_AddEntireSchema'
)
BEGIN
    CREATE TABLE [Payments] (
        [PaymentId] int NOT NULL IDENTITY,
        [BusinessId] int NOT NULL,
        [InvoiceId] int NOT NULL,
        [AmountPaid] decimal(18,2) NOT NULL,
        [PaymentMethod] nvarchar(50) NOT NULL,
        [PaymentDate] datetime2 NOT NULL DEFAULT (GETDATE()),
        [ReferenceNumber] nvarchar(100) NULL,
        CONSTRAINT [PK_Payments] PRIMARY KEY ([PaymentId]),
        CONSTRAINT [FK_Payments_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([BusinessId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Payments_Invoices_InvoiceId] FOREIGN KEY ([InvoiceId]) REFERENCES [Invoices] ([InvoiceId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260402110818_AddEntireSchema'
)
BEGIN
    CREATE TABLE [OrderDetails] (
        [OrderDetailId] int NOT NULL IDENTITY,
        [OrderId] int NOT NULL,
        [ItemId] int NOT NULL,
        [Quantity] int NOT NULL,
        [LockedUnitPrice] decimal(18,2) NOT NULL,
        CONSTRAINT [PK_OrderDetails] PRIMARY KEY ([OrderDetailId]),
        CONSTRAINT [FK_OrderDetails_MenuItems_ItemId] FOREIGN KEY ([ItemId]) REFERENCES [MenuItems] ([ItemId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OrderDetails_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([OrderId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260402110818_AddEntireSchema'
)
BEGIN
    CREATE INDEX [IX_Adjustments_InvoiceId] ON [Adjustments] ([InvoiceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260402110818_AddEntireSchema'
)
BEGIN
    CREATE INDEX [IX_Bookings_BusinessId] ON [Bookings] ([BusinessId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260402110818_AddEntireSchema'
)
BEGIN
    CREATE INDEX [IX_Bookings_CustomerId] ON [Bookings] ([CustomerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260402110818_AddEntireSchema'
)
BEGIN
    CREATE INDEX [IX_Bookings_SpaceId] ON [Bookings] ([SpaceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260402110818_AddEntireSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Businesses_DomainPrefix] ON [Businesses] ([DomainPrefix]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260402110818_AddEntireSchema'
)
BEGIN
    CREATE INDEX [IX_Businesses_PlanId] ON [Businesses] ([PlanId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260402110818_AddEntireSchema'
)
BEGIN
    CREATE INDEX [IX_ChartOfAccounts_BusinessId] ON [ChartOfAccounts] ([BusinessId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260402110818_AddEntireSchema'
)
BEGIN
    CREATE INDEX [IX_Customers_BusinessId] ON [Customers] ([BusinessId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260402110818_AddEntireSchema'
)
BEGIN
    CREATE INDEX [IX_InvoiceItems_InvoiceId] ON [InvoiceItems] ([InvoiceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260402110818_AddEntireSchema'
)
BEGIN
    CREATE INDEX [IX_Invoices_BookingId] ON [Invoices] ([BookingId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260402110818_AddEntireSchema'
)
BEGIN
    CREATE INDEX [IX_Invoices_BusinessId] ON [Invoices] ([BusinessId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260402110818_AddEntireSchema'
)
BEGIN
    CREATE INDEX [IX_JournalEntries_BusinessId] ON [JournalEntries] ([BusinessId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260402110818_AddEntireSchema'
)
BEGIN
    CREATE INDEX [IX_JournalEntryLines_AccountId] ON [JournalEntryLines] ([AccountId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260402110818_AddEntireSchema'
)
BEGIN
    CREATE INDEX [IX_JournalEntryLines_JournalEntryId] ON [JournalEntryLines] ([JournalEntryId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260402110818_AddEntireSchema'
)
BEGIN
    CREATE INDEX [IX_MenuItems_BusinessId] ON [MenuItems] ([BusinessId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260402110818_AddEntireSchema'
)
BEGIN
    CREATE INDEX [IX_OrderDetails_ItemId] ON [OrderDetails] ([ItemId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260402110818_AddEntireSchema'
)
BEGIN
    CREATE INDEX [IX_OrderDetails_OrderId] ON [OrderDetails] ([OrderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260402110818_AddEntireSchema'
)
BEGIN
    CREATE INDEX [IX_Orders_BookingId] ON [Orders] ([BookingId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260402110818_AddEntireSchema'
)
BEGIN
    CREATE INDEX [IX_Orders_BusinessId] ON [Orders] ([BusinessId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260402110818_AddEntireSchema'
)
BEGIN
    CREATE INDEX [IX_Orders_CashierId] ON [Orders] ([CashierId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260402110818_AddEntireSchema'
)
BEGIN
    CREATE INDEX [IX_Payments_BusinessId] ON [Payments] ([BusinessId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260402110818_AddEntireSchema'
)
BEGIN
    CREATE INDEX [IX_Payments_InvoiceId] ON [Payments] ([InvoiceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260402110818_AddEntireSchema'
)
BEGIN
    CREATE INDEX [IX_Spaces_BusinessId] ON [Spaces] ([BusinessId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260402110818_AddEntireSchema'
)
BEGIN
    CREATE INDEX [IX_Users_BusinessId] ON [Users] ([BusinessId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260402110818_AddEntireSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_EmailAddress] ON [Users] ([EmailAddress]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260402110818_AddEntireSchema'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260402110818_AddEntireSchema', N'8.0.26');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260404104252_AddInvoiceTaxDiscountAndBusinessSettings'
)
BEGIN
    ALTER TABLE [Invoices] ADD [DiscountAmount] decimal(18,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260404104252_AddInvoiceTaxDiscountAndBusinessSettings'
)
BEGIN
    ALTER TABLE [Invoices] ADD [SubTotal] decimal(18,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260404104252_AddInvoiceTaxDiscountAndBusinessSettings'
)
BEGIN
    ALTER TABLE [Invoices] ADD [TaxAmount] decimal(18,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260404104252_AddInvoiceTaxDiscountAndBusinessSettings'
)
BEGIN
    ALTER TABLE [Businesses] ADD [LogoUrl] nvarchar(500) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260404104252_AddInvoiceTaxDiscountAndBusinessSettings'
)
BEGIN
    ALTER TABLE [Businesses] ADD [TaxRatePercentage] decimal(5,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260404104252_AddInvoiceTaxDiscountAndBusinessSettings'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260404104252_AddInvoiceTaxDiscountAndBusinessSettings', N'8.0.26');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260404113710_AddSaaSSubscriptionBilling'
)
BEGIN
    ALTER TABLE [Businesses] ADD [CurrentPeriodEnd] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260404113710_AddSaaSSubscriptionBilling'
)
BEGIN
    ALTER TABLE [Businesses] ADD [StripeCustomerId] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260404113710_AddSaaSSubscriptionBilling'
)
BEGIN
    ALTER TABLE [Businesses] ADD [StripeSubscriptionId] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260404113710_AddSaaSSubscriptionBilling'
)
BEGIN
    ALTER TABLE [Businesses] ADD [SubscriptionStatus] nvarchar(20) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260404113710_AddSaaSSubscriptionBilling'
)
BEGIN
    CREATE TABLE [SubscriptionInvoices] (
        [SubscriptionInvoiceId] int NOT NULL IDENTITY,
        [BusinessId] int NOT NULL,
        [PlanId] int NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [PaymentMethod] nvarchar(50) NOT NULL,
        [IssuedAt] datetime2 NOT NULL,
        [PaidAt] datetime2 NULL,
        [PeriodStart] datetime2 NOT NULL,
        [PeriodEnd] datetime2 NOT NULL,
        [ExternalReference] nvarchar(100) NULL,
        CONSTRAINT [PK_SubscriptionInvoices] PRIMARY KEY ([SubscriptionInvoiceId]),
        CONSTRAINT [FK_SubscriptionInvoices_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([BusinessId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SubscriptionInvoices_SubscriptionPlans_PlanId] FOREIGN KEY ([PlanId]) REFERENCES [SubscriptionPlans] ([PlanId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260404113710_AddSaaSSubscriptionBilling'
)
BEGIN
    CREATE INDEX [IX_SubscriptionInvoices_BusinessId] ON [SubscriptionInvoices] ([BusinessId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260404113710_AddSaaSSubscriptionBilling'
)
BEGIN
    CREATE INDEX [IX_SubscriptionInvoices_PlanId] ON [SubscriptionInvoices] ([PlanId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260404113710_AddSaaSSubscriptionBilling'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260404113710_AddSaaSSubscriptionBilling', N'8.0.26');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260404120920_AddInventoryManagement'
)
BEGIN
    ALTER TABLE [MenuItems] ADD [CostPrice] decimal(18,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260404120920_AddInventoryManagement'
)
BEGIN
    ALTER TABLE [MenuItems] ADD [LowStockThreshold] int NOT NULL DEFAULT 5;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260404120920_AddInventoryManagement'
)
BEGIN
    CREATE TABLE [InventoryTransactions] (
        [InventoryTransactionId] int NOT NULL IDENTITY,
        [BusinessId] int NOT NULL,
        [ItemId] int NOT NULL,
        [QuantityChange] int NOT NULL,
        [PreviousStock] int NOT NULL,
        [NewStock] int NOT NULL,
        [TransactionType] nvarchar(20) NOT NULL,
        [Notes] nvarchar(255) NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (GETDATE()),
        CONSTRAINT [PK_InventoryTransactions] PRIMARY KEY ([InventoryTransactionId]),
        CONSTRAINT [FK_InventoryTransactions_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([BusinessId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_InventoryTransactions_MenuItems_ItemId] FOREIGN KEY ([ItemId]) REFERENCES [MenuItems] ([ItemId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260404120920_AddInventoryManagement'
)
BEGIN
    CREATE INDEX [IX_InventoryTransactions_BusinessId_ItemId_CreatedAt] ON [InventoryTransactions] ([BusinessId], [ItemId], [CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260404120920_AddInventoryManagement'
)
BEGIN
    CREATE INDEX [IX_InventoryTransactions_ItemId] ON [InventoryTransactions] ([ItemId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260404120920_AddInventoryManagement'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260404120920_AddInventoryManagement', N'8.0.26');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260404131452_AddShiftCashDrawerManagement'
)
BEGIN
    CREATE TABLE [PosShifts] (
        [ShiftId] int NOT NULL IDENTITY,
        [BusinessId] int NOT NULL,
        [CashierId] int NOT NULL,
        [OpenedAt] datetime2 NOT NULL DEFAULT (GETDATE()),
        [ClosedAt] datetime2 NULL,
        [OpeningCash] decimal(18,2) NOT NULL,
        [ExpectedCash] decimal(18,2) NOT NULL,
        [ActualCash] decimal(18,2) NULL,
        [Variance] decimal(18,2) NULL,
        [Status] nvarchar(20) NOT NULL DEFAULT N'Open',
        [Notes] nvarchar(255) NULL,
        CONSTRAINT [PK_PosShifts] PRIMARY KEY ([ShiftId]),
        CONSTRAINT [FK_PosShifts_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([BusinessId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PosShifts_Users_CashierId] FOREIGN KEY ([CashierId]) REFERENCES [Users] ([UserId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260404131452_AddShiftCashDrawerManagement'
)
BEGIN
    CREATE TABLE [CashDrawerTransactions] (
        [DrawerTransactionId] int NOT NULL IDENTITY,
        [BusinessId] int NOT NULL,
        [ShiftId] int NOT NULL,
        [TransactionType] nvarchar(20) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Notes] nvarchar(255) NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (GETDATE()),
        CONSTRAINT [PK_CashDrawerTransactions] PRIMARY KEY ([DrawerTransactionId]),
        CONSTRAINT [FK_CashDrawerTransactions_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([BusinessId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CashDrawerTransactions_PosShifts_ShiftId] FOREIGN KEY ([ShiftId]) REFERENCES [PosShifts] ([ShiftId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260404131452_AddShiftCashDrawerManagement'
)
BEGIN
    CREATE INDEX [IX_CashDrawerTransactions_BusinessId_ShiftId_CreatedAt] ON [CashDrawerTransactions] ([BusinessId], [ShiftId], [CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260404131452_AddShiftCashDrawerManagement'
)
BEGIN
    CREATE INDEX [IX_CashDrawerTransactions_ShiftId] ON [CashDrawerTransactions] ([ShiftId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260404131452_AddShiftCashDrawerManagement'
)
BEGIN
    CREATE INDEX [IX_PosShifts_BusinessId_CashierId_Status_OpenedAt] ON [PosShifts] ([BusinessId], [CashierId], [Status], [OpenedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260404131452_AddShiftCashDrawerManagement'
)
BEGIN
    CREATE INDEX [IX_PosShifts_CashierId] ON [PosShifts] ([CashierId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260404131452_AddShiftCashDrawerManagement'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260404131452_AddShiftCashDrawerManagement', N'8.0.26');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260404134437_AddTableLayoutAndSplitCheckout'
)
BEGIN
    DROP INDEX [IX_Spaces_BusinessId] ON [Spaces];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260404134437_AddTableLayoutAndSplitCheckout'
)
BEGIN
    ALTER TABLE [Spaces] ADD [Capacity] int NOT NULL DEFAULT 4;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260404134437_AddTableLayoutAndSplitCheckout'
)
BEGIN
    ALTER TABLE [Spaces] ADD [FloorArea] nvarchar(50) NOT NULL DEFAULT N'Main Floor';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260404134437_AddTableLayoutAndSplitCheckout'
)
BEGIN
    CREATE INDEX [IX_Spaces_BusinessId_FloorArea_CurrentStatus] ON [Spaces] ([BusinessId], [FloorArea], [CurrentStatus]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260404134437_AddTableLayoutAndSplitCheckout'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260404134437_AddTableLayoutAndSplitCheckout', N'8.0.26');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260404135236_AddPosAuditTrailLogs'
)
BEGIN
    CREATE TABLE [PosAuditLogs] (
        [PosAuditLogId] int NOT NULL IDENTITY,
        [BusinessId] int NOT NULL,
        [CashierId] int NOT NULL,
        [BookingId] int NULL,
        [ActionType] nvarchar(40) NOT NULL,
        [SourceSpaceId] int NULL,
        [SourceSpaceName] nvarchar(50) NULL,
        [TargetSpaceId] int NULL,
        [TargetSpaceName] nvarchar(50) NULL,
        [SplitCount] int NULL,
        [InvoiceIds] nvarchar(255) NULL,
        [Details] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (GETDATE()),
        CONSTRAINT [PK_PosAuditLogs] PRIMARY KEY ([PosAuditLogId]),
        CONSTRAINT [FK_PosAuditLogs_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([BusinessId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PosAuditLogs_Users_CashierId] FOREIGN KEY ([CashierId]) REFERENCES [Users] ([UserId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260404135236_AddPosAuditTrailLogs'
)
BEGIN
    CREATE INDEX [IX_PosAuditLogs_BusinessId_ActionType_CreatedAt] ON [PosAuditLogs] ([BusinessId], [ActionType], [CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260404135236_AddPosAuditTrailLogs'
)
BEGIN
    CREATE INDEX [IX_PosAuditLogs_BusinessId_CashierId_CreatedAt] ON [PosAuditLogs] ([BusinessId], [CashierId], [CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260404135236_AddPosAuditTrailLogs'
)
BEGIN
    CREATE INDEX [IX_PosAuditLogs_CashierId] ON [PosAuditLogs] ([CashierId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260404135236_AddPosAuditTrailLogs'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260404135236_AddPosAuditTrailLogs', N'8.0.26');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260408010140_AddStripePriceIdToSubscriptionPlan'
)
BEGIN
    ALTER TABLE [SubscriptionPlans] ADD [StripePriceId] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260408010140_AddStripePriceIdToSubscriptionPlan'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260408010140_AddStripePriceIdToSubscriptionPlan', N'8.0.26');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260408134556_AddPendingRegistrations'
)
BEGIN
    CREATE TABLE [PendingRegistrations] (
        [PendingRegistrationId] int NOT NULL IDENTITY,
        [Token] nvarchar(36) NOT NULL,
        [PlanId] int NOT NULL,
        [BusinessName] nvarchar(100) NOT NULL,
        [FirstName] nvarchar(50) NOT NULL,
        [LastName] nvarchar(50) NOT NULL,
        [EmailAddress] nvarchar(256) NOT NULL,
        [PasswordHash] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [IsUsed] bit NOT NULL,
        CONSTRAINT [PK_PendingRegistrations] PRIMARY KEY ([PendingRegistrationId]),
        CONSTRAINT [FK_PendingRegistrations_SubscriptionPlans_PlanId] FOREIGN KEY ([PlanId]) REFERENCES [SubscriptionPlans] ([PlanId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260408134556_AddPendingRegistrations'
)
BEGIN
    CREATE INDEX [IX_PendingRegistrations_PlanId] ON [PendingRegistrations] ([PlanId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260408134556_AddPendingRegistrations'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260408134556_AddPendingRegistrations', N'8.0.26');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409115153_AddPasswordResetToken'
)
BEGIN
    ALTER TABLE [Users] ADD [PasswordResetToken] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409115153_AddPasswordResetToken'
)
BEGIN
    ALTER TABLE [Users] ADD [PasswordResetTokenExpiry] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409115153_AddPasswordResetToken'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260409115153_AddPasswordResetToken', N'8.0.26');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409184834_AddCheckoutRequestedToBooking'
)
BEGIN
    ALTER TABLE [Bookings] ADD [CheckoutRequested] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409184834_AddCheckoutRequestedToBooking'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260409184834_AddCheckoutRequestedToBooking', N'8.0.26');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260410144814_AddCustomerEmailToBooking'
)
BEGIN
    ALTER TABLE [Bookings] ADD [CustomerEmail] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260410144814_AddCustomerEmailToBooking'
)
BEGIN
    ALTER TABLE [Bookings] ADD [CustomerReceiptEmailSent] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260410144814_AddCustomerEmailToBooking'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260410144814_AddCustomerEmailToBooking', N'8.0.26');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260424060953_AddRequestedSplitCount'
)
BEGIN
    ALTER TABLE [Bookings] ADD [RequestedSplitCount] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260424060953_AddRequestedSplitCount'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260424060953_AddRequestedSplitCount', N'8.0.26');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428063824_AddSuperAdminLifecycleAuditAndTrends'
)
BEGIN
    CREATE TABLE [BusinessLifecycleEvents] (
        [EventId] int NOT NULL IDENTITY,
        [BusinessId] int NOT NULL,
        [EventType] nvarchar(40) NOT NULL,
        [PreviousValue] nvarchar(100) NULL,
        [NewValue] nvarchar(100) NULL,
        [Reason] nvarchar(300) NULL,
        [ActorUserId] int NULL,
        [ActorName] nvarchar(120) NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (GETDATE()),
        CONSTRAINT [PK_BusinessLifecycleEvents] PRIMARY KEY ([EventId]),
        CONSTRAINT [FK_BusinessLifecycleEvents_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([BusinessId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428063824_AddSuperAdminLifecycleAuditAndTrends'
)
BEGIN
    CREATE TABLE [SuperAdminAuditLogs] (
        [AuditLogId] int NOT NULL IDENTITY,
        [ActionType] nvarchar(40) NOT NULL,
        [EntityType] nvarchar(40) NOT NULL,
        [EntityId] int NULL,
        [BusinessId] int NULL,
        [BusinessName] nvarchar(120) NULL,
        [Details] nvarchar(400) NULL,
        [Reason] nvarchar(300) NULL,
        [ActorUserId] int NULL,
        [ActorName] nvarchar(120) NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (GETDATE()),
        CONSTRAINT [PK_SuperAdminAuditLogs] PRIMARY KEY ([AuditLogId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428063824_AddSuperAdminLifecycleAuditAndTrends'
)
BEGIN
    CREATE INDEX [IX_BusinessLifecycleEvents_BusinessId_CreatedAt] ON [BusinessLifecycleEvents] ([BusinessId], [CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428063824_AddSuperAdminLifecycleAuditAndTrends'
)
BEGIN
    CREATE INDEX [IX_BusinessLifecycleEvents_EventType_CreatedAt] ON [BusinessLifecycleEvents] ([EventType], [CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428063824_AddSuperAdminLifecycleAuditAndTrends'
)
BEGIN
    CREATE INDEX [IX_SuperAdminAuditLogs_ActionType_CreatedAt] ON [SuperAdminAuditLogs] ([ActionType], [CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428063824_AddSuperAdminLifecycleAuditAndTrends'
)
BEGIN
    CREATE INDEX [IX_SuperAdminAuditLogs_EntityType_CreatedAt] ON [SuperAdminAuditLogs] ([EntityType], [CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428063824_AddSuperAdminLifecycleAuditAndTrends'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260428063824_AddSuperAdminLifecycleAuditAndTrends', N'8.0.26');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429130508_AddMenuItemCatalogMetadata'
)
BEGIN
    DROP INDEX [IX_MenuItems_BusinessId] ON [MenuItems];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429130508_AddMenuItemCatalogMetadata'
)
BEGIN
    ALTER TABLE [MenuItems] ADD [Category] nvarchar(50) NOT NULL DEFAULT N'General';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429130508_AddMenuItemCatalogMetadata'
)
BEGIN
    ALTER TABLE [MenuItems] ADD [ImageUrl] nvarchar(500) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429130508_AddMenuItemCatalogMetadata'
)
BEGIN
    ALTER TABLE [MenuItems] ADD [SortOrder] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429130508_AddMenuItemCatalogMetadata'
)
BEGIN
    CREATE INDEX [IX_MenuItems_BusinessId_Category_SortOrder_ItemName] ON [MenuItems] ([BusinessId], [Category], [SortOrder], [ItemName]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429130508_AddMenuItemCatalogMetadata'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260429130508_AddMenuItemCatalogMetadata', N'8.0.26');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429141102_AddOrderSourceAndServedTracking'
)
BEGIN
    ALTER TABLE [Orders] ADD [OrderSource] nvarchar(10) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429141102_AddOrderSourceAndServedTracking'
)
BEGIN
    ALTER TABLE [OrderDetails] ADD [IsServed] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429141102_AddOrderSourceAndServedTracking'
)
BEGIN
    ALTER TABLE [OrderDetails] ADD [ServedAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429141102_AddOrderSourceAndServedTracking'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260429141102_AddOrderSourceAndServedTracking', N'8.0.26');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501031834_AddInvoiceNumber'
)
BEGIN
    ALTER TABLE [Invoices] ADD [InvoiceNumber] nvarchar(20) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501031834_AddInvoiceNumber'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260501031834_AddInvoiceNumber', N'8.0.26');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504144702_AddInitialCapitalToBusiness'
)
BEGIN
    ALTER TABLE [Businesses] ADD [InitialCapital] decimal(18,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504144702_AddInitialCapitalToBusiness'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260504144702_AddInitialCapitalToBusiness', N'8.0.26');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505045049_AddThemePreference'
)
BEGIN
    ALTER TABLE [Businesses] ADD [ThemePreference] nvarchar(50) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505045049_AddThemePreference'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260505045049_AddThemePreference', N'8.0.26');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505074152_AddInventoryReorderIntelligence'
)
BEGIN
    ALTER TABLE [Businesses] ADD [InventoryAlertEmail] nvarchar(256) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505074152_AddInventoryReorderIntelligence'
)
BEGIN
    ALTER TABLE [Businesses] ADD [InventoryAlertEnabled] bit NOT NULL DEFAULT CAST(1 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505074152_AddInventoryReorderIntelligence'
)
BEGIN
    ALTER TABLE [Businesses] ADD [InventoryLeadTimeDays] int NOT NULL DEFAULT 3;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505074152_AddInventoryReorderIntelligence'
)
BEGIN
    ALTER TABLE [Businesses] ADD [InventoryReorderLookbackDays] int NOT NULL DEFAULT 30;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505074152_AddInventoryReorderIntelligence'
)
BEGIN
    ALTER TABLE [Businesses] ADD [InventorySafetyStockDays] int NOT NULL DEFAULT 2;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505074152_AddInventoryReorderIntelligence'
)
BEGIN
    ALTER TABLE [Businesses] ADD [InventoryTargetCoverageDays] int NOT NULL DEFAULT 7;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505074152_AddInventoryReorderIntelligence'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260505074152_AddInventoryReorderIntelligence', N'8.0.26');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505141133_AddInventoryAlertLogs'
)
BEGIN
    CREATE TABLE [InventoryAlertLogs] (
        [InventoryAlertLogId] int NOT NULL IDENTITY,
        [BusinessId] int NOT NULL,
        [AlertType] nvarchar(32) NOT NULL,
        [TriggerSource] nvarchar(32) NOT NULL,
        [RecipientEmail] nvarchar(256) NOT NULL,
        [RecipientName] nvarchar(120) NOT NULL,
        [RecommendationCount] int NOT NULL,
        [RecommendedUnits] int NOT NULL,
        [AlertSignature] nvarchar(128) NOT NULL,
        [RecommendationSnapshotJson] nvarchar(max) NOT NULL,
        [SentAt] datetime2 NOT NULL DEFAULT (GETDATE()),
        CONSTRAINT [PK_InventoryAlertLogs] PRIMARY KEY ([InventoryAlertLogId]),
        CONSTRAINT [FK_InventoryAlertLogs_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([BusinessId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505141133_AddInventoryAlertLogs'
)
BEGIN
    CREATE INDEX [IX_InventoryAlertLogs_BusinessId_AlertType_AlertSignature_SentAt] ON [InventoryAlertLogs] ([BusinessId], [AlertType], [AlertSignature], [SentAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505141133_AddInventoryAlertLogs'
)
BEGIN
    CREATE INDEX [IX_InventoryAlertLogs_BusinessId_SentAt] ON [InventoryAlertLogs] ([BusinessId], [SentAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505141133_AddInventoryAlertLogs'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260505141133_AddInventoryAlertLogs', N'8.0.26');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505142212_AddSupplierPurchaseOrders'
)
BEGIN
    CREATE TABLE [Suppliers] (
        [SupplierId] int NOT NULL IDENTITY,
        [BusinessId] int NOT NULL,
        [SupplierName] nvarchar(120) NOT NULL,
        [ContactPerson] nvarchar(120) NULL,
        [EmailAddress] nvarchar(256) NULL,
        [PhoneNumber] nvarchar(30) NULL,
        [LeadTimeDaysOverride] int NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (GETDATE()),
        CONSTRAINT [PK_Suppliers] PRIMARY KEY ([SupplierId]),
        CONSTRAINT [FK_Suppliers_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([BusinessId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505142212_AddSupplierPurchaseOrders'
)
BEGIN
    CREATE TABLE [PurchaseOrders] (
        [PurchaseOrderId] int NOT NULL IDENTITY,
        [BusinessId] int NOT NULL,
        [SupplierId] int NOT NULL,
        [PurchaseOrderNumber] nvarchar(40) NOT NULL,
        [Status] nvarchar(20) NOT NULL DEFAULT N'Draft',
        [Notes] nvarchar(255) NULL,
        [CreatedByUserId] int NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (GETDATE()),
        [ExpectedDeliveryDate] datetime2 NULL,
        CONSTRAINT [PK_PurchaseOrders] PRIMARY KEY ([PurchaseOrderId]),
        CONSTRAINT [FK_PurchaseOrders_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([BusinessId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PurchaseOrders_Suppliers_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [Suppliers] ([SupplierId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PurchaseOrders_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([UserId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505142212_AddSupplierPurchaseOrders'
)
BEGIN
    CREATE TABLE [PurchaseOrderLines] (
        [PurchaseOrderLineId] int NOT NULL IDENTITY,
        [PurchaseOrderId] int NOT NULL,
        [ItemId] int NOT NULL,
        [Quantity] int NOT NULL,
        [UnitCost] decimal(18,2) NOT NULL,
        [LineTotal] decimal(18,2) NOT NULL,
        CONSTRAINT [PK_PurchaseOrderLines] PRIMARY KEY ([PurchaseOrderLineId]),
        CONSTRAINT [FK_PurchaseOrderLines_MenuItems_ItemId] FOREIGN KEY ([ItemId]) REFERENCES [MenuItems] ([ItemId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PurchaseOrderLines_PurchaseOrders_PurchaseOrderId] FOREIGN KEY ([PurchaseOrderId]) REFERENCES [PurchaseOrders] ([PurchaseOrderId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505142212_AddSupplierPurchaseOrders'
)
BEGIN
    CREATE INDEX [IX_PurchaseOrderLines_ItemId] ON [PurchaseOrderLines] ([ItemId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505142212_AddSupplierPurchaseOrders'
)
BEGIN
    CREATE INDEX [IX_PurchaseOrderLines_PurchaseOrderId_ItemId] ON [PurchaseOrderLines] ([PurchaseOrderId], [ItemId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505142212_AddSupplierPurchaseOrders'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PurchaseOrders_BusinessId_PurchaseOrderNumber] ON [PurchaseOrders] ([BusinessId], [PurchaseOrderNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505142212_AddSupplierPurchaseOrders'
)
BEGIN
    CREATE INDEX [IX_PurchaseOrders_BusinessId_Status_CreatedAt] ON [PurchaseOrders] ([BusinessId], [Status], [CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505142212_AddSupplierPurchaseOrders'
)
BEGIN
    CREATE INDEX [IX_PurchaseOrders_CreatedByUserId] ON [PurchaseOrders] ([CreatedByUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505142212_AddSupplierPurchaseOrders'
)
BEGIN
    CREATE INDEX [IX_PurchaseOrders_SupplierId] ON [PurchaseOrders] ([SupplierId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505142212_AddSupplierPurchaseOrders'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Suppliers_BusinessId_SupplierName] ON [Suppliers] ([BusinessId], [SupplierName]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505142212_AddSupplierPurchaseOrders'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260505142212_AddSupplierPurchaseOrders', N'8.0.26');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505144257_AddPartialPurchaseOrderReceiving'
)
BEGIN
    ALTER TABLE [Suppliers] ADD [UpdatedAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505144257_AddPartialPurchaseOrderReceiving'
)
BEGIN
    ALTER TABLE [PurchaseOrders] ADD [OrderedAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505144257_AddPartialPurchaseOrderReceiving'
)
BEGIN
    ALTER TABLE [PurchaseOrders] ADD [ReceivedAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505144257_AddPartialPurchaseOrderReceiving'
)
BEGIN
    ALTER TABLE [PurchaseOrderLines] ADD [ReceivedQuantity] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505144257_AddPartialPurchaseOrderReceiving'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260505144257_AddPartialPurchaseOrderReceiving', N'8.0.26');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505145153_AddPurchaseOrderReceiptHistory'
)
BEGIN
    CREATE TABLE [PurchaseOrderReceipts] (
        [PurchaseOrderReceiptId] int NOT NULL IDENTITY,
        [PurchaseOrderId] int NOT NULL,
        [BusinessId] int NOT NULL,
        [ItemId] int NOT NULL,
        [QuantityReceived] int NOT NULL,
        [PreviousReceivedQuantity] int NOT NULL,
        [NewReceivedQuantity] int NOT NULL,
        [PreviousStock] int NOT NULL,
        [NewStock] int NOT NULL,
        [Notes] nvarchar(255) NULL,
        [ReceivedAt] datetime2 NOT NULL DEFAULT (GETDATE()),
        CONSTRAINT [PK_PurchaseOrderReceipts] PRIMARY KEY ([PurchaseOrderReceiptId]),
        CONSTRAINT [FK_PurchaseOrderReceipts_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([BusinessId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PurchaseOrderReceipts_MenuItems_ItemId] FOREIGN KEY ([ItemId]) REFERENCES [MenuItems] ([ItemId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PurchaseOrderReceipts_PurchaseOrders_PurchaseOrderId] FOREIGN KEY ([PurchaseOrderId]) REFERENCES [PurchaseOrders] ([PurchaseOrderId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505145153_AddPurchaseOrderReceiptHistory'
)
BEGIN
    CREATE INDEX [IX_PurchaseOrderReceipts_BusinessId_ItemId_ReceivedAt] ON [PurchaseOrderReceipts] ([BusinessId], [ItemId], [ReceivedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505145153_AddPurchaseOrderReceiptHistory'
)
BEGIN
    CREATE INDEX [IX_PurchaseOrderReceipts_ItemId] ON [PurchaseOrderReceipts] ([ItemId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505145153_AddPurchaseOrderReceiptHistory'
)
BEGIN
    CREATE INDEX [IX_PurchaseOrderReceipts_PurchaseOrderId_ReceivedAt] ON [PurchaseOrderReceipts] ([PurchaseOrderId], [ReceivedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505145153_AddPurchaseOrderReceiptHistory'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260505145153_AddPurchaseOrderReceiptHistory', N'8.0.26');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505151030_AddInventoryDemandForecasting'
)
BEGIN
    ALTER TABLE [Businesses] ADD [InventoryForecastHorizonDays] int NOT NULL DEFAULT 7;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505151030_AddInventoryDemandForecasting'
)
BEGIN
    ALTER TABLE [Businesses] ADD [InventoryForecastLookbackDays] int NOT NULL DEFAULT 28;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505151030_AddInventoryDemandForecasting'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260505151030_AddInventoryDemandForecasting', N'8.0.26');
END;
GO

COMMIT;
GO

