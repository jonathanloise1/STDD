-- ================================================
-- Post-Restore Script: Fix Managed Identity Users
-- ================================================
-- This script runs automatically after the restore
-- to replace PROD users with DEV users

SET NOCOUNT ON;
GO

PRINT '================================================';
PRINT 'Post-Restore: DEV User Configuration';
PRINT '================================================';
PRINT '';

-- Step 1: Remove PROD user (if exists)
IF EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'MyApp-AS-Prod')
BEGIN
    PRINT '[1/2] Removing PROD user...';
    DROP USER [MyApp-AS-Prod];
    PRINT '      OK - MyApp-AS-Prod removed';
END
ELSE
BEGIN
    PRINT '[1/2] PROD user not found (skip)';
END

PRINT '';

-- Step 2: Create DEV user (if not already exists)
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'MyApp-AS-Dev')
BEGIN
    PRINT '[2/2] Creating DEV user...';
    CREATE USER [MyApp-AS-Dev] FROM EXTERNAL PROVIDER;
    PRINT '      OK - MyApp-AS-Dev created';
END
ELSE
BEGIN
    PRINT '[2/2] DEV user already exists';
END

-- Step 3: Assign roles (idempotent - safe if already assigned)
PRINT '';
PRINT 'Assigning roles...';

-- db_datareader
IF NOT EXISTS (
    SELECT 1 FROM sys.database_role_members rm
    JOIN sys.database_principals dp ON rm.member_principal_id = dp.principal_id
    WHERE dp.name = 'MyApp-AS-Dev' AND USER_NAME(rm.role_principal_id) = 'db_datareader'
)
BEGIN
    ALTER ROLE db_datareader ADD MEMBER [MyApp-AS-Dev];
    PRINT '      OK - db_datareader assigned';
END

-- db_datawriter
IF NOT EXISTS (
    SELECT 1 FROM sys.database_role_members rm
    JOIN sys.database_principals dp ON rm.member_principal_id = dp.principal_id
    WHERE dp.name = 'MyApp-AS-Dev' AND USER_NAME(rm.role_principal_id) = 'db_datawriter'
)
BEGIN
    ALTER ROLE db_datawriter ADD MEMBER [MyApp-AS-Dev];
    PRINT '      OK - db_datawriter assigned';
END

-- db_ddladmin
IF NOT EXISTS (
    SELECT 1 FROM sys.database_role_members rm
    JOIN sys.database_principals dp ON rm.member_principal_id = dp.principal_id
    WHERE dp.name = 'MyApp-AS-Dev' AND USER_NAME(rm.role_principal_id) = 'db_ddladmin'
)
BEGIN
    ALTER ROLE db_ddladmin ADD MEMBER [MyApp-AS-Dev];
    PRINT '      OK - db_ddladmin assigned';
END

PRINT '';
PRINT '================================================';
PRINT 'User configuration completed!';
PRINT '================================================';
PRINT '';

-- Final verification
PRINT 'Configured users:';
SELECT 
    dp.name AS user_name,
    STRING_AGG(roles.name, ', ') AS roles
FROM 
    sys.database_principals dp
LEFT JOIN 
    sys.database_role_members rm ON dp.principal_id = rm.member_principal_id
LEFT JOIN 
    sys.database_principals roles ON rm.role_principal_id = roles.principal_id
WHERE 
    dp.name = 'MyApp-AS-Dev'
GROUP BY dp.name;

GO
