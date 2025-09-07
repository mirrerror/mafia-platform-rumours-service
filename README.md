# Mafia Platform Rumours Service

## Service Responsibilities

Provides an information marketplace. Players can spend currency to buy pieces of information (rumors) about other players, sourced from their actions, appearance, or location.

## Technology Stack
С# (ASP .NET Core), PostgreSQL as the database.

## API Endpoints

All request and response bodies are in **JSON** format.

## Rumors Service

### POST `/purchase`

A player buys a random rumor.
**Request Body:**

```json
{ "userId": "uuid", "lobbyId": "uuid" }
```

**Response (200 OK):**

```json
{ "rumorId": "uuid", "text": "string" }
```
