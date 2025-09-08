# Delete Collections API

## Overview
This API endpoint allows you to delete all data from specific collections in the localization system. It provides a controlled way to clean up data from any combination of the four main collections.

## Endpoint
- **URL**: `POST /Key/DeleteCollections`
- **Authorization**: Required (JWT Token)
- **Content-Type**: `application/json`

## Request Body
```json
{
  "collections": ["BlocksLanguageKeys", "BlocksLanguages", "BlocksLanguageModules", "UilmFiles"],
  "projectKey": "your-project-key"
}
```

### Request Parameters
- `collections` (array, required): List of collection names to delete data from
- `projectKey` (string, optional): Project identifier for context

### Valid Collection Names
- `BlocksLanguageKeys`: Contains language keys/translations
- `BlocksLanguages`: Contains language configurations  
- `BlocksLanguageModules`: Contains module definitions
- `UilmFiles`: Contains UILM export files

## Response Format
### Success Response
```json
{
  "isSuccess": true,
  "errors": null
}
```

### Error Response
```json
{
  "isSuccess": false,
  "errors": {
    "Collections": "Invalid collections specified: InvalidCollection. Valid collections are: BlocksLanguageKeys, BlocksLanguages, BlocksLanguageModules, UilmFiles"
  }
}
```

## Example Usage

### Delete data from specific collections
```http
POST /Key/DeleteCollections
Authorization: Bearer your-jwt-token
Content-Type: application/json

{
  "collections": ["BlocksLanguageKeys", "UilmFiles"],
  "projectKey": "my-project"
}
```

### Delete data from all collections
```http
POST /Key/DeleteCollections
Authorization: Bearer your-jwt-token
Content-Type: application/json

{
  "collections": ["BlocksLanguageKeys", "BlocksLanguages", "BlocksLanguageModules", "UilmFiles"],
  "projectKey": "my-project"
}
```

## Validation Rules
1. At least one collection must be specified
2. Only valid collection names are accepted
3. Authorization is required
4. ProjectKey must be provided for context

## Error Scenarios
- **400 Bad Request**: Invalid collection names or missing collections array
- **401 Unauthorized**: Missing or invalid JWT token
- **500 Internal Server Error**: Database operation failure

## Notes
- This operation is irreversible - deleted data cannot be recovered
- Use with caution in production environments
- The operation returns the count of deleted records for each collection
- All specified collections are processed atomically

## Security Considerations
- Requires proper authorization
- Logs all delete operations for audit purposes
- Validates collection names to prevent unauthorized deletions
