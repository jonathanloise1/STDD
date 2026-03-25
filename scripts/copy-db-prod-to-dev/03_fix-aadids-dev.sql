-- ============================================================================
-- FIX AadId ON SQL-Dev (Azure)
-- ============================================================================
-- Run on SQL-Dev AFTER 02_anonymize-data.sql
-- Purpose: Map real Azure Entra External ID AadIds to DB users so they
--          can log in correctly on the DEV environment.
--
-- PROBLEM: The anonymization copies the DB from PROD, but the AadIds in the
--          DB belong to the PROD Entra tenant, not the shared DEV tenant.
--          On first login, SyncUserAsync doesn't find the user and creates
--          a new one WITHOUT any linked organizations.
--
-- CONFIGURED USERS:
--   admin@myapp.example
-- ============================================================================

PRINT '========================================';
PRINT 'FIX AadId DEV USERS';
PRINT 'Date: ' + CONVERT(VARCHAR, GETDATE(), 120);
PRINT '========================================';
PRINT '';

BEGIN TRANSACTION;

-- ============================================================================
-- 1. CLEANUP DUPLICATES (users created by previous logins with real AadId)
-- ============================================================================
PRINT '1. Cleaning up duplicates from previous logins...';

-- Delete duplicate Users created by login (no org linked)
DELETE FROM Users 
WHERE AadId = '00000000-0000-0000-0000-000000000001'  -- admin real AadId
  AND Id NOT IN (SELECT UserId FROM OrganizationUsers WHERE UserId IS NOT NULL);

PRINT '   OK - Duplicates cleaned up';
PRINT '';

-- ============================================================================
-- 2. FIX PRESERVED USER (email not anonymized)
-- ============================================================================
PRINT '2. Fixing AadId for preserved user...';

-- admin@myapp.example - preserved during anonymization
-- Has the PROD AadId, update it with the real Entra tenant AadId
UPDATE Users 
SET AadId = '00000000-0000-0000-0000-000000000001'
WHERE Email = 'admin@myapp.example';

PRINT '   OK - admin@myapp.example -> AadId 00000000-...';
PRINT '';

-- ============================================================================
-- 3. VERIFICATION
-- ============================================================================
PRINT '========================================';
PRINT 'VERIFICATION RESULTS';
PRINT '========================================';

SELECT 'User' as Type, Email, 
       CAST(AadId AS VARCHAR(36)) as AadId,
       CASE WHEN EXISTS (SELECT 1 FROM OrganizationUsers ou WHERE ou.UserId = u.Id) 
            THEN 'Yes' ELSE 'No' END as HasOrg
FROM Users u
WHERE Email IN ('admin@myapp.example');

COMMIT;

PRINT '';
PRINT '========================================';
PRINT 'SCRIPT COMPLETED SUCCESSFULLY';
PRINT '========================================';
