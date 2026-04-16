# Blocks Localization

> .NET 9 localization management platform with a Web API and worker service for translation workflows, UILM import/export, and timeline/audit operations.

![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet&logoColor=white)
![Docker](https://img.shields.io/badge/docker-ready-2496ED?logo=docker&logoColor=white)
![License](https://img.shields.io/badge/license-MIT-green.svg)

## Table of Contents

- [Overview](#overview)
- [Features](#features)
- [Architecture Overview](#architecture-overview)
- [Controllers / Endpoints](#controllers--endpoints)
- [Tech Stack](#tech-stack)
- [Prerequisites](#prerequisites)
- [Installation](#installation)
- [Configuration — Environment Variables](#configuration--environment-variables)
- [Running the Project Locally](#running-the-project-locally)
- [Usage](#usage)
- [Contributing](#contributing)
- [License](#license)
- [Maintainers](#maintainers)

## Overview

`blocks-localization-net` provides centralized localization lifecycle management for applications built on the SELISE `<Blocks/>` ecosystem. It exposes APIs for managing modules, languages, keys, glossaries, timeline history, and asynchronous translation/import/export workflows, while a dedicated worker processes background queue events.

This project is designed for platform teams, backend engineers, and product teams who need auditable, scalable multilingual content management.

Key use cases:
- Manage localization modules, languages, keys, and glossary terms
- Trigger and track bulk/single-key AI-assisted translations
- Import/export UILM language files through async jobs
- Retrieve key-level and operation-level timeline history
- Integrate localization workflows into distributed event-driven systems

## Features

- **Module Management** - Create and list localization modules per project context.
- **Language Management** - Add, list, delete, and set default languages.
- **Key Management** - Create, update, delete, and bulk save localization keys.
- **Glossary Management** - Store reusable glossary/context terms for translation consistency.
- **AI Translation Suggestions** - Generate translation suggestions via external AI completion API.
- **Async Translation Jobs** - Queue translation tasks for all keys or individual keys.
- **UILM Import/Export Pipelines** - Execute background import/export and generate downloadable artifacts.
- **Timeline & Audit APIs** - Query key timeline, localization timeline, and operation-specific timeline views.
- **History & Artifact Tracking** - Retrieve exported file metadata and language file generation history.
- **Pluggable Messaging Provider** - Auto-select RabbitMQ or Azure Service Bus by message connection string scheme.

## Architecture Overview

```text
+--------------------------------------+
| External Clients / UI / Integrations |
+--------------------------------------+
									 |
									 v
+--------------------------------------+
| blocks-localization-api              |
| ASP.NET Core Web API                 |
+--------------------------------------+
									 |
									 v
+--------------------------------------+
| Message Broker                       |
| Azure Service Bus or RabbitMQ        |
+--------------------------------------+
									 |
									 v
+--------------------------------------+
| blocks-localization-worker           |
| Background event consumers           |
+--------------------------------------+
									 |
									 v
+--------------------------------------+
| Shared Infrastructure                |
| MongoDB | Redis-compatible cache     |
| Azure Key Vault | Log/Metric/Trace   |
+--------------------------------------+
```

The solution is organized into four projects: `Api` (HTTP endpoints), `Worker` (queue consumers), `DomainService` (business logic, repositories, entities, validators), and `XUnitTest` (unit/integration tests). Shared logic in `DomainService` is reused by both runtime services for consistent validation, persistence, and event processing.

## Controllers / Endpoints

```text
Auth legend:
- Public              : No auth attribute on endpoint
- [Authorize]         : ASP.NET Core authorization required
- [ProtectedEndPoint] : Blocks platform protected endpoint attribute
```

### AssistantController — /Assistant/

#### Translation Assistant

| Method | Path | Auth | Description |
|---|---|---|---|
| POST | `/Assistant/GetTranslationSuggestion` | [Authorize] | Returns an AI-generated translation suggestion for provided source text/context. |

### ConfigController — /Config/

#### Webhook Configuration

| Method | Path | Auth | Description |
|---|---|---|---|
| POST | `/Config/SaveWebHook` | [ProtectedEndPoint] | Saves webhook configuration for localization-related notifications/events. |

### GlossaryController — /Glossary/

#### Glossary CRUD

| Method | Path | Auth | Description |
|---|---|---|---|
| POST | `/Glossary/Save` | [Authorize] | Creates or updates a glossary entry. |
| GET | `/Glossary/Gets` | [Authorize] | Retrieves glossary entries with query filters/pagination inputs. |
| DELETE | `/Glossary/Delete` | [Authorize] | Deletes a glossary entry by `ItemId`. |

### KeyController — /Key/

#### Core Key Operations

| Method | Path | Auth | Description |
|---|---|---|---|
| POST | `/Key/Save` | [ProtectedEndPoint] | Creates or updates a single localization key. |
| POST | `/Key/SaveKeys` | [ProtectedEndPoint] | Bulk creates or updates multiple localization keys. |
| POST | `/Key/Gets` | [ProtectedEndPoint] | Retrieves localization keys using request filters. |
| POST | `/Key/GetsByKeyNames` | [ProtectedEndPoint] | Retrieves keys by an explicit key-name list. |
| GET | `/Key/Get` | [ProtectedEndPoint] | Retrieves a single key by query parameters. |
| DELETE | `/Key/Delete` | [ProtectedEndPoint] | Deletes a key by `ItemId`. |
| GET | `/Key/GetSuggestedGlossaries` | [ProtectedEndPoint] | Returns glossary suggestions relevant to a key/query. |

#### Timeline & Audit

| Method | Path | Auth | Description |
|---|---|---|---|
| GET | `/Key/GetTimeline` | [ProtectedEndPoint] | Returns paginated key timeline history. |
| GET | `/Key/GetLocalizationTimeline` | [Authorize] | Returns localization operation-level timeline overview. |
| GET | `/Key/GetTimelineByOperationId` | [Authorize] | Returns timeline entries for a specific operation ID. |
| POST | `/Key/RollBack` | [ProtectedEndPoint] | Rolls back a key to a previous timeline state. |

#### Translation Workflows

| Method | Path | Auth | Description |
|---|---|---|---|
| POST | `/Key/TranslateAll` | [ProtectedEndPoint] | Queues translation for all untranslated keys (optionally scoped). |
| POST | `/Key/TranslateKey` | [Authorize] | Queues translation for a single blocks language key. |

#### UILM File Operations

| Method | Path | Auth | Description |
|---|---|---|---|
| GET | `/Key/GetUilmFile` | Public | Returns UILM JSON file content for requested module/language. |
| POST | `/Key/GenerateUilmFile` | [ProtectedEndPoint] | Queues UILM file generation job. |
| POST | `/Key/UilmImport` | [ProtectedEndPoint] | Queues UILM import job. |
| POST | `/Key/UilmExport` | [ProtectedEndPoint] | Queues UILM export job. |
| GET | `/Key/GetUilmExportedFiles` | [ProtectedEndPoint] | Returns paginated exported UILM file records. |
| GET | `/Key/GetLanguageFileGenerationHistory` | [ProtectedEndPoint] | Returns paginated language file generation history. |

#### Administrative / Internal

| Method | Path | Auth | Description |
|---|---|---|---|
| POST | `/Key/DeleteCollections` | [ProtectedEndPoint] | Deletes data from selected collections (hidden from API explorer via `ApiExplorerSettings`). |

### LanguageController — /Language/

#### Language Management

| Method | Path | Auth | Description |
|---|---|---|---|
| POST | `/Language/Save` | [ProtectedEndPoint] | Creates or updates a language entry. |
| GET | `/Language/Gets` | Public | Retrieves available languages. |
| DELETE | `/Language/Delete` | [ProtectedEndPoint] | Deletes a language by language name. |
| POST | `/Language/SetDefault` | [ProtectedEndPoint] | Sets the default language for a project context. |

### ModuleController — /Module/

#### Module Management

| Method | Path | Auth | Description |
|---|---|---|---|
| POST | `/Module/Save` | [ProtectedEndPoint] | Creates or updates a localization module. |
| GET | `/Module/Gets` | Public | Retrieves all localization modules. |

## Tech Stack

| Layer | Technology |
|---|---|
| Runtime | .NET 9 (`net9.0`) |
| Language | C# |
| Containerization | Docker (multi-stage builds for API and Worker) |
| Database(s) | MongoDB-compatible document database (BSON-annotated entities/repositories) |
| Cache | Redis-compatible cache (via `CacheConnectionString`) |
| Message Broker | Azure Service Bus or RabbitMQ (`amqp/amqps` auto-detected) |
| Observability | Blocks Genesis-integrated logging, metrics, and tracing backends |
| Secret Management | Azure Key Vault via `Blocks.Genesis` (or local env vars) |
| Auth Standard | ASP.NET Core authorization (`[Authorize]`) plus Blocks protected endpoints |
| API Docs | Swagger / OpenAPI (`/swagger`) |

## Prerequisites

| Tool | Minimum Version | Notes |
|---|---|---|
| .NET SDK | 9.0 | Required to restore, build, run API/Worker, and run tests |
| Docker Engine | 24+ | Required for containerized local runs |
| Git | 2.40+ | Required to clone and contribute |
| MongoDB-compatible DB | 6+ | Backing store referenced by `DatabaseConnectionString` |
| Redis-compatible cache | 6+ | Backing cache referenced by `CacheConnectionString` |
| Message Broker | Azure Service Bus or RabbitMQ 3.12+ | Queue transport for worker consumers |
| Azure Key Vault (optional) | N/A | Needed for staging/production secret resolution |

## Installation

```bash
git clone https://github.com/SELISEdigitalplatforms/l2-net-blocks-localization.git
cd l2-net-blocks-localization

dotnet restore src/blocks-localization-net.sln
dotnet build src/blocks-localization-net.sln -c Release
```

## Configuration — Environment Variables

This project supports **two configuration approaches**:

1. **Option A: Local environment variables** (recommended for local development)
2. **Option B: Azure Key Vault** (recommended for staging/production)

Use **only one approach at a time**.

### Option A — Local env vars

Use ASP.NET Core nested configuration with the `BlocksSecret__` prefix for shared Blocks secrets:

```bash
# Cache
BlocksSecret__CacheConnectionString="redis://localhost:6379" # Redis/cache endpoint

# Message Broker
BlocksSecret__MessageConnectionString="amqp://guest:guest@localhost:5672" # RabbitMQ or Service Bus connection

# Observability
BlocksSecret__LogConnectionString="<log-backend-connection>" # Centralized log backend connection
BlocksSecret__MetricConnectionString="<metric-backend-connection>" # Metrics backend connection
BlocksSecret__TraceConnectionString="<trace-backend-connection>" # Tracing backend connection
BlocksSecret__LogDatabaseName="blocks_logs" # Log database/index name
BlocksSecret__MetricDatabaseName="blocks_metrics" # Metric database/index name
BlocksSecret__TraceDatabaseName="blocks_traces" # Trace database/index name

# Database
BlocksSecret__DatabaseConnectionString="mongodb://localhost:27017" # Primary application database connection
BlocksSecret__RootDatabaseName="blocks_root" # Root/default database name

# Security
BlocksSecret__EnableHsts="false" # Enable HSTS (true in production)

# Project-specific runtime settings
AiCompletionUrl="https://api.openai.com/v1/chat/completions" # AI completion endpoint
ChatGptTemperature="0.1" # Default AI sampling temperature (0-1)
RootTenantId="<tenant-id>" # Root tenant used for notification/auth context
NotificationServiceUrl="https://dev-api.seliseblocks.com/communication/v1/Notifier/SendSecretNotification" # Notification service endpoint
BlocksAppNotificationReceiver="language-import-export" # Notification receiver configuration key

# Cryptography / AI secret material
Salt__0="0x01" # Salt byte 0 for key derivation
Salt__1="0x02" # Salt byte 1
Salt__2="0x03" # Salt byte 2
Salt__3="0x04" # Salt byte 3
Salt__4="0x05" # Salt byte 4
Salt__5="0x06" # Salt byte 5
Salt__6="0x07" # Salt byte 6
Salt__7="0x08" # Salt byte 7
ChatGptEncryptedSecret="<encrypted-api-key>" # Encrypted AI provider secret
ChatGptEncryptionKey="<encryption-key>" # Encryption key used to decrypt ChatGptEncryptedSecret
```

### Option B — Azure Key Vault (Production / Staging)

When deploying to an environment configured to read from Azure Key Vault, add the following secrets using flat names (no `BlocksSecret__` prefix):

```text
CacheConnectionString
MessageConnectionString
LogConnectionString
MetricConnectionString
TraceConnectionString
LogDatabaseName
MetricDatabaseName
TraceDatabaseName
DatabaseConnectionString
RootDatabaseName
EnableHsts
ChatGptEncryptedSecret
ChatGptEncryptionKey
```

The application uses `Blocks.Genesis` which automatically resolves these secrets when `VaultType.Azure` is specified in `Program.cs`. No code changes are needed to switch between options — only the presence or absence of the `BlocksSecret__` prefix matters.

### Variable Reference

| Variable | Purpose |
|---|---|
| CacheConnectionString | Redis (or compatible) cache — used for token and JWKS caching |
| MessageConnectionString | Message broker endpoint for publishing and consuming domain events |
| LogConnectionString | Connection to the centralized log storage backend |
| MetricConnectionString | Connection to the metrics collection backend |
| TraceConnectionString | Connection to the distributed tracing backend |
| LogDatabaseName | Database / index name within the log store |
| MetricDatabaseName | Database / index name within the metrics store |
| TraceDatabaseName | Database / index name within the trace store |
| DatabaseConnectionString | Primary application database (users, tokens, resources) |
| RootDatabaseName | Root / default database name on the primary data store |
| EnableHsts | Enables HTTP Strict Transport Security — true in production, false locally |
| AiCompletionUrl | AI completion endpoint used by translation suggestion workflows |
| ChatGptTemperature | Default model temperature for AI completion requests |
| RootTenantId | Tenant identifier used for notification signing/context |
| NotificationServiceUrl | External notification API URL for async workflow completion events |
| BlocksAppNotificationReceiver | Notification channel/configuration name for export notifications |
| Salt | Byte-array salt used for decrypting stored AI secrets |
| ChatGptEncryptedSecret | Encrypted AI API secret read by localization secret processor |
| ChatGptEncryptionKey | Key used to decrypt `ChatGptEncryptedSecret` |

## Running the Project Locally

### Step 1: Set environment variables

**bash (Linux/macOS)**

```bash
export BlocksSecret__CacheConnectionString="redis://localhost:6379"
export BlocksSecret__MessageConnectionString="amqp://guest:guest@localhost:5672"
export BlocksSecret__LogConnectionString="<log-connection>"
export BlocksSecret__MetricConnectionString="<metric-connection>"
export BlocksSecret__TraceConnectionString="<trace-connection>"
export BlocksSecret__LogDatabaseName="blocks_logs"
export BlocksSecret__MetricDatabaseName="blocks_metrics"
export BlocksSecret__TraceDatabaseName="blocks_traces"
export BlocksSecret__DatabaseConnectionString="mongodb://localhost:27017"
export BlocksSecret__RootDatabaseName="blocks_root"
export BlocksSecret__EnableHsts="false"
export AiCompletionUrl="https://api.openai.com/v1/chat/completions"
export ChatGptTemperature="0.1"
export RootTenantId="<tenant-id>"
export NotificationServiceUrl="https://dev-api.seliseblocks.com/communication/v1/Notifier/SendSecretNotification"
export BlocksAppNotificationReceiver="language-import-export"
export Salt__0="0x01"; export Salt__1="0x02"; export Salt__2="0x03"; export Salt__3="0x04"
export Salt__4="0x05"; export Salt__5="0x06"; export Salt__6="0x07"; export Salt__7="0x08"
export ChatGptEncryptedSecret="<encrypted-api-key>"
export ChatGptEncryptionKey="<encryption-key>"
```

**PowerShell (Windows)**

```powershell
$env:BlocksSecret__CacheConnectionString = "redis://localhost:6379"
$env:BlocksSecret__MessageConnectionString = "amqp://guest:guest@localhost:5672"
$env:BlocksSecret__LogConnectionString = "<log-connection>"
$env:BlocksSecret__MetricConnectionString = "<metric-connection>"
$env:BlocksSecret__TraceConnectionString = "<trace-connection>"
$env:BlocksSecret__LogDatabaseName = "blocks_logs"
$env:BlocksSecret__MetricDatabaseName = "blocks_metrics"
$env:BlocksSecret__TraceDatabaseName = "blocks_traces"
$env:BlocksSecret__DatabaseConnectionString = "mongodb://localhost:27017"
$env:BlocksSecret__RootDatabaseName = "blocks_root"
$env:BlocksSecret__EnableHsts = "false"
$env:AiCompletionUrl = "https://api.openai.com/v1/chat/completions"
$env:ChatGptTemperature = "0.1"
$env:RootTenantId = "<tenant-id>"
$env:NotificationServiceUrl = "https://dev-api.seliseblocks.com/communication/v1/Notifier/SendSecretNotification"
$env:BlocksAppNotificationReceiver = "language-import-export"
$env:Salt__0 = "0x01"; $env:Salt__1 = "0x02"; $env:Salt__2 = "0x03"; $env:Salt__3 = "0x04"
$env:Salt__4 = "0x05"; $env:Salt__5 = "0x06"; $env:Salt__6 = "0x07"; $env:Salt__7 = "0x08"
$env:ChatGptEncryptedSecret = "<encrypted-api-key>"
$env:ChatGptEncryptionKey = "<encryption-key>"
```

### Step 2: Run services in separate terminals

```bash
# Terminal 1 - API
cd src/Api
dotnet run --launch-profile http
```

```bash
# Terminal 2 - Worker
cd src/Worker
dotnet run --launch-profile Worker
```

Default local URLs/ports:
- API: `http://localhost:5170`
- Swagger UI: `http://localhost:5170/swagger`
- Health check: `http://localhost:5170/health` (when enabled by middleware)

### Option 2: Docker

```bash
# Build images
docker build -f Dockerfile -t blocks-localization-api --build-arg git_branch=Development .
docker build -f worker.Dockerfile -t blocks-localization-worker --build-arg git_branch=Development .

# Run API container
docker run --rm -p 8080:8080 \
	-e port=8080 \
	-e ASPNETCORE_ENVIRONMENT=Development \
	-e BlocksSecret__CacheConnectionString="redis://host.docker.internal:6379" \
	-e BlocksSecret__MessageConnectionString="amqp://guest:guest@host.docker.internal:5672" \
	-e BlocksSecret__LogConnectionString="<log-connection>" \
	-e BlocksSecret__MetricConnectionString="<metric-connection>" \
	-e BlocksSecret__TraceConnectionString="<trace-connection>" \
	-e BlocksSecret__LogDatabaseName="blocks_logs" \
	-e BlocksSecret__MetricDatabaseName="blocks_metrics" \
	-e BlocksSecret__TraceDatabaseName="blocks_traces" \
	-e BlocksSecret__DatabaseConnectionString="mongodb://host.docker.internal:27017" \
	-e BlocksSecret__RootDatabaseName="blocks_root" \
	-e BlocksSecret__EnableHsts="false" \
	-e AiCompletionUrl="https://api.openai.com/v1/chat/completions" \
	-e ChatGptTemperature="0.1" \
	-e RootTenantId="<tenant-id>" \
	-e NotificationServiceUrl="https://dev-api.seliseblocks.com/communication/v1/Notifier/SendSecretNotification" \
	-e BlocksAppNotificationReceiver="language-import-export" \
	-e Salt__0="0x01" -e Salt__1="0x02" -e Salt__2="0x03" -e Salt__3="0x04" \
	-e Salt__4="0x05" -e Salt__5="0x06" -e Salt__6="0x07" -e Salt__7="0x08" \
	-e ChatGptEncryptedSecret="<encrypted-api-key>" \
	-e ChatGptEncryptionKey="<encryption-key>" \
	blocks-localization-api

# Run Worker container
docker run --rm \
	-e ASPNETCORE_ENVIRONMENT=Development \
	-e BlocksSecret__CacheConnectionString="redis://host.docker.internal:6379" \
	-e BlocksSecret__MessageConnectionString="amqp://guest:guest@host.docker.internal:5672" \
	-e BlocksSecret__LogConnectionString="<log-connection>" \
	-e BlocksSecret__MetricConnectionString="<metric-connection>" \
	-e BlocksSecret__TraceConnectionString="<trace-connection>" \
	-e BlocksSecret__LogDatabaseName="blocks_logs" \
	-e BlocksSecret__MetricDatabaseName="blocks_metrics" \
	-e BlocksSecret__TraceDatabaseName="blocks_traces" \
	-e BlocksSecret__DatabaseConnectionString="mongodb://host.docker.internal:27017" \
	-e BlocksSecret__RootDatabaseName="blocks_root" \
	-e BlocksSecret__EnableHsts="false" \
	-e AiCompletionUrl="https://api.openai.com/v1/chat/completions" \
	-e ChatGptTemperature="0.1" \
	-e RootTenantId="<tenant-id>" \
	-e NotificationServiceUrl="https://dev-api.seliseblocks.com/communication/v1/Notifier/SendSecretNotification" \
	-e BlocksAppNotificationReceiver="language-import-export" \
	-e Salt__0="0x01" -e Salt__1="0x02" -e Salt__2="0x03" -e Salt__3="0x04" \
	-e Salt__4="0x05" -e Salt__5="0x06" -e Salt__6="0x07" -e Salt__7="0x08" \
	-e ChatGptEncryptedSecret="<encrypted-api-key>" \
	-e ChatGptEncryptionKey="<encryption-key>" \
	blocks-localization-worker
```

## Usage

| Surface | Local URL |
|---|---|
| API Base URL | `http://localhost:5170` |
| Swagger UI | `http://localhost:5170/swagger` |
| Health Check | `http://localhost:5170/health` |
| Discovery Endpoint | `N/A (service discovery is handled by platform/gateway deployment)` |

Refer to the Swagger UI (`/swagger`) for full request/response schemas, required fields, and live API testing.

## Contributing

Contributions are welcome. Please follow these steps:

1. Fork the repository.
2. Create a feature branch: `git checkout -b feature/my-feature`
3. Commit your changes using [Conventional Commits](https://www.conventionalcommits.org/).
4. Push your branch and open a Pull Request against `dev`.
5. Ensure all tests pass before submitting: `dotnet test src/blocks-localization-net.sln`

Please read [CONTRIBUTING.md](CONTRIBUTING.md) and [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) before submitting a PR.

## License

This project is licensed under the terms of the [MIT License](LICENSE).

## Maintainers

For questions or issues, open a [GitHub Issue](https://github.com/SELISEdigitalplatforms/l2-net-blocks-localization/issues). For security disclosures, follow [SECURITY.md](SECURITY.md).
