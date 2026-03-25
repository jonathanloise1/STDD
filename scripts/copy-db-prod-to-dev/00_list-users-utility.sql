-- ================================================
-- Script to list database users and roles
-- ================================================
-- Run this script on PROD and DEV to see the configuration

-- 1. List all database users
SELECT 
    'DATABASE USERS' AS [Section],
    dp.name AS [User Name],
    dp.type_desc AS [User Type],
    dp.authentication_type_desc AS [Authentication Type],
    dp.create_date AS [Created Date]
FROM sys.database_principals dp
WHERE dp.type IN ('S', 'U', 'E', 'X')  -- S=SQL User, U=Windows User, E=External User, X=External Group
    AND dp.name NOT IN ('dbo', 'guest', 'INFORMATION_SCHEMA', 'sys', 'public')
ORDER BY dp.name;

GO

-- 2. Roles assigned to each user
SELECT 
    'USER ROLE MEMBERSHIPS' AS [Section],
    USER_NAME(rm.member_principal_id) AS [User Name],
    USER_NAME(rm.role_principal_id) AS [Role Name]
FROM sys.database_role_members rm
WHERE USER_NAME(rm.member_principal_id) NOT IN ('dbo', 'guest', 'INFORMATION_SCHEMA', 'sys', 'public')
ORDER BY USER_NAME(rm.member_principal_id), USER_NAME(rm.role_principal_id);

GO

-- 3. Explicit permissions assigned to users (beyond roles)
SELECT 
    'USER EXPLICIT PERMISSIONS' AS [Section],
    USER_NAME(p.grantee_principal_id) AS [User Name],
    p.permission_name AS [Permission],
    p.state_desc AS [State],
    OBJECT_NAME(p.major_id) AS [Object Name]
FROM sys.database_permissions p
WHERE p.grantee_principal_id > 4  -- Exclude system users
    AND USER_NAME(p.grantee_principal_id) NOT IN ('dbo', 'guest', 'INFORMATION_SCHEMA', 'sys', 'public')
ORDER BY USER_NAME(p.grantee_principal_id), p.permission_name;

GO

-- 4. Summary in readable format
SELECT 
    'SUMMARY' AS [Section],
    dp.name AS [User],
    dp.type_desc AS [Type],
    STRING_AGG(USER_NAME(rm.role_principal_id), ', ') AS [Roles]
FROM sys.database_principals dp
LEFT JOIN sys.database_role_members rm ON dp.principal_id = rm.member_principal_id
WHERE dp.type IN ('S', 'U', 'E', 'X')
    AND dp.name NOT IN ('dbo', 'guest', 'INFORMATION_SCHEMA', 'sys', 'public')
GROUP BY dp.name, dp.type_desc
ORDER BY dp.name;
