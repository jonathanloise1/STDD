# AUD-INTERCEPT-001 — Interceptor captures Create, Update, Delete

| Field | Value |
|-------|-------|
| **User Story** | US-AUD-01 |
| **Type** | Happy path |
| **Priority** | High |
| **Automated** | Yes |

## Description

Verify that the EF Core SaveChanges interceptor automatically creates AuditLog records for every Create, Update, and Delete operation on tracked entities, within the same transaction.

## Preconditions

- [x] User is authenticated with Editor role
- [x] The user has at least one uploaded policy
- [x] AuditLog table is empty or has a known baseline count

## Test Data

| Operation | Entity | Details |
|-----------|--------|---------|
| Create | Policy | InsurerName = "Zurich", Tag = "rc-auto" |
| Update | Policy | Change InsurerName "Zurich" → "Helvetia" |
| Delete | Policy | Soft-delete the policy |

## Steps

1. Create a Policy via the API.
2. Query the AuditLog for this entity.
3. Verify a Create audit record exists with NewValues containing `{"InsurerName":"Zurich"}`.
4. Update the Policy to change InsurerName to "Helvetia".
5. Query the AuditLog again.
6. Verify an Update audit record exists with OldValues `{"InsurerName":"Zurich"}` and NewValues `{"InsurerName":"Helvetia"}`.
7. Verify ChangedProperties includes `["InsurerName"]`.
8. Delete the Policy.
9. Verify a Delete audit record exists with OldValues containing the last known state.
10. Verify all 3 records have the correct UserId and Timestamp.

## Expected Results

- [x] Create action: OldValues is null, NewValues contains all properties
- [x] Update action: OldValues and NewValues contain only changed properties
- [x] Update action: ChangedProperties lists the modified property names
- [x] Delete action: OldValues contains the entity's last state, NewValues is null
- [x] UserId matches the authenticated user
- [x] Timestamp is UTC and within expected range
- [x] Audit records are in the same transaction (if entity save fails, no audit generated)
- [x] Only scalar properties are serialized (no navigation properties)

## API Calls

| Method | Endpoint | Expected Status |
|--------|----------|-----------------|
| POST | `/api/policies` | 201 Created |
| PUT | `/api/policies/{policyId}` | 200 OK |
| DELETE | `/api/policies/{policyId}` | 200 OK |
| GET | `/api/organizations/{orgId}/audit?entityType=Policy&entityId={policyId}` | 200 OK |
