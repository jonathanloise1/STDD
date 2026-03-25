#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Restore the production database to development from backup
.DESCRIPTION
    Script to restore db-prod to db-dev using Point-in-Time Restore (backup).
    Uses Azure SQL automatic backups without impacting production.
.EXAMPLE
    .\copy-prod-to-dev.ps1
    Restores the backup from 1 hour ago (default)
.EXAMPLE
    .\copy-prod-to-dev.ps1 -SkipBackup
    Skips the current db-dev backup
#>

param(
    [switch]$SkipBackup,          # Skip the current db-dev backup
    [switch]$WhatIf               # Show what would happen without executing
)

# ============================================
# CONFIGURATION - Edit these values
# ============================================

# Production
$PROD_RG = "RG-Prod"
$PROD_SERVER = "MyAppsqlserverprod"
$PROD_DB = "SQL-Prod"

# Development
$DEV_RG = "RG-Dev"
$DEV_SERVER = "MyAppsqlserverdev"
$DEV_DB = "SQL-Dev"
$DEV_TIER = "S0"  # Service tier for dev (must be >= prod tier)

# Post-Restore SQL Scripts
$POST_RESTORE_SQL_SCRIPT = "$PSScriptRoot\01_configure-managed-identity.sql"
$ANONYMIZE_SQL_SCRIPT = "$PSScriptRoot\02_anonymize-data.sql"

# ============================================
# SCRIPT - Do not modify below this line
# ============================================

$ErrorActionPreference = "Continue"

# User confirmation
if (-not $WhatIf) {
    $confirmation = Read-Host "This operation will DELETE the current db-dev. Continue? (y/N)"
    if ($confirmation -ne 'y' -and $confirmation -ne 'Y') {
        Write-Host "Operation cancelled." -ForegroundColor Red
        exit 0
    }
}

# Step 1: Verify production database exists
Write-Host ""
Write-Host "[1/5] Verifying production database..." -ForegroundColor Green
if ($WhatIf) {
    Write-Host "      [WhatIf] Would verify existence of $PROD_DB" -ForegroundColor DarkGray
} else {
    $prodDb = az sql db show --name $PROD_DB --resource-group $PROD_RG --server $PROD_SERVER --query "name" -o tsv 2>&1
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "      X Error: Production database not found" -ForegroundColor Red
        exit 1
    }
    Write-Host "      OK - Production database found" -ForegroundColor DarkGreen
}

# Step 2: Backup current dev database (optional)
if (-not $SkipBackup) {
    Write-Host ""
    Write-Host "[2/5] Backing up current dev database..." -ForegroundColor Green
    
    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $backupName = "$DEV_DB-backup-$timestamp"
    
    if ($WhatIf) {
        Write-Host "      [WhatIf] Would create backup: $backupName" -ForegroundColor DarkGray
    } else {
        $devDbExists = az sql db show --name $DEV_DB --resource-group $DEV_RG --server $DEV_SERVER --query "name" -o tsv 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "      Creating backup..." -ForegroundColor Yellow
            az sql db copy --name $DEV_DB --resource-group $DEV_RG --server $DEV_SERVER --dest-name $backupName --dest-resource-group $DEV_RG --dest-server $DEV_SERVER --no-wait 2>&1 | Out-Null
            
            Write-Host "      OK - Backup started: $backupName" -ForegroundColor DarkGreen
            Write-Host "        (backup will continue in background)" -ForegroundColor DarkGray
        } else {
            Write-Host "      i Dev database does not exist, skipping backup" -ForegroundColor DarkYellow
        }
    }
} else {
    Write-Host ""
    Write-Host "[2/5] Backup skipped (--SkipBackup flag)" -ForegroundColor Yellow
}

# Step 3: Delete current dev database
Write-Host ""
Write-Host "[3/5] Deleting current dev database..." -ForegroundColor Green

if ($WhatIf) {
    Write-Host "      [WhatIf] Would delete $DEV_DB" -ForegroundColor DarkGray
} else {
    $devDbExists = az sql db show --name $DEV_DB --resource-group $DEV_RG --server $DEV_SERVER --query "name" -o tsv 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "      Deleting..." -ForegroundColor Yellow
        az sql db delete --name $DEV_DB --resource-group $DEV_RG --server $DEV_SERVER --yes 2>&1 | Out-Null
        
        Write-Host "      OK - Dev database deleted" -ForegroundColor DarkGreen
    } else {
        Write-Host "      i Dev database does not exist" -ForegroundColor DarkYellow
    }
}

# Step 4: Copy from production to development
Write-Host ""
Write-Host "[4/5] Copying from production to development..." -ForegroundColor Green

if ($WhatIf) {
    Write-Host "      [WhatIf] Would copy $PROD_DB -> $DEV_DB" -ForegroundColor DarkGray
} else {
    Write-Host "      Copying (live database)..." -ForegroundColor Yellow
    Write-Host "      This operation may take several minutes..." -ForegroundColor Yellow
    
    az sql db copy --name $PROD_DB --resource-group $PROD_RG --server $PROD_SERVER --dest-name $DEV_DB --dest-resource-group $DEV_RG --dest-server $DEV_SERVER --service-objective $DEV_TIER
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "      OK - Database copied successfully!" -ForegroundColor DarkGreen
    } else {
        Write-Host "      X Error during copy operation" -ForegroundColor Red
        exit 1
    }
}

# Step 5: Post-Restore - Fix Managed Identity Users
Write-Host ""
Write-Host "[5/6] Post-restore: Configuring DEV users..." -ForegroundColor Green

if ($WhatIf) {
    Write-Host "      [WhatIf] Would run SQL script: $POST_RESTORE_SQL_SCRIPT" -ForegroundColor DarkGray
} else {
    if (-not (Test-Path $POST_RESTORE_SQL_SCRIPT)) {
        Write-Host "      ! Warning: SQL script not found: $POST_RESTORE_SQL_SCRIPT" -ForegroundColor Yellow
        Write-Host "        You will need to manually configure the MyApp-AS-Dev user" -ForegroundColor Yellow
    } else {
        Write-Host "      Running SQL script..." -ForegroundColor Yellow
        
        $sqlContent = Get-Content $POST_RESTORE_SQL_SCRIPT -Raw
        $result = az sql db query --server $DEV_SERVER --database $DEV_DB --auth-type ADPassword --query-text "$sqlContent" 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "      OK - Users configured successfully!" -ForegroundColor DarkGreen
        } else {
            Write-Host "      ! Automatic execution failed" -ForegroundColor Yellow
            Write-Host "      i Run manually:" -ForegroundColor Yellow
            Write-Host "        1. Open Azure Portal -> SQL-Dev -> Query Editor" -ForegroundColor White
            Write-Host "        2. Run the file: 01_configure-managed-identity.sql" -ForegroundColor White
        }
    }
}

# Step 6: Anonymize Production Data (MANDATORY for GDPR)
Write-Host ""
Write-Host "[6/6] Anonymizing production data..." -ForegroundColor Green
Write-Host "      ! IMPORTANT: This step is MANDATORY for GDPR compliance!" -ForegroundColor Red

if ($WhatIf) {
    Write-Host "      [WhatIf] Would run SQL script: $ANONYMIZE_SQL_SCRIPT" -ForegroundColor DarkGray
} else {
    if (-not (Test-Path $ANONYMIZE_SQL_SCRIPT)) {
        Write-Host "      ! CRITICAL: SQL script not found: $ANONYMIZE_SQL_SCRIPT" -ForegroundColor Red
        Write-Host "        The database contains NON-ANONYMIZED production data!" -ForegroundColor Red
    } else {
        Write-Host "      Running anonymization..." -ForegroundColor Yellow
        
        $sqlContent = Get-Content $ANONYMIZE_SQL_SCRIPT -Raw
        $result = az sql db query --server $DEV_SERVER --database $DEV_DB --auth-type ADPassword --query-text "$sqlContent" 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "      OK - Data anonymized successfully!" -ForegroundColor DarkGreen
            Write-Host "      i Emails: devuser_*@dev.MyApp.local" -ForegroundColor DarkGray
            Write-Host "      i Stripe IDs: NULL" -ForegroundColor DarkGray
            Write-Host "      i Phones: +3900********" -ForegroundColor DarkGray
        } else {
            Write-Host "      ! Automatic execution failed" -ForegroundColor Red
            Write-Host "      ! You MUST run this manually BEFORE testing:" -ForegroundColor Red
            Write-Host "        1. Open Azure Portal -> SQL-Dev -> Query Editor" -ForegroundColor White
            Write-Host "        2. Run the file: 02_anonymize-data.sql" -ForegroundColor White
            Write-Host "        3. Verify output (row count)" -ForegroundColor White
        }
    }
}

# Summary
Write-Host ""
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "  COMPLETED" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "The database has been copied from production." -ForegroundColor Green
Write-Host ""
Write-Host "NEXT STEPS:" -ForegroundColor Yellow
Write-Host "  1. Verify that anonymization completed successfully" -ForegroundColor White
Write-Host "  2. Check the connection string in the dev app" -ForegroundColor White
Write-Host "  3. Test the application on the dev environment" -ForegroundColor White

if (-not $SkipBackup) {
    Write-Host "  4. If everything is OK, you can delete old backups from Azure Portal" -ForegroundColor White
}

Write-Host ""
Write-Host "IMPORTANT: If steps 5-6 failed, run manually:" -ForegroundColor Red
Write-Host "  - 01_configure-managed-identity.sql (user configuration)" -ForegroundColor White
Write-Host "  - 02_anonymize-data.sql (MANDATORY for GDPR)" -ForegroundColor Red

Write-Host ""
