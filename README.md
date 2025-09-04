# l2-net-blocks-localization

## Overview

`l2-net-blocks-localization` is a .NET 9.0 solution for managing localization keys, languages, and modules for SELISE Blocks applications. It provides APIs and background workers for translation management, key generation, import/export, and more.

## Solution Structure

- **Api**: ASP.NET Core Web API for localization management.
- **DomainService**: Business logic and data access for keys, languages, and modules.
- **Worker**: Background service for event-driven tasks (file generation, translation, import/export).
- **XUnitTest**: Unit tests for business logic and API endpoints.

## Main Features

- Manage localization keys, languages, and modules via RESTful APIs.
- Import/export localization data in JSON, CSV, and Excel formats.
- Automated translation suggestions using AI.
- Health checks and environment-based configuration.
- Event-driven architecture for background processing.

## API Endpoints

- `/Language/Save` - Add or update a language.
- `/Language/Gets` - List all languages.
- `/Key/Save` - Add or update a key.
- `/Key/Gets` - List all keys.
- `/Module/Save` - Add or update a module.
- `/Module/Gets` - List all modules.
- `/Assistant/GetTranslationSuggestion` - Get AI translation suggestions.


## Contributing

1. Fork the repository.
2. Create a feature branch.
3. Submit a pull request.

## License

Proprietary - SELISE Digital Platforms