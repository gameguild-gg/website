# GraphQL API Documentation

## Overview

The CMS now includes a GraphQL API powered by HotChocolate with Banana Cake Pop as the GraphQL IDE. GraphQL provides a flexible and efficient way to query and manipulate data.

## Endpoints

- **GraphQL Endpoint**: `http://localhost:5002/graphql`
- **Banana Cake Pop IDE**: Open the GraphQL endpoint in your browser for an interactive GraphQL playground

## GraphQL Schema

### Types

#### User (with EntityBase Support)
```graphql
type User {
  # BaseEntity Properties
  id: UUID!
  version: Int!
  createdAt: DateTime!
  updatedAt: DateTime!
  deletedAt: DateTime
  isDeleted: Boolean!
  
  # User-specific Properties
  name: String!
  email: String!
  isActive: Boolean!
}
```

#### Input Types

```graphql
input CreateUserInput {
  name: String!
  email: String!
  isActive: Boolean = true
}

input UpdateUserInput {
  id: UUID!
  name: String
  email: String
  isActive: Boolean
}
```

### Queries

#### Get All Users (Active Only)
```graphql
query GetUsers {
  users {
    id
    version
    name
    email
    isActive
    createdAt
    updatedAt
    deletedAt
    isDeleted
  }
}
```

#### Get User By ID
```graphql
query GetUserById($id: UUID!) {
  userById(id: $id) {
    id
    version
    name
    email
    isActive
    createdAt
    updatedAt
    deletedAt
    isDeleted
  }
}
```

#### Get Active Users Only
```graphql
query GetActiveUsers {
  activeUsers {
    id
    version
    name
    email
    isActive
    createdAt
    updatedAt
  }
}
```

#### Get Deleted Users
```graphql
query GetDeletedUsers {
  deletedUsers {
    id
    version
    name
    email
    isActive
    createdAt
    updatedAt
    deletedAt
    isDeleted
  }
}
```
```graphql
query GetActiveUsers {
  activeUsers {
    id
    username
    email
    firstName
    lastName
    isActive
    createdAt
    updatedAt
  }
}
```

### Mutations

#### Create User
```graphql
mutation CreateUser($input: CreateUserInput!) {
  createUser(input: $input) {
    id
    version
    name
    email
    isActive
    createdAt
    updatedAt
    deletedAt
    isDeleted
  }
}
```

**Variables:**
```json
{
  "input": {
    "name": "John Doe",
    "email": "john@example.com",
    "isActive": true
  }
}
```

#### Update User
```graphql
mutation UpdateUser($input: UpdateUserInput!) {
  updateUser(input: $input) {
    id
    version
    name
    email
    isActive
    createdAt
    updatedAt
    deletedAt
    isDeleted
  }
}
```

**Variables:**
```json
{
  "input": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "name": "Jane Doe Updated",
    "email": "jane.updated@example.com"
  }
}
```

#### Delete User
```graphql
mutation DeleteUser($id: UUID!) {
  deleteUser(id: $id)
}
```

**Variables:**
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000"
}
```

#### Soft Delete User
```graphql
mutation SoftDeleteUser($id: UUID!) {
  softDeleteUser(id: $id)
}
```

**Variables:**
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000"
}
```

#### Restore User
```graphql
mutation RestoreUser($id: UUID!) {
  restoreUser(id: $id)
}
```

**Variables:**
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000"
}
```

## Features

### Banana Cake Pop IDE
- **Interactive Query Builder**: Build queries visually
- **Schema Documentation**: Explore the complete API schema
- **Query Validation**: Real-time syntax checking
- **Variables Support**: Easy variable management
- **Response Formatting**: Beautiful JSON response formatting

### Benefits of GraphQL
1. **Flexible Queries**: Request exactly the data you need
2. **Single Endpoint**: One URL for all operations
3. **Strong Typing**: Schema-first development
4. **Real-time Validation**: Query validation before execution
5. **Introspection**: Self-documenting API

## Getting Started

1. **Start the Application**:
   ```bash
   cd /Users/tolstenko/projects/gameguild/services/cms
   dotnet run --urls="http://localhost:5002"
   ```

2. **Open Banana Cake Pop**: Navigate to `http://localhost:5002/graphql`

3. **Try Your First Query**:
   ```graphql
   query {
     users {
       id
       name
       email
       isActive
     }
   }
   ```

4. **Create Your First User**:
   ```graphql
   mutation {
     createUser(input: {
       name: "Test User"
       email: "test@example.com"
       isActive: true
     }) {
       id
       name
       email
       isActive
     }
   }
   ```
     }
   }
   ```

## Error Handling

GraphQL provides detailed error messages:

```json
{
  "errors": [
    {
      "message": "Email 'test@example.com' already exists.",
      "locations": [{"line": 2, "column": 3}],
      "path": ["createUser"]
    }
  ]
}
```

## Integration with REST API

Both REST and GraphQL APIs are available:
- **REST API**: `http://localhost:5002/api/users` 
- **GraphQL API**: `http://localhost:5002/graphql`
- **Swagger UI**: `http://localhost:5002/swagger`

Choose the API style that best fits your needs!
