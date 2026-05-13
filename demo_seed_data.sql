-- ============================================================
-- ZoneBill Demo Data Seed Script
-- Generates realistic transaction data from Jan 1, 2026 to May 11, 2026
-- Run this on the MonsterASP production database (db49781)
-- ============================================================

SET NOCOUNT ON;
BEGIN TRANSACTION;

-- ============================================================
-- 1. DISCOVER EXISTING DATA
-- ============================================================
DECLARE @BizId INT;
SELECT TOP 1 @BizId = BusinessId FROM Businesses WHERE IsActive = 1;

DECLARE @CashierId INT;
SELECT TOP 1 @CashierId = UserId FROM Users WHERE BusinessId = @BizId AND UserRole IN ('MainAdmin','Cashier');

IF @BizId IS NULL OR @CashierId IS NULL
BEGIN
    PRINT 'ERROR: No active business or cashier found. Aborting.';
    ROLLBACK;
    RETURN;
END

PRINT 'Business ID: ' + CAST(@BizId AS VARCHAR);
PRINT 'Cashier ID: ' + CAST(@CashierId AS VARCHAR);

-- Collect Spaces into a temp table
CREATE TABLE #Spaces (RowNum INT IDENTITY(1,1), SpaceId INT, HourlyRate DECIMAL(18,2));
INSERT INTO #Spaces (SpaceId, HourlyRate)
SELECT SpaceId, CurrentHourlyRate FROM Spaces WHERE BusinessId = @BizId AND IsActive = 1;

DECLARE @SpaceCount INT = (SELECT COUNT(*) FROM #Spaces);
PRINT 'Spaces found: ' + CAST(@SpaceCount AS VARCHAR);

-- Collect MenuItems into a temp table
CREATE TABLE #Items (RowNum INT IDENTITY(1,1), ItemId INT, Price DECIMAL(18,2), CostPrice DECIMAL(18,2), Category VARCHAR(50));
INSERT INTO #Items (ItemId, Price, CostPrice, Category)
SELECT ItemId, CurrentPrice, CostPrice, Category FROM MenuItems WHERE BusinessId = @BizId AND IsActive = 1;

DECLARE @ItemCount INT = (SELECT COUNT(*) FROM #Items);
PRINT 'Menu items found: ' + CAST(@ItemCount AS VARCHAR);

IF @SpaceCount = 0 OR @ItemCount = 0
BEGIN
    PRINT 'ERROR: No spaces or menu items found. Aborting.';
    DROP TABLE #Spaces; DROP TABLE #Items;
    ROLLBACK;
    RETURN;
END

-- ============================================================
-- 2. GENERATE BOOKINGS, ORDERS, INVOICES, PAYMENTS DAY BY DAY
-- ============================================================
DECLARE @StartDate DATE = '2026-01-01';
DECLARE @EndDate DATE = '2026-05-11';
DECLARE @CurrentDate DATE = @StartDate;
DECLARE @DayCounter INT = 0;

-- Random seed helpers
DECLARE @BookingsToday INT;
DECLARE @BookingLoop INT;
DECLARE @RandSpaceIdx INT;
DECLARE @RandSpaceId INT;
DECLARE @RandRate DECIMAL(18,2);
DECLARE @StartHour INT;
DECLARE @DurationHrs DECIMAL(10,2);
DECLARE @BookingStart DATETIME;
DECLARE @BookingEnd DATETIME;
DECLARE @NewBookingId INT;
DECLARE @NewOrderId INT;
DECLARE @NewInvoiceId INT;
DECLARE @OrderItemCount INT;
DECLARE @OrderLoop INT;
DECLARE @RandItemIdx INT;
DECLARE @RandItemId INT;
DECLARE @RandItemPrice DECIMAL(18,2);
DECLARE @RandItemCost DECIMAL(18,2);
DECLARE @RandQty INT;
DECLARE @SpaceCharge DECIMAL(18,2);
DECLARE @OrdersTotal DECIMAL(18,2);
DECLARE @SubTotal DECIMAL(18,2);
DECLARE @TaxRate DECIMAL(5,4) = 0.0000;
DECLARE @TaxAmt DECIMAL(18,2);
DECLARE @TotalAmt DECIMAL(18,2);
DECLARE @InvNum VARCHAR(20);
DECLARE @RefCode VARCHAR(20);
DECLARE @InvCounter INT = 0;

WHILE @CurrentDate <= @EndDate
BEGIN
    SET @DayCounter = @DayCounter + 1;
    
    -- 10-20 bookings per day (vary by day of week)
    SET @BookingsToday = 10 + ABS(CHECKSUM(NEWID())) % 11; -- 10 to 20
    
    -- Weekends get more bookings
    IF DATEPART(WEEKDAY, @CurrentDate) IN (1, 7) -- Sunday=1, Saturday=7
        SET @BookingsToday = @BookingsToday + 5;
    
    SET @BookingLoop = 0;
    
    WHILE @BookingLoop < @BookingsToday
    BEGIN
        SET @BookingLoop = @BookingLoop + 1;
        
        -- Pick random space
        SET @RandSpaceIdx = 1 + ABS(CHECKSUM(NEWID())) % @SpaceCount;
        SELECT @RandSpaceId = SpaceId, @RandRate = HourlyRate FROM #Spaces WHERE RowNum = @RandSpaceIdx;
        
        -- Random start hour (10 AM to 10 PM)
        SET @StartHour = 10 + ABS(CHECKSUM(NEWID())) % 13;
        
        -- Random duration (1 to 4 hours)
        SET @DurationHrs = 1.0 + (ABS(CHECKSUM(NEWID())) % 7) * 0.5; -- 1.0 to 4.0
        
        SET @BookingStart = DATEADD(MINUTE, ABS(CHECKSUM(NEWID())) % 60, 
                            DATEADD(HOUR, @StartHour, CAST(@CurrentDate AS DATETIME)));
        SET @BookingEnd = DATEADD(MINUTE, CAST(@DurationHrs * 60 AS INT), @BookingStart);
        
        -- Reference code
        SET @RefCode = 'ZB-' + REPLACE(CONVERT(VARCHAR, @CurrentDate, 112), '-', '') 
                       + '-' + RIGHT('000' + CAST(@BookingLoop AS VARCHAR), 3);
        
        -- Insert Booking (Completed)
        INSERT INTO Bookings (BusinessId, SpaceId, CustomerId, StartTime, EndTime, DurationHours, 
                              LockedHourlyRate, BookingStatus, ReferenceCode, CheckoutRequested, 
                              RequestedSplitCount, CustomerEmail, CustomerReceiptEmailSent)
        VALUES (@BizId, @RandSpaceId, NULL, @BookingStart, @BookingEnd, @DurationHrs,
                @RandRate, 'Completed', @RefCode, 0, NULL, NULL, 0);
        
        SET @NewBookingId = SCOPE_IDENTITY();
        
        -- ---- ORDERS (1-3 orders per booking) ----
        SET @OrdersTotal = 0;
        SET @OrderItemCount = 1 + ABS(CHECKSUM(NEWID())) % 3; -- 1 to 3 orders
        SET @OrderLoop = 0;
        
        WHILE @OrderLoop < @OrderItemCount
        BEGIN
            SET @OrderLoop = @OrderLoop + 1;
            
            -- Insert Order
            INSERT INTO Orders (BusinessId, BookingId, CashierId, OrderTime, OrderSource)
            VALUES (@BizId, @NewBookingId, @CashierId,
                    DATEADD(MINUTE, @OrderLoop * 20, @BookingStart),
                    CASE WHEN ABS(CHECKSUM(NEWID())) % 5 = 0 THEN 'Portal' ELSE 'POS' END);
            
            SET @NewOrderId = SCOPE_IDENTITY();
            
            -- Insert 1-4 order details per order
            DECLARE @DetailCount INT = 1 + ABS(CHECKSUM(NEWID())) % 4;
            DECLARE @DetailLoop INT = 0;
            
            WHILE @DetailLoop < @DetailCount
            BEGIN
                SET @DetailLoop = @DetailLoop + 1;
                
                SET @RandItemIdx = 1 + ABS(CHECKSUM(NEWID())) % @ItemCount;
                SELECT @RandItemId = ItemId, @RandItemPrice = Price, @RandItemCost = CostPrice 
                FROM #Items WHERE RowNum = @RandItemIdx;
                
                SET @RandQty = 1 + ABS(CHECKSUM(NEWID())) % 3; -- 1 to 3
                
                INSERT INTO OrderDetails (OrderId, ItemId, Quantity, LockedUnitPrice, IsServed, ServedAt)
                VALUES (@NewOrderId, @RandItemId, @RandQty, @RandItemPrice, 1, 
                        DATEADD(MINUTE, 5 + @DetailLoop * 3, DATEADD(MINUTE, @OrderLoop * 20, @BookingStart)));
                
                SET @OrdersTotal = @OrdersTotal + (@RandItemPrice * @RandQty);
            END
        END
        
        -- ---- INVOICE ----
        SET @InvCounter = @InvCounter + 1;
        SET @SpaceCharge = @RandRate * @DurationHrs;
        SET @SubTotal = @SpaceCharge + @OrdersTotal;
        SET @TaxAmt = ROUND(@SubTotal * @TaxRate, 2);
        SET @TotalAmt = @SubTotal + @TaxAmt;
        SET @InvNum = 'INV-' + RIGHT('00000' + CAST(@InvCounter AS VARCHAR), 5);
        
        INSERT INTO Invoices (BusinessId, BookingId, InvoiceNumber, SubTotal, DiscountAmount, 
                              TaxAmount, TotalAmount, TaxRateApplied, PaymentStatus, GeneratedDate)
        VALUES (@BizId, @NewBookingId, @InvNum, @SubTotal, 0, @TaxAmt, @TotalAmt, @TaxRate, 
                'Paid', @BookingEnd);
        
        SET @NewInvoiceId = SCOPE_IDENTITY();
        
        -- Invoice Items: Space charge
        INSERT INTO InvoiceItems (InvoiceId, ItemType, Description, Quantity, UnitPrice, Total)
        VALUES (@NewInvoiceId, 'Space', 'Space rental (' + CAST(@DurationHrs AS VARCHAR) + ' hrs)', 
                1, @SpaceCharge, @SpaceCharge);
        
        -- Invoice Items: Orders total
        IF @OrdersTotal > 0
        BEGIN
            INSERT INTO InvoiceItems (InvoiceId, ItemType, Description, Quantity, UnitPrice, Total)
            VALUES (@NewInvoiceId, 'Order', 'Food & beverage orders', 1, @OrdersTotal, @OrdersTotal);
        END
        
        -- ---- PAYMENT ----
        INSERT INTO Payments (BusinessId, InvoiceId, AmountPaid, PaymentMethod, PaymentDate, ReferenceNumber)
        VALUES (@BizId, @NewInvoiceId, @TotalAmt,
                CASE ABS(CHECKSUM(NEWID())) % 3
                    WHEN 0 THEN 'Cash'
                    WHEN 1 THEN 'GCash'
                    ELSE 'Cash'
                END,
                DATEADD(MINUTE, 5, @BookingEnd),
                'PAY-' + RIGHT('00000' + CAST(@InvCounter AS VARCHAR), 5));
    END
    
    SET @CurrentDate = DATEADD(DAY, 1, @CurrentDate);
END

PRINT 'Generated ' + CAST(@InvCounter AS VARCHAR) + ' bookings with orders, invoices, and payments.';
PRINT 'Date range: ' + CAST(@StartDate AS VARCHAR) + ' to ' + CAST(@EndDate AS VARCHAR);

-- ============================================================
-- 3. GENERATE INVENTORY TRANSACTIONS (Restocks + Sales)
-- ============================================================
-- Add periodic restocks (every 5-7 days per item)
DECLARE @InvDate DATE = @StartDate;
DECLARE @InvItemLoop INT;
DECLARE @InvItemId INT;
DECLARE @RestockQty INT;
DECLARE @RunningStock INT;
DECLARE @TxCount INT = 0;

WHILE @InvDate <= @EndDate
BEGIN
    -- Restock every ~5 days
    IF DATEDIFF(DAY, @StartDate, @InvDate) % 5 = 0
    BEGIN
        SET @InvItemLoop = 1;
        WHILE @InvItemLoop <= @ItemCount
        BEGIN
            SELECT @InvItemId = ItemId FROM #Items WHERE RowNum = @InvItemLoop;
            SET @RestockQty = 20 + ABS(CHECKSUM(NEWID())) % 30; -- 20 to 49
            
            INSERT INTO InventoryTransactions (BusinessId, ItemId, QuantityChange, PreviousStock, NewStock, TransactionType, Notes, CreatedAt)
            VALUES (@BizId, @InvItemId, @RestockQty, 0, @RestockQty, 'Restock', 
                    'Periodic restock', DATEADD(HOUR, 8, CAST(@InvDate AS DATETIME)));
            
            SET @TxCount = @TxCount + 1;
            SET @InvItemLoop = @InvItemLoop + 1;
        END
    END
    
    -- Daily sales deductions (simulated)
    SET @InvItemLoop = 1;
    WHILE @InvItemLoop <= @ItemCount
    BEGIN
        SELECT @InvItemId = ItemId FROM #Items WHERE RowNum = @InvItemLoop;
        DECLARE @SalesQty INT = 2 + ABS(CHECKSUM(NEWID())) % 8; -- 2-9 sold per day
        
        INSERT INTO InventoryTransactions (BusinessId, ItemId, QuantityChange, PreviousStock, NewStock, TransactionType, Notes, CreatedAt)
        VALUES (@BizId, @InvItemId, -@SalesQty, @SalesQty + 10, 10, 'Sale', 
                'Daily sales', DATEADD(HOUR, 22, CAST(@InvDate AS DATETIME)));
        
        SET @TxCount = @TxCount + 1;
        SET @InvItemLoop = @InvItemLoop + 1;
    END
    
    SET @InvDate = DATEADD(DAY, 1, @InvDate);
END

PRINT 'Generated ' + CAST(@TxCount AS VARCHAR) + ' inventory transactions.';

-- Cleanup temp tables
DROP TABLE #Spaces;
DROP TABLE #Items;

COMMIT TRANSACTION;
PRINT 'ALL DEMO DATA SEEDED SUCCESSFULLY!';
