# Blocks Localization Service

## Overview

SELISE `<blocks />` Localization is a .NET-based service for centralized localization management in distributed systems. It manages translation keys, languages, and modules across multi-tenant applications, processes heavy localization jobs through background workers, and serves generated language files for frontend consumption.

## Table of Content

- [Overview](#overview)
- [Feature](#feature)
- [Technology Stack](#technology-stack)
- [Project Structure](#project-structure)
- [Controller / Endpoint](#controller--endpoint)
- [Prerequisites](#prerequisites)
- [Getting Started](#getting-started)
- [Installation](#installation)

## Feature

- Centralized key, language, and module management through HTTP endpoints
- Bulk key save and paginated, filtered key retrieval
- UILM import/export and generated language file delivery
- AI-assisted translation suggestions and queue-based translation workflows
- Key timeline history with rollback support
- Background worker consumers for asynchronous processing
- Automated unit test coverage for major solution components

## Technology Stack

- .NET 9
- ASP.NET Core Web API
- MongoDB
- FluentValidation
- SeliseBlocks.Genesis
- xUnit
- ClosedXML
- CsvHelper

## Project Structure

```text
.
├── src
│   ├── Api                # REST API layer (controllers, startup)
│   ├── DomainService      # Business logic, repositories, validators
│   ├── Worker             # Background worker for asynchronous processing
│   └── XUnitTest          # Unit tests
├── config                 # Package source configuration
├── Dockerfile             # API image build
└── worker.Dockerfile      # Worker image build
```

## Controller / Endpoint

### KeyController

Base route: `/Key`

- `POST /Key/Save` — Create or update a single localization key with its resources.
- `POST /Key/SaveKeys` — Create or update multiple localization keys in one request.
- `POST /Key/Gets` — Retrieve paginated and filtered key results.
- `GET /Key/Get` — Retrieve a single key by its item identifier.
- `DELETE /Key/Delete` — Delete a key by its item identifier.
- `GET /Key/GetTimeline` — Retrieve paginated timeline/audit records for a key.
- `POST /Key/RollBack` — Roll back a key to a previous timeline state.
- `GET /Key/GetUilmFile` — Return the generated UILM JSON for a given module, language, and project.
- `POST /Key/GenerateUilmFile` — Queue generation of UILM language files.
- `POST /Key/UilmImport` — Import UILM content and merge it into existing localization data.
- `POST /Key/UilmExport` — Queue UILM export for all or selected modules.
- `GET /Key/GetUilmExportedFiles` — Return paginated records of exported UILM files.
- `GET /Key/GetLanguageFileGenerationHistory` — Return paginated UILM generation history.
- `POST /Key/TranslateAll` — Queue translation for all missing values, optionally filtered by module.
- `POST /Key/TranslateKey` — Queue translation for a specific key.
- `POST /Key/DeleteCollections` — Delete selected database collections (admin/internal operation).

### LanguageController

Base route: `/Language`

- `POST /Language/Save` — Create or update a language entry.
- `GET /Language/Gets` — Retrieve all configured languages.
- `DELETE /Language/Delete` — Delete a language by name.
- `POST /Language/SetDefault` — Mark a language as the default language.

### ModuleController

Base route: `/Module`

- `POST /Module/Save` — Create or update a module.
- `GET /Module/Gets` — Retrieve all modules.

### AssistantController

Base route: `/Assistant`

- `POST /Assistant/GetTranslationSuggestion` — Return AI-based translation suggestions for the provided source text.

### ConfigController

Base route: `/Config`

- `POST /Config/SaveWebHook` — Create or update a webhook configuration for notifications and integrations.

Note: API endpoints marked with `ProtectedEndPoint` require the expected authentication and request context.

## Prerequisites

Before running the solution, make sure the following are available:

- .NET SDK 9.0 or later
- MongoDB
- Docker Desktop (optional, for containerized builds)
- Access to the required secret/configuration provider used by the application

## Getting Started

1. Clone the repository.
2. Move into the source directory.
3. Restore and build the solution.
4. Configure environment settings and secrets.
5. Run the required services.

Basic build commands:

```sh
cd src
dotnet restore blocks-localization-net.sln
dotnet build blocks-localization-net.sln
```

To run the services locally, use separate terminals from the `src` directory:

```sh
dotnet run --project Api/Api.csproj
dotnet run --project Worker/Worker.csproj
```

To run tests:

```sh
dotnet test XUnitTest/XUnitTest.csproj
```

## Installation

### 1. Clone the Repository

```sh
git clone <repository-url>
cd l2-net-blocks-localization
```

### 2. Restore Dependencies

```sh
cd src
dotnet restore blocks-localization-net.sln
```

### 3. Configure Environment Variables and Secrets

Set `ASPNETCORE_ENVIRONMENT` as needed, for example:

- `Development`
- `dev`
- `stg`
- `prod`

Provide the required configuration values through your secret store or environment-specific configuration.

### 4. Build the Solution

```sh
dotnet build blocks-localization-net.sln
```

### 5. Optional Docker Builds

From the repository root:

```sh
docker build -f Dockerfile -t blocks-localization-api --build-arg git_branch=Development .
docker build -f worker.Dockerfile -t blocks-localization-worker --build-arg git_branch=Development .
```
