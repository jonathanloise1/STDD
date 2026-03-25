# Copy Database PROD → DEV

Script to copy production database to development using **Database Copy** method with **mandatory data anonymization** for GDPR compliance.

## ⚠️ Critical Requirements

- **GDPR Compliance**: `02_anonymize-data.sql` MUST be executed after copy
- **Production Stripe IDs**: Will be cleared (incompatible with dev test keys)
- **Personal Data**: All emails and phones will be anonymized
- **No Production Impact**: Uses database copy, production keeps running

## 📁 Included Files

| File | Description |
|------|-------------|
| `copy-prod-to-dev.ps1` | Main PowerShell script |
| `02_anonymize-data.sql` | **[MANDATORY]** Anonymizes personal data for GDPR |
| `03_fix-aadids-dev.sql` | Remaps Entra ID object IDs for dev test users |
| `01_configure-managed-identity.sql` | Configures DEV managed identity users |
| `00_list-users-utility.sql` | Query to verify users and roles |

---

## 🚀 Quick Start

### Step 1: Run Copy Script

```powershell
.\copy-prod-to-dev.ps1
```

**Expected result**:
- ✅ Copy operation completed (~10-30 minutes)
- ✅ Status: "Online"
- ⚠️ SQL scripts may require manual execution via Azure Portal

---

### Step 2: Anonymize Data (MANDATORY ⚠️)

**Azure Portal → SQL-Dev → Query Editor**

- [ ] Open Query Editor
- [ ] Authenticate with Azure AD
- [ ] Execute `02_anonymize-data.sql` (entire file)
- [ ] Verify output shows row counts

**Verification**:
```sql
-- Should return 0 (or only excluded users)
SELECT COUNT(*) FROM Users WHERE Email NOT LIKE '%@dev.MyApp.local';

-- Should return 0
SELECT COUNT(*) FROM OrganizationSubscriptions WHERE StripeCustomerId IS NOT NULL;
```

---

### Step 3: Remap AAD IDs for Dev

**Azure Portal → SQL-Dev → Query Editor**

- [ ] Execute `03_fix-aadids-dev.sql`
- [ ] Verify output shows updated rows

---

### Step 4: Configure Managed Identity

**Azure Portal → SQL-Dev → Query Editor**

- [ ] Execute `01_configure-managed-identity.sql`
- [ ] Verify no errors in output

**Verification**:
```sql
-- Should show MyApp-AS-Dev with roles
SELECT
    dp.name AS UserName,
    dp.type_desc AS UserType,
    r.name AS RoleName
FROM sys.database_principals dp
LEFT JOIN sys.database_role_members drm ON dp.principal_id = drm.member_principal_id
LEFT JOIN sys.database_principals r ON drm.role_principal_id = r.principal_id
WHERE dp.name LIKE 'MyApp%'
ORDER BY dp.name, r.name;
```

---

## 📝 What Gets Anonymized

| Data Type | Before | After |
|-----------|--------|-------|
| **User Emails** | `user@company.com` | `devuser_<guid>@dev.MyApp.local` |
| **Phone Numbers** | `+393331234567` | `+39` + 10 random digits |
| **Stripe Customer IDs** | `cus_xxxxx` | `NULL` |
| **Stripe Subscription IDs** | `sub_xxxxx` | `NULL` |
| **Billing Emails** | Real emails | `billing_*@dev.MyApp.local` |
| **Invitation Emails** | Real emails | `invite_*@dev.MyApp.local` |

**Excluded from anonymization** (for testing):
- `admin@myapp.example`

**NOT Anonymized**:
- User IDs, Organization IDs (required for relationships)
- Dates and timestamps

---

## ⚙️ Configuration

Edit variables at the beginning of `copy-prod-to-dev.ps1`:

```powershell
# Production
$PROD_RG = "RG-Prod"
$PROD_SERVER = "MyAppsqlserverprod"
$PROD_DB = "SQL-Prod"

# Development
$DEV_RG = "RG-Dev"
$DEV_SERVER = "MyAppsqlserverdev"
$DEV_DB = "SQL-Dev"
$DEV_TIER = "S0"
```

---

## ✅ Post-Copy Checklist

### 1. Pre-Copy
- [ ] Azure CLI authenticated (`az login`)
- [ ] Access to both prod and dev resource groups
- [ ] Dev environment apps stopped (recommended)

### 2. Execute Copy
- [ ] Run `.\copy-prod-to-dev.ps1`
- [ ] Wait for completion (~10-30 min)

### 3. Mandatory Steps
- [ ] Execute `02_anonymize-data.sql` (GDPR)
- [ ] Execute `03_fix-aadids-dev.sql` (remap AAD IDs)
- [ ] Execute `01_configure-managed-identity.sql`
- [ ] Verify all scripts completed successfully

### 4. Configuration Files

**Backend**: `src/backend/MyApp.WebApi/appsettings.Development.json`
- [ ] Connection string points to `SQL-Dev`

**Frontend**: `src/frontend/.env.development`
- [ ] `REACT_APP_API_BASE_URL` points to dev API

### 5. Start Environment
- [ ] Start backend: `cd src/backend/MyApp.WebApi && dotnet run`
- [ ] Start frontend: `cd src/frontend && npm start`
- [ ] Swagger available: https://localhost:5001/swagger
- [ ] Frontend available: http://localhost:3000

### 6. Smoke Tests
- [ ] Login with test user
- [ ] Create/view organization
- [ ] Invite a collaborator
- [ ] Check console/logs for errors

### 7. Final Verification
- [ ] No real emails in database
- [ ] No real phone numbers in database
- [ ] No production Stripe IDs in database
- [ ] Apps connect to SQL-Dev successfully

---

## 🔧 Troubleshooting

| Error | Cause | Solution |
|-------|-------|----------|
| `User 'MyApp-AS-Dev' does not exist` | Managed identity not configured | Execute `01_configure-managed-identity.sql` |
| Real emails in database | Anonymization not run | Execute `02_anonymize-data.sql` NOW |
| Accidental email sent | Anonymization skipped | **NEVER SKIP anonymization!** |

### Verify Anonymization Worked

```sql
-- Check emails (should show only dev domains)
SELECT TOP 10 Email FROM Users;

-- Check phones (should show +39000******* format)
SELECT TOP 10 PhoneNumber FROM Users WHERE PhoneNumber IS NOT NULL;

-- Check Stripe IDs (should return 0 rows)
SELECT * FROM OrganizationSubscriptions WHERE StripeCustomerId IS NOT NULL;
```

---

## 📌 Important Notes

- **Method**: Database Copy (cross-server) — production NOT affected
- **Duration**: ~10-30 minutes depending on database size
- **GDPR**: Anonymization is **MANDATORY**, not optional
- **Stripe**: Dev uses Test Mode keys, production IDs cause errors

---

## 🎉 Done!

Dev environment is ready for testing with anonymized data.

**Remember**: NEVER skip `02_anonymize-data.sql` — it's MANDATORY for GDPR compliance and prevents accidental emails/SMS to real users!
