# Audit Service

Service that listens for user events from RabbitMQ queues and records them to MongoDB. Also provides log and audit record retention management.

## Features

- **Event Subscriptions**: Subscribes to user events (UserCreated, UserModified, UserDeleted, UserDisabled) from RabbitMQ queues
- **Audit Logging**: Records all user events to MongoDB `audit_logs` collection
- **Retention Management**: Automatically cleans up old logs and audit records based on configurable retention periods
- **Tenant-Specific Retention**: Supports per-tenant retention period overrides via tenant settings
- **Scheduled Cleanup**: Runs retention cleanup on a configurable schedule (default: every 24 hours)

## Quick Reference

### Environment Variables Summary

| Variable | Required | Default | Description |
|----------|----------|---------|-------------|
| `MONGODB_CONNECTION_STRING` | Yes | - | MongoDB connection for audit logs |
| `POSTGRESQL_CONNECTION_STRING` | No | - | PostgreSQL connection for tenant settings |
| `LOGS_MONGODB_CONNECTION_STRING` | No | Same as audit | MongoDB connection for application logs |
| `RETENTION_CLEANUP_FREQUENCY_HOURS` | No | 24 | How often cleanup runs (hours) |
| `RETENTION_AUDIT_RECORDS_DAYS` | No | 2555 | Audit records retention (7 years) |
| `RETENTION_TRACE_LOGS_HOURS` | No | 48 | Trace logs retention |
| `RETENTION_DEBUG_LOGS_HOURS` | No | 72 | Debug logs retention |
| `RETENTION_INFORMATION_LOGS_DAYS` | No | 30 | Information logs retention |
| `RETENTION_WARNING_LOGS_DAYS` | No | 90 | Warning logs retention |
| `RETENTION_ERROR_LOGS_DAYS` | No | 90 | Error logs retention |
| `RETENTION_CRITICAL_LOGS_DAYS` | No | 90 | Critical logs retention |

### Tenant Setting Keys

| Setting Key | Value Format | Example | Description |
|-------------|--------------|---------|-------------|
| `retention.auditRecords` | Hours (integer string) | `"61320"` | Audit records retention (7 years) |
| `retention.traceLogs` | Hours (integer string) | `"48"` | Trace logs retention |
| `retention.debugLogs` | Hours (integer string) | `"72"` | Debug logs retention |
| `retention.informationLogs` | Hours (integer string) | `"720"` | Information logs retention (30 days) |
| `retention.warningLogs` | Hours (integer string) | `"2160"` | Warning logs retention (90 days) |
| `retention.errorLogs` | Hours (integer string) | `"2160"` | Error logs retention (90 days) |
| `retention.criticalLogs` | Hours (integer string) | `"2160"` | Critical logs retention (90 days) |

**Important**: All tenant settings store retention periods in **hours** as integer strings (e.g., `"720"` for 30 days).

## Configuration

### Environment Variables

#### Required

- **`MONGODB_CONNECTION_STRING`** - MongoDB connection string for audit logs
  - Format: `mongodb://username:password@host:port/database` or `mongodb://host:port/database`
  - Example: `mongodb://admin:password@localhost:27017/audit`
  - **Note**: This is required for the service to start

#### Optional

- **`LOGS_MONGODB_CONNECTION_STRING`** - MongoDB connection string for application logs (if different from audit logs)
  - If not specified, uses the same connection as `MONGODB_CONNECTION_STRING`
  - Example: `mongodb://admin:password@localhost:27017/logs`
  - **Note**: If not configured, application log cleanup will be skipped

- **`POSTGRESQL_CONNECTION_STRING`** - PostgreSQL connection string for accessing tenant settings
  - Required if you want to support tenant-specific retention period overrides
  - Format: `Host=host;Port=5432;Database=BoilerPlateAuth;Username=user;Password=password`
  - Example: `Host=localhost;Port=5432;Database=BoilerPlateAuth;Username=boilerplate;Password=SecurePassword123!`
  - **Note**: If not configured, the service will use default retention periods for all tenants

- **`RABBITMQ_CONNECTION_STRING`** - RabbitMQ connection string (overrides appsettings.json)
  - Format: `amqp://username:password@host:port/vhost`
  - Example: `amqp://admin:password@localhost:5672/`

#### Retention Configuration Environment Variables

All retention periods can be configured via environment variables. These set the **default** retention periods that apply to all tenants unless overridden by tenant-specific settings.

- **`RETENTION_CLEANUP_FREQUENCY_HOURS`** - How often to run retention cleanup (default: 24 hours)
  - Controls the frequency of the scheduled retention cleanup job
  - Example: `RETENTION_CLEANUP_FREQUENCY_HOURS=12` (runs every 12 hours)
  - Example: `RETENTION_CLEANUP_FREQUENCY_HOURS=48` (runs every 2 days)
  - **Minimum**: 1 hour (not recommended to run more frequently)
  - **Recommended**: 24 hours for most use cases

- **`RETENTION_AUDIT_RECORDS_DAYS`** - Retention period for audit records in days (default: 2555 days = 7 years)
  - How long to keep audit log entries in the `audit_logs` collection
  - Example: `RETENTION_AUDIT_RECORDS_DAYS=3650` (10 years)
  - Example: `RETENTION_AUDIT_RECORDS_DAYS=1825` (5 years)
  - **Note**: Audit records are typically kept longer for compliance purposes

- **`RETENTION_TRACE_LOGS_HOURS`** - Retention period for trace/verbose logs in hours (default: 48 hours)
  - How long to keep trace/verbose level application logs
  - Example: `RETENTION_TRACE_LOGS_HOURS=24` (1 day)
  - Example: `RETENTION_TRACE_LOGS_HOURS=12` (12 hours)
  - **Note**: Trace logs are typically kept for the shortest period

- **`RETENTION_DEBUG_LOGS_HOURS`** - Retention period for debug logs in hours (default: 72 hours = 3 days)
  - How long to keep debug level application logs
  - Example: `RETENTION_DEBUG_LOGS_HOURS=48` (2 days)
  - Example: `RETENTION_DEBUG_LOGS_HOURS=168` (1 week)

- **`RETENTION_INFORMATION_LOGS_DAYS`** - Retention period for informational logs in days (default: 30 days)
  - How long to keep information level application logs
  - Example: `RETENTION_INFORMATION_LOGS_DAYS=60` (2 months)
  - Example: `RETENTION_INFORMATION_LOGS_DAYS=90` (3 months)

- **`RETENTION_WARNING_LOGS_DAYS`** - Retention period for warning logs in days (default: 90 days)
  - How long to keep warning level application logs
  - Example: `RETENTION_WARNING_LOGS_DAYS=180` (6 months)
  - Example: `RETENTION_WARNING_LOGS_DAYS=365` (1 year)

- **`RETENTION_ERROR_LOGS_DAYS`** - Retention period for error logs in days (default: 90 days)
  - How long to keep error level application logs
  - Example: `RETENTION_ERROR_LOGS_DAYS=365` (1 year)
  - Example: `RETENTION_ERROR_LOGS_DAYS=730` (2 years)

- **`RETENTION_CRITICAL_LOGS_DAYS`** - Retention period for critical/fatal logs in days (default: 90 days)
  - How long to keep critical/fatal level application logs
  - Example: `RETENTION_CRITICAL_LOGS_DAYS=365` (1 year)
  - Example: `RETENTION_CRITICAL_LOGS_DAYS=730` (2 years)

### Default Retention Periods

| Record Type | Default Retention Period | Environment Variable |
|-------------|-------------------------|---------------------|
| Audit Records | 7 years (2555 days) | `RETENTION_AUDIT_RECORDS_DAYS` |
| Trace/Verbose Logs | 48 hours | `RETENTION_TRACE_LOGS_HOURS` |
| Debug Logs | 72 hours (3 days) | `RETENTION_DEBUG_LOGS_HOURS` |
| Information Logs | 30 days | `RETENTION_INFORMATION_LOGS_DAYS` |
| Warning Logs | 90 days | `RETENTION_WARNING_LOGS_DAYS` |
| Error Logs | 90 days | `RETENTION_ERROR_LOGS_DAYS` |
| Critical/Fatal Logs | 90 days | `RETENTION_CRITICAL_LOGS_DAYS` |

## Tenant-Specific Retention Settings

Each tenant can override the default retention periods by configuring tenant settings in the authentication database. This allows different tenants to have different retention requirements based on their compliance needs, data policies, or business requirements.

### How Tenant-Specific Retention Works

1. **Priority Order**:
   - If a tenant has a specific retention setting configured, that value is used
   - If no tenant-specific setting exists, the default retention period (from environment variables or code defaults) is used
   - If `POSTGRESQL_CONNECTION_STRING` is not configured, all tenants use default retention periods

2. **Setting Format**:
   - All tenant retention settings store values in **hours** as integer strings
   - The service automatically converts hours to `TimeSpan` for comparison
   - Values must be positive integers

3. **Setting Keys**:
   - `retention.auditRecords` - Retention period for audit records (in hours)
   - `retention.traceLogs` - Retention period for trace/verbose logs (in hours)
   - `retention.debugLogs` - Retention period for debug logs (in hours)
   - `retention.informationLogs` - Retention period for information logs (in hours)
   - `retention.warningLogs` - Retention period for warning logs (in hours)
   - `retention.errorLogs` - Retention period for error logs (in hours)
   - `retention.criticalLogs` - Retention period for critical/fatal logs (in hours)

### Tenant Setting Value Examples

| Retention Period | Hours Value | Example Use Case |
|-----------------|------------|------------------|
| 1 day | `24` | Trace logs for development tenant |
| 1 week | `168` | Debug logs for staging tenant |
| 30 days | `720` | Information logs (default) |
| 90 days | `2160` | Warning/Error logs (default) |
| 1 year | `8760` | Extended error log retention |
| 2 years | `17520` | Compliance requirement for audit records |
| 7 years | `61320` | Long-term audit retention (default) |
| 10 years | `87600` | Extended compliance requirement |

### Example: Configure Tenant-Specific Retention

#### Using REST API

```bash
# Set audit records retention to 2 years for a specific tenant
curl -X POST "https://your-app.com/api/TenantSettings" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "tenantId": "123e4567-e89b-12d3-a456-426614174000",
    "key": "retention.auditRecords",
    "value": "17520"
  }'

# Set information logs retention to 60 days
curl -X POST "https://your-app.com/api/TenantSettings" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "tenantId": "123e4567-e89b-12d3-a456-426614174000",
    "key": "retention.informationLogs",
    "value": "1440"
  }'

# Set error logs retention to 1 year
curl -X POST "https://your-app.com/api/TenantSettings" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "tenantId": "123e4567-e89b-12d3-a456-426614174000",
    "key": "retention.errorLogs",
    "value": "8760"
  }'
```

#### Using OData

```bash
# Create tenant setting via OData
curl -X POST "https://your-app.com/odata/TenantSettings" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "TenantId": "123e4567-e89b-12d3-a456-426614174000",
    "Key": "retention.debugLogs",
    "Value": "168"
  }'
```

#### Query Tenant Retention Settings

```bash
# Get all retention settings for a tenant
curl -X GET "https://your-app.com/odata/TenantSettings?$filter=TenantId eq 123e4567-e89b-12d3-a456-426614174000 and startswith(Key, 'retention.')" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN"
```

### Calculating Hours for Tenant Settings

To convert retention periods to hours for tenant settings:

- **Days to Hours**: `days × 24`
  - Example: 30 days = 30 × 24 = 720 hours
  - Example: 90 days = 90 × 24 = 2160 hours

- **Weeks to Hours**: `weeks × 7 × 24`
  - Example: 2 weeks = 2 × 7 × 24 = 336 hours

- **Months to Hours**: `months × 30 × 24` (approximate)
  - Example: 6 months ≈ 6 × 30 × 24 = 4320 hours

- **Years to Hours**: `years × 365 × 24`
  - Example: 7 years = 7 × 365 × 24 = 61,320 hours
  - Example: 10 years = 10 × 365 × 24 = 87,600 hours

**Note**: For exact calculations, account for leap years if needed (add 24 hours per leap year).

## How Retention Works

### Overview

The retention system automatically removes old logs and audit records based on configurable retention periods. The cleanup process runs on a scheduled basis and respects both global defaults and tenant-specific overrides.

### Retention Cleanup Process

#### 1. Scheduled Execution

- **Service**: `RetentionCleanupService` (background hosted service)
- **Frequency**: Configurable via `RETENTION_CLEANUP_FREQUENCY_HOURS` (default: 24 hours)
- **Initial Delay**: Waits 5 minutes after service startup before first run
- **Error Handling**: Continues running even if cleanup fails for individual tenants/logs

#### 2. Audit Records Cleanup

The service processes audit records from the `audit_logs` MongoDB collection:

1. **Get All Tenants**: Queries the collection to get all unique `tenantId` values
2. **For Each Tenant**:
   - Retrieves tenant-specific retention period from tenant settings (if `POSTGRESQL_CONNECTION_STRING` is configured)
   - Falls back to default retention period if no tenant setting exists
   - Calculates cutoff date: `DateTime.UtcNow - retentionPeriod`
   - Deletes all audit records where `createdAt < cutoffDate` for that tenant
3. **Logging**: Logs the number of records deleted per tenant

**Example**:
- Default retention: 7 years (61,320 hours)
- Tenant A setting: `retention.auditRecords = "17520"` (2 years)
- Tenant B: No setting (uses default 7 years)
- Result: Tenant A's audit records older than 2 years are deleted; Tenant B's records older than 7 years are deleted

#### 3. Application Logs Cleanup

The service processes application logs from the `logs` MongoDB collection by log level:

1. **Get Tenant IDs**: Extracts unique tenant IDs from logs (if `Properties.tenantId` exists)
2. **For Each Log Level** (Trace, Debug, Information, Warning, Error, Critical):
   - Gets default retention period for that log level
   - **For Each Tenant** (if tenant IDs found):
     - Retrieves tenant-specific retention period (e.g., `retention.informationLogs`)
     - Falls back to default if no tenant setting exists
     - Calculates cutoff date: `DateTime.UtcNow - retentionPeriod`
     - Deletes logs where `Level = logLevel`, `Timestamp < cutoffDate`, and `Properties.tenantId = tenantId`
   - **For Logs Without Tenant ID**:
     - Uses default retention period for that log level
     - Deletes logs where `Level = logLevel`, `Timestamp < cutoffDate`, and `Properties.tenantId` doesn't exist or is null

**Example**:
- Default information logs retention: 30 days (720 hours)
- Tenant A setting: `retention.informationLogs = "1440"` (60 days)
- Tenant B: No setting (uses default 30 days)
- Result: Tenant A's information logs older than 60 days are deleted; Tenant B's information logs older than 30 days are deleted

#### 4. Tenant Settings Lookup

When `POSTGRESQL_CONNECTION_STRING` is configured:

1. **Query Tenant Setting**: For each tenant and retention type, queries PostgreSQL:
   ```sql
   SELECT "Value" FROM "TenantSettings" 
   WHERE "TenantId" = @tenantId AND "Key" = @key
   ```

2. **Parse Value**: Converts the string value (hours) to `TimeSpan`

3. **Fallback Behavior**:
   - If tenant setting exists and is valid: Use tenant-specific retention
   - If tenant setting doesn't exist: Use default retention
   - If lookup fails (database error): Log warning and use default retention
   - If `POSTGRESQL_CONNECTION_STRING` is not configured: All tenants use defaults

### Retention Period Resolution Logic

```
For each tenant:
  ├─ Is POSTGRESQL_CONNECTION_STRING configured?
  │  ├─ Yes → Query tenant setting for this retention type
  │  │  ├─ Setting exists and is valid? → Use tenant-specific retention
  │  │  └─ Setting missing or invalid? → Use default retention
  │  └─ No → Use default retention
  └─ Calculate cutoff date: DateTime.UtcNow - retentionPeriod
     └─ Delete records/logs older than cutoff date
```

### Performance Considerations

- **Indexes**: The service relies on MongoDB indexes for efficient queries:
  - `audit_logs`: Index on `createdAt` and compound index on `tenantId + createdAt`
  - `logs`: Index on `Timestamp`
- **Batch Processing**: Processes tenants sequentially to avoid overwhelming the database
- **Error Isolation**: Errors for one tenant don't stop processing of other tenants
- **Efficient Queries**: Uses MongoDB's `DeleteManyAsync` with indexed fields for fast deletion

### Example Retention Cleanup Flow

**Scenario**: Cleanup runs at 2025-01-15 02:00:00 UTC

**Tenant A** (has custom settings):
- `retention.auditRecords = "17520"` (2 years)
- `retention.informationLogs = "1440"` (60 days)
- Audit records before 2023-01-15 02:00:00 UTC are deleted
- Information logs before 2024-11-16 02:00:00 UTC are deleted

**Tenant B** (no custom settings):
- Uses default audit retention: 7 years
- Uses default information logs retention: 30 days
- Audit records before 2018-01-15 02:00:00 UTC are deleted
- Information logs before 2024-12-16 02:00:00 UTC are deleted

## MongoDB Collections

### audit_logs

Stores audit records from user events. Structure:

```json
{
  "_id": "ObjectId",
  "eventType": "UserCreatedEvent",
  "userId": "guid",
  "tenantId": "guid",
  "userName": "string",
  "email": "string",
  "eventData": { /* BSON document */ },
  "traceId": "string",
  "referenceId": "string",
  "eventTimestamp": "ISODate",
  "createdAt": "ISODate",
  "metadata": { /* BSON document */ }
}
```

### logs

Stores application logs from Serilog. Structure:

```json
{
  "_id": "ObjectId",
  "Timestamp": "ISODate",
  "Level": "Information|Warning|Error|Debug|Verbose|Fatal",
  "Message": "string",
  "Exception": "string",
  "Properties": {
    "Application": "string",
    "tenantId": "guid (optional)",
    /* other properties */
  }
}
```

## Indexes

The service automatically creates indexes on startup:

- **audit_logs**:
  - `CreatedAt_Index` (descending) - for efficient retention queries
  - `UserId_Index` - for user-specific queries
  - `TenantId_Index` - for tenant-specific queries
  - `EventType_Index` - for event type filtering
  - `TenantId_CreatedAt_Index` (compound) - for tenant-specific retention queries
  - `TraceId_Index` (sparse) - for distributed tracing

- **logs**:
  - `Timestamp_Index` (descending) - for efficient retention queries

## Running the Service

### Development

```bash
dotnet run --project BoilerPlate.Services.Audit
```

### Production

```bash
dotnet BoilerPlate.Services.Audit.dll
```

### Docker

```bash
docker run -d \
  -e MONGODB_CONNECTION_STRING="mongodb://admin:password@mongodb:27017/audit" \
  -e POSTGRESQL_CONNECTION_STRING="Host=postgres;Port=5432;Database=BoilerPlateAuth;Username=boilerplate;Password=SecurePassword123!" \
  -e RABBITMQ_CONNECTION_STRING="amqp://admin:password@rabbitmq:5672/" \
  -e RETENTION_CLEANUP_FREQUENCY_HOURS=24 \
  boilerplate-audit-service
```

## Monitoring

The service logs retention cleanup operations:

- **Information**: Cleanup start/completion, total records deleted
- **Debug**: Per-tenant and per-log-level deletion counts
- **Warning**: Failed tenant setting lookups (falls back to defaults)
- **Error**: Cleanup failures (service continues running)

## Configuration Examples

### Complete Environment Variable Setup

```bash
# Required
export MONGODB_CONNECTION_STRING="mongodb://admin:password@localhost:27017/audit"

# Optional - for tenant-specific retention
export POSTGRESQL_CONNECTION_STRING="Host=localhost;Port=5432;Database=BoilerPlateAuth;Username=boilerplate;Password=SecurePassword123!"

# Optional - for separate logs database
export LOGS_MONGODB_CONNECTION_STRING="mongodb://admin:password@localhost:27017/logs"

# Optional - retention configuration
export RETENTION_CLEANUP_FREQUENCY_HOURS=24
export RETENTION_AUDIT_RECORDS_DAYS=2555
export RETENTION_TRACE_LOGS_HOURS=48
export RETENTION_DEBUG_LOGS_HOURS=72
export RETENTION_INFORMATION_LOGS_DAYS=30
export RETENTION_WARNING_LOGS_DAYS=90
export RETENTION_ERROR_LOGS_DAYS=90
export RETENTION_CRITICAL_LOGS_DAYS=90
```

### Docker Compose Example

```yaml
services:
  audit-service:
    image: boilerplate-audit-service:latest
    environment:
      - MONGODB_CONNECTION_STRING=mongodb://admin:password@mongodb:27017/audit
      - POSTGRESQL_CONNECTION_STRING=Host=postgres;Port=5432;Database=BoilerPlateAuth;Username=boilerplate;Password=SecurePassword123!
      - LOGS_MONGODB_CONNECTION_STRING=mongodb://admin:password@mongodb:27017/logs
      - RETENTION_CLEANUP_FREQUENCY_HOURS=24
      - RETENTION_AUDIT_RECORDS_DAYS=2555
      - RETENTION_INFORMATION_LOGS_DAYS=30
      - RETENTION_ERROR_LOGS_DAYS=365
```

### Kubernetes ConfigMap and Secret Example

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: audit-service-config
data:
  RETENTION_CLEANUP_FREQUENCY_HOURS: "24"
  RETENTION_AUDIT_RECORDS_DAYS: "2555"
  RETENTION_TRACE_LOGS_HOURS: "48"
  RETENTION_DEBUG_LOGS_HOURS: "72"
  RETENTION_INFORMATION_LOGS_DAYS: "30"
  RETENTION_WARNING_LOGS_DAYS: "90"
  RETENTION_ERROR_LOGS_DAYS: "90"
  RETENTION_CRITICAL_LOGS_DAYS: "90"
---
apiVersion: v1
kind: Secret
metadata:
  name: audit-service-secrets
type: Opaque
stringData:
  MONGODB_CONNECTION_STRING: "mongodb://admin:password@mongodb:27017/audit"
  POSTGRESQL_CONNECTION_STRING: "Host=postgres;Port=5432;Database=BoilerPlateAuth;Username=boilerplate;Password=SecurePassword123!"
  LOGS_MONGODB_CONNECTION_STRING: "mongodb://admin:password@mongodb:27017/logs"
```

## Troubleshooting

### Retention cleanup not running

**Symptoms**: No cleanup logs appear, old records are not being deleted

**Solutions**:
- Check that `RetentionCleanupService` is registered in `Program.cs`
- Verify the service is running (check application logs)
- Check `RETENTION_CLEANUP_FREQUENCY_HOURS` environment variable
- Wait at least 5 minutes after service startup (initial delay)
- Check service logs for errors during cleanup execution

**Verification**:
```bash
# Check service logs for retention cleanup messages
docker logs audit-service | grep -i retention
```

### Tenant-specific retention not working

**Symptoms**: All tenants use default retention periods, tenant settings are ignored

**Solutions**:
- Verify `POSTGRESQL_CONNECTION_STRING` is configured correctly
- Test PostgreSQL connection from the audit service
- Check that tenant settings exist in the database:
  ```sql
  SELECT * FROM "TenantSettings" 
  WHERE "Key" LIKE 'retention.%' 
  AND "TenantId" = 'your-tenant-id';
  ```
- Verify setting keys match exactly (case-sensitive): `retention.auditRecords`, `retention.informationLogs`, etc.
- Check service logs for tenant setting lookup errors (warnings)
- Ensure tenant setting values are valid integers (hours)

**Common Issues**:
- Setting key typo: `retention.auditRecord` (missing 's') instead of `retention.auditRecords`
- Invalid value format: `"2 years"` instead of `"17520"` (must be hours as integer string)
- PostgreSQL connection string incorrect or database unreachable

### Logs not being cleaned up

**Symptoms**: Application logs accumulate indefinitely, no cleanup occurs

**Solutions**:
- Verify `LOGS_MONGODB_CONNECTION_STRING` is configured (or uses same as audit logs)
- Check that `logs` collection exists in MongoDB
- Verify log documents have `Timestamp` field (required for retention queries)
- Check that log documents have `Level` field (required for log level filtering)
- Verify MongoDB indexes exist on `Timestamp` field
- Check service logs for cleanup errors specific to logs collection

**Verification**:
```javascript
// In MongoDB shell
use logs
db.logs.findOne() // Check document structure
db.logs.getIndexes() // Verify Timestamp index exists
```

### High MongoDB load during cleanup

**Symptoms**: MongoDB performance degrades during cleanup, slow queries

**Solutions**:
- Increase `RETENTION_CLEANUP_FREQUENCY_HOURS` to run less frequently (e.g., 48 hours)
- Schedule cleanup during off-peak hours (adjust service startup time)
- Ensure indexes are created and optimized:
  - `audit_logs`: Index on `createdAt` (descending)
  - `audit_logs`: Compound index on `tenantId + createdAt`
  - `logs`: Index on `Timestamp` (descending)
- Consider running cleanup during maintenance windows
- Monitor MongoDB performance metrics during cleanup

### Tenant setting lookup errors

**Symptoms**: Logs show warnings about failed tenant setting lookups

**Solutions**:
- Verify PostgreSQL connection string is correct
- Check PostgreSQL database is accessible from audit service
- Verify `TenantSettings` table exists and has correct schema
- Check PostgreSQL connection pool settings
- Review service logs for specific error messages

**Verification**:
```sql
-- Test tenant settings query
SELECT "Value" FROM "TenantSettings" 
WHERE "TenantId" = 'your-tenant-id' 
AND "Key" = 'retention.auditRecords';
```

### Retention periods not applying correctly

**Symptoms**: Records are deleted too early or kept too long

**Solutions**:
- Verify tenant setting values are in **hours** (not days or other units)
- Check that values are positive integers
- Verify environment variable values are correct (check units: days vs hours)
- Review service logs to see which retention period was used for each tenant
- Calculate expected cutoff dates and compare with actual deletion dates

**Example Calculation**:
- Tenant setting: `retention.informationLogs = "1440"` (60 days in hours)
- Current time: 2025-01-15 00:00:00 UTC
- Expected cutoff: 2025-01-15 - 60 days = 2024-11-16 00:00:00 UTC
- Records with `Timestamp < 2024-11-16 00:00:00 UTC` should be deleted
