# Mafia Platform Rumours Service

## Service Responsibilities

Provides an information marketplace. Players can spend currency to buy pieces of information (rumors) about other players, sourced from their actions, appearance, or location.

---

## Technology Stack
С# (ASP .NET Core), PostgreSQL as the database.

---

## Prerequisites
Before you begin, ensure you have the following installed on your machine:

- Git
- .NET 9.0 SDK
- Docker Desktop

---

## Getting Started

### 1. Clone the Repository
Clone the project to your local machine:

```bash
https://github.com/mirrerror/mafia-platform-rumours-service.git
cd mafia-platform-rumours-service
````

---

### 2. Configure Environment Variables

This project uses a `.env` file in the root directory to manage sensitive information like database connection strings.

1. Find the `.env.example` file in the root of the project, make a copy, and rename it to `.env`.
2. Open `.env` and configure it.

**Important Note on Host:**

* When running with Docker Compose → `Host=db`
* When running locally → `Host=localhost`

---

## How to Run the Service

### Option 1: Using Docker Compose (Recommended)

This method runs both the .NET service and PostgreSQL inside containers.

1. Ensure Docker Desktop is running.
2. Update `.env` so the connection string uses `Host=db`:

```env
DB_CONNECTION_STRING="Host=db;Database=mafia_rumours_service;..."
```

3. Build and run:

```bash
docker-compose up --build
```

The service will be available at [http://localhost:8080](http://localhost:8080).

* Stop: press **Ctrl + C**
* Stop & remove containers:

```bash
docker-compose down
```

---

### Option 2: Running Locally (Without Docker)

Useful for debugging in your IDE.

1. Ensure you have a local PostgreSQL instance running.
2. Update `.env` so the connection string uses `Host=localhost`:

```env
DB_CONNECTION_STRING="Host=localhost;Database=mafia_rumours_service;..."
```

3. Run the application:

* On **Windows**:

  ```bash
  start.bat
  ```
* On **Linux/macOS**:

  ```bash
  ./start.sh
  ```
* Or directly:

  ```bash
  dotnet run
  ```

---

### Option 3: Docker Hub Image

You can also pull the pre-built Docker image of the service from Docker Hub.

**Docker Hub Repository:** `m1rrerror/mafia-rumours-service`

#### Pull and Run

```bash
# Pull the image
docker pull m1rrerror/mafia-rumours-service:latest

# Run the container
docker run -d -p 8080:80 --name mafia-rumours-service m1rrerror/mafia-rumours-service:latest
```

The service will be available at [http://localhost:8080](http://localhost:8080).

#### Notes

* When running via Docker Hub image, you can override environment variables using `-e` flags:

```bash
docker run -d -p 8080:80 \
  -e DB_CONNECTION_STRING="Host=db;Database=mafia_rumours_service;Username=postgres;Password=postgres" \
  --name mafia-rumours-service \
  m1rrerror/mafia-rumours-service:latest
```

* Stop the container:

```bash
docker stop mafia-rumours-service
docker rm mafia-rumours-service
```

---

## Running the Tests

From the solution root, run:

```bash
dotnet test
```

---

## API Reference

All request and response bodies are in **JSON** format.

---

### Buy a rumour

**Endpoint:** `POST /api/rumours/{lobbyId}/purchase`

**Description:** Buys a rumour in the specified lobby.

**Request Body:**
```json
{
  "gameId": 1,
  "rumourType": "activity",
  "senderId": 0,
  "targetId": 1
}
```

**Available rumour types:** activity, appearance.

**Success Response (200):**
```json
{
  "data": {
    "id": 1,
    "lobbyId": "test",
    "type": "activity",
    "ownerId": 0,
    "targetId": 1,
    "text": "Player X was seen near the victim's house last night",
    "createdAt": "2025-10-01T12:00:00Z"
  }
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
      "code": "BAD_RUMOURS_TYPE",
      "message": "Rumours type not found"
    }
  }
  ```


---

### Get user's purchased rumours

**Endpoint:** `GET /api/rumours/{lobbyId}/user/{userId}`

**Description:** Gets all purchased rumours for the specified user in the specified lobby.

**Success Response (200):**
```json
{
  "data": {
    [
      {
        "id": 1,
        "lobbyId": "test",
        "type": "activity",
        "ownerId": 0,
        "targetId": 1,
        "text": "Player X was seen near the victim's house last night",
        "createdAt": "2025-10-01T12:00:00Z"
      },
      {
        "id": 2,
        "lobbyId": "test",
        "type": "activity",
        "ownerId": 0,
        "targetId": 2,
        "text": "Player Y has been acting suspiciously",
        "createdAt": "2025-10-01T12:00:00Z"
      }
    ]
  }
}
```


---

## General Errors

**503 Service Unavailable**

```json
{
  "error": {
    "code": "SERVICE_UNAVAILABLE",
    "message": "Gateway service is unavailable: message"
  }
}
```

**503 Service Unavailable**

```json
{
  "error": {
    "code": "CONCURRENCY_LIMIT_REACHED",
    "message": "The service is temporarily overloaded. Please try again later."
  }
}
```

**408 Request Timeout**

```json
{
  "error": {
    "code": "REQUEST_TIMEOUT",
    "message": "The request took too long to process."
  }
}
```