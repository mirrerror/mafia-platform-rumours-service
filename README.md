# Mafia Platform Rumours Service

## Service Responsibilities

Provides an information marketplace. Players can spend currency to buy pieces of information (rumors) about other players, sourced from their actions, appearance, or location.

## Technology Stack
С# (ASP .NET Core), PostgreSQL as the database.

## API Endpoints

All request and response bodies are in **JSON** format.

### Buy a rumour

**Endpoint:** `POST /api/purchase-rumour`

**Description:** Buys a random rumour in the specified lobby.

**Headers:**
- `Authorization: Bearer <token>`

**Request Body:**
```json
{
  "lobbyId": "lobby_id",
  "rumourType": "player_role",
  "targetPlayerId": 12
}
```

**Success Response (200):**
```json
{
  "rumour": "Player X was seen near the victim's house last night"
}
```

**Error Responses:**

**400 Bad Request**
  ```json
  {
    "error": {
      "code": "INSUFFICIENT_FUNDS",
      "message": "Not enough currency to purchase rumour"
    }
  }
  ```

**404 Not Found**
  ```json
  {
    "error": {
      "code": "NO_RUMOURS_AVAILABLE",
      "message": "No rumours available for this target"
    }
  }
  ```

**404 Not Found**
```json
{
  "error": {
    "code": "LOBBY_NOT_FOUND",
    "message": "Lobby does not exist"
  }
}
```
