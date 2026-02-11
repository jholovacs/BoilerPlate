# BoilerPlate.Diagnostics.WebApi

Read-only Web API for browsing, searching, and monitoring system activity. Exposes **event logs**, **audit logs**, and **OpenTelemetry metrics** via OData endpoints.

## Data sources (wired by default)

- **Event logs** – `BoilerPlate.Diagnostics.EventLogs.MongoDb` (MongoDB `logs` collection, e.g. Serilog)
- **Audit logs** – `BoilerPlate.Diagnostics.AuditLogs.MongoDb` (MongoDB `audit_logs` collection, same as BoilerPlate.Services.Audit)
- **Metrics** – `BoilerPlate.Diagnostics.Metrics.OpenTelemetry` (scraped from the OTEL collector's Prometheus exporter at port 8889)

## Configuration

Set MongoDB connection for event and audit logs:

- **ConnectionStrings:EventLogsMongoConnection** or **ConnectionStrings:AuditLogsMongoConnection** (or shared **ConnectionStrings:MongoDbConnection**), or
- **MONGODB_CONNECTION_STRING** environment variable

Optional: **EventLogsMongoDb:DatabaseName** / **AuditLogsMongoDb:DatabaseName** (defaults: from connection string path, or `logs` / `audit`).

**Metrics (OTEL collector):** The Diagnostics API scrapes the collector's Prometheus endpoint. Set **OTEL_EXPORTER_OTLP_ENDPOINT** (e.g. `http://otel-collector:4317`) and the Prometheus URL is derived as `http://<host>:8889/metrics`. Or set **DiagnosticsMetrics:PrometheusMetricsUrl** explicitly (e.g. `http://otel-collector:8889/metrics`). **DiagnosticsMetrics:ScrapeIntervalSeconds** (default 15) controls how often metrics are refreshed.

## Authorization and tenant filtering

All OData endpoints require JWT authentication (same tokens as the Authentication WebApi). Configure **JwtSettings** (at least **PublicKey**, **Issuer**, **Audience**) or **JWT_PUBLIC_KEY** so the Diagnostics API can validate tokens.

- **Service Administrator**: sees event logs, audit logs, and metrics for all tenants.
- **Tenant Administrator** (or any other role): sees only data for their tenant. Filtering is by:
  - **AuditLogs**: `TenantId` equals the user's tenant.
  - **EventLogs**: `Properties` JSON contains the tenant ID (e.g. from Serilog enrichers).
  - **Metrics**: `Attributes` or `Source` contains the tenant ID.

## OData endpoints

- `GET /odata/EventLogs` – query event logs (filter, orderby, select, top, skip, count)
- `GET /odata/EventLogs({id})` – single event log by key
- `GET /odata/AuditLogs` – query audit logs
- `GET /odata/AuditLogs({id})` – single audit log by key (string id)
- `GET /odata/Metrics` – query metric points
- `GET /odata/Metrics({id})` – single metric by key

All endpoints are read-only.
