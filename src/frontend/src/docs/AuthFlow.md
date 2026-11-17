# Azure AD B2C Authentication Flow

This document explains the optimized authentication and token refresh flow implemented in the MyApp application.

## Authentication Flow

1. **Initial Login**
   - User accesses the application
   - App checks for existing tokens
   - If no valid token exists, user is redirected to Azure AD B2C login
   - Upon successful authentication, tokens are stored:
     - ID Token (for user identity)
     - Access Token (for API access)
     - Refresh Token (managed by MSAL internally)

2. **Token Usage**
   - Access Token is included in API requests via Authorization header
   - Token is validated for proper audience and expiration before use

3. **Token Refresh**
   - Uses MSAL's built-in token refresh mechanisms
   - Automatically handles token refresh through multiple methods:
     - Proactive refresh before expiration (every minute check)
     - Reactive refresh when an expired token is detected
     - Automatic retry of failed requests after successful token refresh

## Key Components

### Token Management

- **MSAL Silent Token Acquisition**: Uses `acquireTokenSilent()` to get new tokens using refresh tokens
- **Preemptive Token Refresh**: Checks token expiration periodically and refreshes tokens before they expire
- **Centralized Token Store**: All tokens are managed by the auth context

### API Request Flow

1. Request is made with current token
2. If token is expired or invalid:
   - Trigger token refresh event
   - Wait briefly for token refresh
   - Retry request with new token
   - If refresh fails, redirect to login

### Data Synchronization

- **User Data**: Synced only when:
  - Essential data is missing
  - After token refresh (if needed)
  - Not on every page navigation

- **Organization Data**: Uses smart caching:
  - Data is cached for 30 minutes
  - Cache is invalidated after token refresh
  - Requests in flight are properly tracked to avoid duplicates

## Optimizations

1. **Reduced API Calls**:
   - Time-based caching to prevent frequent refetches
   - Only sync when data is missing or stale

2. **Improved Token Management**:
   - Preemptive token refresh to avoid expired tokens
   - Queue mechanism for concurrent requests during token refresh
   - Automatic retry of failed requests after token refresh

3. **Resilient Error Handling**:
   - Graceful degradation when APIs are unavailable
   - Proper error dispatching for user feedback
   - Automatic recovery from unauthorized errors

## Event System

Communication between components is handled by a custom event system:

- `auth-token-refresh-needed`: Signals that a token needs refreshing
- `auth-token-refreshed`: Signals that a token was successfully refreshed
- `auth-token-error`: Signals authentication failure requiring re-login
- `api-timeout-error`: Signals API timeout for user feedback

## Testing the Flow

1. **Normal Flow**: Login and navigate through the application
2. **Token Expiration**: Will handle automatically through preemptive refresh
3. **Force Token Refresh**: Clear access token from localStorage but keep ID token
4. **Complete Re-auth**: Clear all tokens to force a full re-authentication 