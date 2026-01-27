# L2 Net Blocks Localization

A comprehensive .NET 9.0 localization management system for SELISE Blocks applications. This solution provides a robust API and background worker services for managing translation keys, languages, modules, and facilitating multi-language support across applications.

## 📋 Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [Features](#features)
- [Technology Stack](#technology-stack)
- [Solution Structure](#solution-structure)
- [API Endpoints](#api-endpoints)
- [Background Workers](#background-workers)
- [UILM Format](#uilm-format)
- [Key Timeline](#key-timeline)
- [Getting Started](#getting-started)
- [Configuration](#configuration)
- [Development](#development)
- [Testing](#testing)
- [Deployment](#deployment)
- [Contributing](#contributing)
- [License](#license)

## 🎯 Overview

L2 Net Blocks Localization is a production-ready localization management platform that enables:

- **Centralized Translation Management**: Manage translation keys, languages, and modules through RESTful APIs
- **Multi-Format Import/Export**: Support for JSON, CSV, and Excel (XLSX) formats
- **AI-Powered Translation**: Integration with AI services for translation suggestions
- **Event-Driven Processing**: Asynchronous background processing for heavy operations
- **Version Control**: Complete audit trail with key timeline tracking
- **Rollback Capabilities**: Revert to previous key states
- **Multi-Tenant Support**: Project-based isolation and management

## 🏗️ Architecture

The solution follows a clean architecture pattern with clear separation of concerns:

```
┌─────────────────┐
│   API Layer     │  ← RESTful API Controllers
├─────────────────┤
│  Domain Service │  ← Business Logic & Services
├─────────────────┤
│   Repositories  │  ← Data Access Layer (MongoDB)
├─────────────────┤
│  Background     │  ← Event Consumers & Workers
│    Workers      │
└─────────────────┘
```

### Key Architectural Patterns

- **Repository Pattern**: Abstraction layer for data access
- **Service Layer**: Business logic encapsulation
- **Event-Driven Architecture**: Asynchronous processing via message queues
- **Dependency Injection**: Loose coupling and testability
- **Multi-Tenant Architecture**: Project-based data isolation

## ✨ Features

### Core Features

- ✅ **Key Management**: Create, read, update, delete, and query translation keys
- ✅ **Language Management**: Manage supported languages and set default language
- ✅ **Module Management**: Organize keys into logical modules
- ✅ **Bulk Operations**: Save multiple keys in a single operation
- ✅ **Advanced Filtering**: Query keys by module, language, translation status, and more
- ✅ **Pagination**: Efficient handling of large datasets

### Import/Export Features

- ✅ **UILM File Generation**: Generate localization files for frontend consumption
- ✅ **Multi-Format Support**: JSON, CSV, Excel (XLSX) import/export
- ✅ **Selective Export**: Export specific modules or all modules
- ✅ **Incremental Import**: Update existing keys without replacing entire datasets

### Translation Features

- ✅ **AI Translation Suggestions**: Get AI-powered translation recommendations
- ✅ **Bulk Translation**: Translate all missing translations automatically
- ✅ **Selective Translation**: Translate specific keys on demand
- ✅ **Translation Status Tracking**: Identify partially translated keys

### Advanced Features

- ✅ **Key Timeline**: Complete audit trail of all key changes
- ✅ **Rollback Functionality**: Revert keys to previous states
- ✅ **Environment Data Migration**: Migrate localization data between environments
- ✅ **Collection Management**: Administrative tools for data cleanup
- ✅ **Health Checks**: Built-in health monitoring endpoints

## 🛠️ Technology Stack

- **.NET 9.0**: Latest .NET framework
- **ASP.NET Core**: Web API framework
- **MongoDB**: NoSQL database for flexible schema
- **Blocks.Genesis**: SELISE Blocks framework integration
- **FluentValidation**: Input validation
- **ClosedXML**: Excel file generation
- **CsvHelper**: CSV file processing
- **Newtonsoft.Json**: JSON serialization
- **xUnit**: Unit testing framework

## 📁 Solution Structure

```
l2-net-blocks-localization/
├── src/
│   ├── Api/                    # Web API project
│   │   ├── Controllers/       # API controllers
│   │   ├── Program.cs         # Application entry point
│   │   └── ServiceRegistry.cs # Dependency injection
│   │
│   ├── DomainService/          # Business logic layer
│   │   ├── Repositories/      # Data access layer
│   │   ├── Services/          # Business services
│   │   ├── Shared/            # Shared models and utilities
│   │   └── Validation/        # FluentValidation validators
│   │
│   ├── Worker/                 # Background worker project
│   │   ├── Consumers/         # Event consumers
│   │   ├── Program.cs         # Worker entry point
│   │   └── ServiceRegistry.cs # Worker DI configuration
│   │
│   └── XUnitTest/             # Unit tests
│
├── config/                     # Configuration files
├── Dockerfile                  # Docker configuration
└── README.md                   # This file
```

### Project Components

#### **Api** (`src/Api/`)
ASP.NET Core Web API providing RESTful endpoints for localization management.

**Key Controllers:**
- `KeyController`: Translation key operations
- `LanguageController`: Language management
- `ModuleController`: Module management
- `AssistantController`: AI translation suggestions

#### **DomainService** (`src/DomainService/`)
Core business logic and data access layer.

**Key Services:**
- `KeyManagementService`: Key CRUD and business logic
- `LanguageManagementService`: Language operations
- `ModuleManagementService`: Module operations
- `AssistantService`: AI integration

**Repositories:**
- `KeyRepository`: Key data access
- `KeyTimelineRepository`: Timeline data access
- `LanguageRepository`: Language data access
- `ModuleRepository`: Module data access

#### **Worker** (`src/Worker/`)
Background service for asynchronous processing.

**Event Consumers:**
- `GenerateUilmFilesConsumer`: Generate UILM files
- `TranslateAllEventConsumer`: Bulk translation processing
- `TranslateBlocksLanguageKeyEventConsumer`: Single key translation
- `UilmImportEventConsumer`: File import processing
- `UilmExportEventConsumer`: File export processing
- `EnvironmentDataMigrationEventConsumer`: Data migration

## 🔌 API Endpoints

### Key Management

#### Save Key
```http
POST /Key/Save
Authorization: Bearer {token}
Content-Type: application/json

{
  "keyName": "welcome.message",
  "moduleId": "auth-module",
  "resources": [
    {
      "culture": "en",
      "value": "Welcome!"
    },
    {
      "culture": "fr",
      "value": "Bienvenue!"
    }
  ],
  "projectKey": "your-project-key"
}
```

#### Save Multiple Keys
```http
POST /Key/SaveKeys
Authorization: Bearer {token}
Content-Type: application/json

[
  {
    "keyName": "key1",
    "moduleId": "module1",
    "resources": [...]
  },
  {
    "keyName": "key2",
    "moduleId": "module1",
    "resources": [...]
  }
]
```

#### Get Keys (with filtering)
```http
POST /Key/Gets
Authorization: Bearer {token}
Content-Type: application/json

{
  "moduleId": "auth-module",
  "languageCode": "en",
  "isPartiallyTranslated": false,
  "pageSize": 20,
  "pageNumber": 1,
  "projectKey": "your-project-key"
}
```

#### Get Key by ID
```http
GET /Key/Get?ItemId={keyId}&ProjectKey={projectKey}
Authorization: Bearer {token}
```

#### Delete Key
```http
DELETE /Key/Delete?ItemId={keyId}&ProjectKey={projectKey}
Authorization: Bearer {token}
```

#### Get Key Timeline
```http
GET /Key/GetTimeline?PageSize=10&PageNumber=1&EntityId={keyId}&ProjectKey={projectKey}
Authorization: Bearer {token}
```

#### Rollback Key
```http
POST /Key/RollBack
Authorization: Bearer {token}
Content-Type: application/json

{
  "itemId": "key-item-id",
  "timelineItemId": "timeline-item-id",
  "projectKey": "your-project-key"
}
```

### Language Management

#### Save Language
```http
POST /Language/Save
Authorization: Bearer {token}
Content-Type: application/json

{
  "languageName": "English",
  "languageCode": "en",
  "isDefault": true,
  "projectKey": "your-project-key"
}
```

#### Get Languages
```http
GET /Language/Gets?ProjectKey={projectKey}
```

#### Delete Language
```http
DELETE /Language/Delete?LanguageName={languageName}&ProjectKey={projectKey}
Authorization: Bearer {token}
```

#### Set Default Language
```http
POST /Language/SetDefault
Authorization: Bearer {token}
Content-Type: application/json

{
  "languageName": "English",
  "projectKey": "your-project-key"
}
```

### Module Management

#### Save Module
```http
POST /Module/Save
Authorization: Bearer {token}
Content-Type: application/json

{
  "moduleName": "authentication",
  "moduleId": "auth-module",
  "projectKey": "your-project-key"
}
```

#### Get Modules
```http
GET /Module/Gets?ProjectKey={projectKey}
```

### UILM File Operations

#### Generate UILM Files
```http
POST /Key/GenerateUilmFile
Authorization: Bearer {token}
Content-Type: application/json

{
  "moduleId": "auth-module",
  "projectKey": "your-project-key"
}
```

#### Get UILM File
```http
GET /Key/GetUilmFile?Module={moduleName}&Language={languageCode}&ProjectKey={projectKey}
```

#### Export UILM Files
```http
POST /Key/UilmExport
Authorization: Bearer {token}
Content-Type: application/json

{
  "moduleIds": ["module1", "module2"],
  "outputFormat": "JSON",
  "projectKey": "your-project-key"
}
```

#### Import UILM File
```http
POST /Key/UilmImport
Authorization: Bearer {token}
Content-Type: application/json

{
  "fileId": "file-id-from-storage",
  "projectKey": "your-project-key"
}
```

#### Get Exported Files
```http
GET /Key/GetUilmExportedFiles?PageSize=10&PageNumber=1&ProjectKey={projectKey}
Authorization: Bearer {token}
```

### Translation Operations

#### Translate All Missing Keys
```http
POST /Key/TranslateAll
Authorization: Bearer {token}
Content-Type: application/json

{
  "moduleId": "auth-module",
  "projectKey": "your-project-key"
}
```

#### Translate Specific Key
```http
POST /Key/TranslateKey
Authorization: Bearer {token}
Content-Type: application/json

{
  "keyId": "key-item-id",
  "targetLanguageCode": "fr",
  "projectKey": "your-project-key"
}
```

### AI Assistant

#### Get Translation Suggestion
```http
POST /Assistant/GetTranslationSuggestion
Authorization: Bearer {token}
Content-Type: application/json

{
  "sourceText": "Welcome to our application",
  "sourceLanguage": "en",
  "targetLanguage": "fr",
  "projectKey": "your-project-key"
}
```

### Administrative Operations

#### Delete Collections (Admin Only)
```http
POST /Key/DeleteCollections
Authorization: Bearer {token}
Content-Type: application/json

{
  "collections": ["BlocksLanguageKeys", "BlocksLanguages"],
  "projectKey": "your-project-key"
}
```

> **Note**: This endpoint is hidden from API documentation and should be used with extreme caution.

## 🔄 Background Workers

The Worker project processes events asynchronously to handle resource-intensive operations:

### Event Types

1. **GenerateUilmFilesEvent**: Generates UILM files for frontend consumption
2. **TranslateAllEvent**: Processes bulk translation requests
3. **TranslateBlocksLanguageKeyEvent**: Processes single key translation
4. **UilmImportEvent**: Handles file import operations
5. **UilmExportEvent**: Handles file export operations
6. **EnvironmentDataMigrationEvent**: Migrates data between environments

### Event Flow

```
API Request → Event Published → Worker Consumer → Processing → Notification Published
```

## 📄 UILM Format

UILM (User Interface Localization Module) is a JSON-based format for storing localization data. The system generates UILM files that can be consumed by frontend applications.

### UILM File Structure

```json
{
  "welcome.message": "Welcome!",
  "auth.login": "Login",
  "auth.logout": "Logout",
  "common.buttons.save": "Save",
  "common.buttons.cancel": "Cancel"
}
```

### Nested Key Support

The system supports nested keys using dot notation:

```json
{
  "common": {
    "buttons": {
      "save": "Save",
      "cancel": "Cancel"
    }
  }
}
```

### Key Mode

A special "key" language mode generates files where values are the same as keys, useful for development and testing.

## 📊 Key Timeline

The Key Timeline feature provides a complete audit trail of all changes to translation keys.

### Features

- **Change Tracking**: Records all create, update, and delete operations
- **State Comparison**: Shows both current and previous states
- **User Attribution**: Tracks who made each change
- **Timestamp Tracking**: Records when changes occurred
- **Source Tracking**: Identifies the source of changes (API endpoint, import, etc.)

### Timeline Entry Structure

```json
{
  "itemId": "timeline-entry-id",
  "entityId": "key-item-id",
  "currentData": { /* Current key state */ },
  "previousData": { /* Previous key state */ },
  "logFrom": "KeyController.Save",
  "userId": "user-id",
  "createDate": "2024-01-15T10:30:00Z"
}
```

### Use Cases

- **Audit Compliance**: Track all changes for compliance requirements
- **Debugging**: Identify when and why translations changed
- **Rollback**: Revert to previous key states
- **Analytics**: Analyze translation change patterns

## 🚀 Getting Started

### Prerequisites

- .NET 9.0 SDK
- MongoDB instance
- Azure Key Vault (for secrets management)
- SELISE Blocks Genesis framework access

### Installation

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd l2-net-blocks-localization
   ```

2. **Restore NuGet packages**
   ```bash
   dotnet restore src/L2-Net-Blocks-Localization.sln
   ```

3. **Configure application settings**
   - Update `appsettings.json` files in Api and Worker projects
   - Configure MongoDB connection strings
   - Set up Azure Key Vault credentials

4. **Build the solution**
   ```bash
   dotnet build src/L2-Net-Blocks-Localization.sln
   ```

5. **Run the API**
   ```bash
   cd src/Api
   dotnet run
   ```

6. **Run the Worker** (in a separate terminal)
   ```bash
   cd src/Worker
   dotnet run
   ```

## ⚙️ Configuration

### Application Settings

The solution uses environment-specific configuration files:

- `appsettings.json`: Base configuration
- `appsettings.Development.json`: Development environment
- `appsettings.dev.json`: Dev environment
- `appsettings.stg.json`: Staging environment
- `appsettings.prod.json`: Production environment

### Key Configuration Areas

1. **MongoDB Connection**: Database connection strings
2. **Azure Key Vault**: Secret management configuration
3. **Message Queue**: Event bus configuration (via Blocks.Genesis)
4. **Storage**: File storage configuration
5. **AI Services**: Translation service API keys

### Environment Variables

- `ASPNETCORE_ENVIRONMENT`: Environment name (Development, Staging, Production)
- `port`: API port number (default: 80)

## 💻 Development

### Project Structure Guidelines

- **Controllers**: Handle HTTP requests and responses
- **Services**: Contain business logic
- **Repositories**: Handle data access
- **Validators**: Input validation using FluentValidation
- **Events**: Domain events for async processing

### Code Style

- Follow C# coding conventions
- Use async/await for all I/O operations
- Implement proper error handling
- Add XML documentation comments for public APIs
- Use dependency injection for all dependencies

### Adding New Features

1. Create models in `DomainService/Shared` or appropriate service folder
2. Add repository methods if needed
3. Implement service logic
4. Create controller endpoints
5. Add validators
6. Register services in `ServiceRegistry`
7. Write unit tests

## 🧪 Testing

### Running Tests

```bash
cd src/XUnitTest
dotnet test
```

### Test Coverage

- Unit tests for services
- Integration tests for repositories
- API endpoint tests

## 🐳 Deployment

### Docker

The solution includes a Dockerfile for containerized deployment:

```bash
docker build -t blocks-localization-api --build-arg git_branch=Production .
docker run -p 8080:80 blocks-localization-api
```

### Environment-Specific Builds

The Dockerfile supports environment-specific builds using the `git_branch` argument:

```bash
docker build -t blocks-localization-api --build-arg git_branch=stg .
```

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

### Contribution Guidelines

- Follow existing code patterns and conventions
- Add unit tests for new features
- Update documentation as needed
- Ensure all tests pass before submitting PR
- Use meaningful commit messages

