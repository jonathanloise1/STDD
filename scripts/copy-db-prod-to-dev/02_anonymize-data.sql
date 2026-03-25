-- ============================================================================
-- DATA ANONYMIZATION SCRIPT — POST PROD -> DEV COPY
-- ============================================================================
-- Purpose: Anonymize CRITICAL data for GDPR compliance and dev safety
--
-- EXCLUSIONS (users NOT anonymized, kept for testing):
--   - admin@myapp.example (Admin)
-- ============================================================================

PRINT '========================================';
PRINT 'STARTING DATA ANONYMIZATION (DEV)';
PRINT 'Date: ' + CONVERT(VARCHAR, GETDATE(), 120);
PRINT '========================================';
PRINT '';

-- ============================================================================
-- 1. ANONYMIZE USER EMAILS
-- ============================================================================
-- CRITICAL: Prevents sending real emails from dev environment
PRINT '1. Anonymizing user emails...';

UPDATE Users
SET 
    Email = 'devuser_' + CAST(NEWID() AS VARCHAR(36)) + '@dev.MyApp.local',
    UpdatedAt = GETDATE()
WHERE Email NOT LIKE '%@dev.MyApp.local'
  AND Email NOT IN ('admin@myapp.example');

DECLARE @EmailCount INT = @@ROWCOUNT;
PRINT '   OK - ' + CAST(@EmailCount AS VARCHAR) + ' user emails anonymized';
PRINT '';

-- ============================================================================
-- 2. ANONYMIZE PHONE NUMBERS
-- ============================================================================
-- CRITICAL: Prevents sending SMS/OTP to real phone numbers
PRINT '2. Anonymizing phone numbers...';

UPDATE Users
SET 
    PhoneNumber = '+3900000' + RIGHT('00000' + CAST(ABS(CHECKSUM(NEWID())) % 100000 AS VARCHAR(5)), 5),
    UpdatedAt = GETDATE()
WHERE PhoneNumber IS NOT NULL
  AND PhoneNumber <> ''
  AND PhoneNumber NOT LIKE '+3900000%'
  AND Email NOT IN ('admin@myapp.example');

DECLARE @PhoneCount INT = @@ROWCOUNT;
PRINT '   OK - ' + CAST(@PhoneCount AS VARCHAR) + ' phone numbers anonymized';
PRINT '';

-- ============================================================================
-- 3. RESET STRIPE IDS - SUBSCRIPTIONS
-- ============================================================================
-- CRITICAL: Production Stripe IDs cause errors in dev with Test keys
PRINT '3. Resetting Stripe IDs in subscriptions...';

UPDATE OrganizationSubscriptions
SET 
    StripeCustomerId = '',
    StripeSubscriptionId = '',
    StripePriceId = '',
    UpdatedAt = GETDATE()
WHERE StripeCustomerId IS NOT NULL AND StripeCustomerId <> '';

DECLARE @OrgSubCount INT = @@ROWCOUNT;
PRINT '   OK - ' + CAST(@OrgSubCount AS VARCHAR) + ' organization subscriptions reset';
PRINT '';

-- ============================================================================
-- 4. ANONYMIZE BILLING EMAILS
-- ============================================================================
-- IMPORTANT: Prevents sending invoices/notifications to real emails
PRINT '4. Anonymizing billing emails...';

UPDATE Organizations
SET 
    BillingEmail = 'billing_org_' + CAST(Id AS VARCHAR(36)) + '@dev.MyApp.local',
    CertifiedEmail = NULL -- Certified email not needed in dev
WHERE BillingEmail NOT LIKE '%@dev.MyApp.local'
  AND Id NOT IN (
    SELECT DISTINCT o.Id 
    FROM Organizations o
    INNER JOIN OrganizationUsers ou ON o.Id = ou.OrganizationId
    INNER JOIN Users u ON ou.UserId = u.Id
    WHERE u.Email IN ('admin@myapp.example')
  );

DECLARE @OrgBillingCount INT = @@ROWCOUNT;
PRINT '   OK - ' + CAST(@OrgBillingCount AS VARCHAR) + ' organization billing emails anonymized';
PRINT '';

-- ============================================================================
-- 5. ANONYMIZE PENDING MEMBER EMAILS (OrganizationUsers)
-- ============================================================================
-- IMPORTANT: Prevents sending invitation emails to real addresses
PRINT '5. Anonymizing pending member emails...';

UPDATE OrganizationUsers
SET 
    Email = 'pending_' + CAST(Id AS VARCHAR(36)) + '@dev.MyApp.local'
WHERE Status = 'Pending'
  AND Email NOT LIKE '%@dev.MyApp.local'
  AND Email NOT IN ('admin@myapp.example');

DECLARE @InviteCount INT = @@ROWCOUNT;
PRINT '   OK - ' + CAST(@InviteCount AS VARCHAR) + ' pending member emails anonymized';
PRINT '';

-- ============================================================================
-- 6. RESTORE SEED DATA EMAILS (for deterministic testing)
-- ============================================================================
-- Restores seed user emails (anonymized in previous steps) so they are
-- recognizable and usable for tests.
PRINT '6. Restoring seed data emails...';

-- Seed Users (Contoso)
UPDATE Users SET Email = 'mario.rossi@contoso.it' WHERE Id = '0e79ab57-9d92-4460-abf9-2bc057513a6b';
UPDATE Users SET Email = 'giulia.bianchi@contoso.it' WHERE Id = '6f8d9e0a-4a5b-6d9f-0f1a-3b4c5d6e7f8a';
UPDATE Users SET Email = 'paolo.verdi@contoso.it' WHERE Id = '7a9e0f1b-5b6c-7e0a-1f2b-4c5d6e7f8a9b';
UPDATE Users SET Email = 'francesca.neri@contoso.it' WHERE Id = '8b0f1a2c-6c7d-8f1b-2f3c-5d6e7f8a9b0c';
UPDATE Users SET Email = 'marco.conti@contoso.it' WHERE Id = 'd15e6f7a-1b2c-3d4f-7a8b-0c1d2e3f4a5b';

-- Seed Users (Fabrikam)
UPDATE Users SET Email = 'sofia.russo@fabrikam.it' WHERE Id = 'cc72784c-2243-473e-b4d5-bdce01a1eb3f';
UPDATE Users SET Email = 'lorenzo.ferri@fabrikam.it' WHERE Id = 'ad2b3c4e-8e9f-0a2d-4e5f-7a8b9c0d1e2f';
UPDATE Users SET Email = 'valeria.gallo@fabrikam.it' WHERE Id = 'be3c4d5f-9f0a-1b3e-5f6a-8b9c0d1e2f3a';
UPDATE Users SET Email = 'matteo.colombo@fabrikam.it' WHERE Id = 'cf4d5e6a-0a1b-2c4f-6a7b-9c0d1e2f3a4b';
UPDATE Users SET Email = 'elena.basile@fabrikam.it' WHERE Id = 'e26f708b-2c3d-4e5f-8b9c-1d2e3f4a5b6c';

DECLARE @SeedCount INT = @@ROWCOUNT;
PRINT '   OK - 10 seed users restored';
PRINT '';

-- ============================================================================
-- FINAL SUMMARY
-- ============================================================================
PRINT '========================================';
PRINT 'ANONYMIZATION COMPLETED';
PRINT '========================================';
PRINT 'User emails:                ' + CAST(@EmailCount AS VARCHAR);
PRINT 'Phone numbers:              ' + CAST(@PhoneCount AS VARCHAR);
PRINT 'Organization subscriptions: ' + CAST(@OrgSubCount AS VARCHAR);
PRINT 'Org billing emails:         ' + CAST(@OrgBillingCount AS VARCHAR);
PRINT 'Pending member emails:      ' + CAST(@InviteCount AS VARCHAR);
PRINT '========================================';
PRINT '';

-- ============================================================================
-- POST-ANONYMIZATION VERIFICATION
-- ============================================================================
PRINT 'Checking for remaining sensitive data...';
PRINT '';

-- Check for remaining real emails (excluding dev domains)
DECLARE @RealEmailCount INT;

SELECT @RealEmailCount = COUNT(*) 
FROM Users 
WHERE Email NOT LIKE '%@dev.MyApp.local'
  AND Email NOT LIKE '%@example.com'
  AND Email NOT LIKE '%@test.com'
  AND Email NOT LIKE '%@contoso.it'
  AND Email NOT LIKE '%@fabrikam.it'
  AND Email NOT IN ('admin@myapp.example');

IF @RealEmailCount > 0
BEGIN
    PRINT 'WARNING: Found ' + CAST(@RealEmailCount AS VARCHAR) + ' emails that appear to be real!';
    PRINT '   Run: SELECT TOP 10 Id, Email FROM Users WHERE Email NOT LIKE ''%@dev.MyApp.local''';
END
ELSE
BEGIN
    PRINT 'OK - No real emails found';
END

-- Check for remaining Stripe IDs
DECLARE @StripeCount INT;

SELECT @StripeCount = COUNT(*) 
FROM OrganizationSubscriptions 
WHERE StripeCustomerId IS NOT NULL AND StripeCustomerId <> '';

IF @StripeCount > 0
BEGIN
    PRINT 'WARNING: Found ' + CAST(@StripeCount AS VARCHAR) + ' remaining Stripe IDs!';
END
ELSE
BEGIN
    PRINT 'OK - All Stripe IDs removed';
END

PRINT '';
PRINT '========================================';
PRINT 'SCRIPT COMPLETED SUCCESSFULLY';
PRINT '========================================';
