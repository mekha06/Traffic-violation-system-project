# Traffic Violation System

A .NET 8 Web API that helps traffic officers review vehicle violation cases. The system detects a vehicle's number plate from an uploaded image using a Python FastAPI OCR service, stores the case in SQL Server, and generates an AI draft summary for officer review.

## Architecture

```
Officer → .NET API → Python OCR Service → SQL Server → Gemini Summary
```

## Tech Stack

| Component | Technology |
|-----------|------------|
| Backend | C#, ASP.NET Core (.NET 8) |
| Database | SQL Server, EF Core |
| AI Service | Python, FastAPI, EasyOCR, OpenCV |
| LLM | Google Gemini API |
| Communication | REST APIs (HttpClient) |
| Testing | xUnit, pytest |
| Deployment | Docker |

## Project Structure

```
├── src/
│   ├── TrafficViolationSystem.API/     # .NET 8 Web API
│   │   ├── Controllers/                # API endpoints
│   │   ├── Data/                       # EF Core DbContext
│   │   ├── DTOs/                       # Data Transfer Objects
│   │   ├── Models/                     # Entity models
│   │   ├── Services/                   # OCR & Gemini services
│   │   ├── Program.cs                  # App entry point
│   │   └── appsettings.json            # Configuration
│   └── VisionService/                  # Python FastAPI OCR service
│       ├── main.py                     # FastAPI app
│       ├── test_main.py                # pytest tests
│       └── requirements.txt            # Python dependencies
├── tests/
│   └── TrafficViolationSystem.Tests/   # xUnit tests
├── docker-compose.yml                  # Orchestration
├── Dockerfile.api                      # .NET API container
├── Dockerfile.vision                   # Python service container
└── TrafficViolationSystem.sln          # Solution file
```

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Python 3.11+](https://www.python.org/downloads/)
- [SQL Server](https://www.microsoft.com/en-us/sql-server/sql-server-downloads) (LocalDB, Express, or full)
- [Docker](https://www.docker.com/) (optional, for containerized deployment)

### 1. Setup the Python Vision Service

```bash
cd src/VisionService
python -m venv venv
venv\Scripts\activate        # Windows
# source venv/bin/activate   # Linux/Mac

pip install -r requirements.txt
uvicorn main:app --reload --port 8000
```

The OCR service will be running at `http://localhost:8000`.

### 2. Setup the .NET API

```bash
# Update the connection string in appsettings.json if needed
# Then:
cd src/TrafficViolationSystem.API
dotnet restore
dotnet ef migrations add InitialCreate
dotnet ef database update
dotnet run
```

The API will be running at `http://localhost:5219` with Swagger UI at the root URL.

### 3. Configure Gemini API Key

Edit `src/TrafficViolationSystem.API/appsettings.json`:

```json
{
  "Gemini": {
    "ApiKey": "YOUR_ACTUAL_GEMINI_API_KEY",
    "Model": "gemini-2.0-flash"
  }
}
```

Get a key from [Google AI Studio](https://aistudio.google.com/apikey).

### 4. Docker Compose (Optional — for later)

```bash
docker-compose up --build
```

This starts all three services: SQL Server, .NET API, and Python Vision Service.

## API Endpoints

### Vehicles
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/vehicles` | List all vehicles |
| GET | `/api/vehicles/{id}` | Get vehicle by ID |
| GET | `/api/vehicles/plate/{plateNumber}` | Get vehicle by plate |
| POST | `/api/vehicles` | Create a vehicle |

### Junctions
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/junctions` | List all junctions |
| GET | `/api/junctions/{id}` | Get junction by ID |
| POST | `/api/junctions` | Create a junction |

### Violation Cases
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/violationcases` | List all cases (filter: `?status=Pending`) |
| GET | `/api/violationcases/{id}` | Get case by ID |
| POST | `/api/violationcases` | Create case (multipart: image + form data) |
| PUT | `/api/violationcases/{id}/review` | Review a case (approve/reject) |
| POST | `/api/violationcases/{id}/regenerate-summary` | Regenerate AI summary |
| GET | `/api/violationcases/{id}/image` | Get the violation image |

## Workflow

1. **Upload**: Officer uploads a violation image with details (junction, violation type).
2. **OCR**: The .NET API sends the image to the Python Vision Service for plate recognition.
3. **Store**: A vehicle record is created/found, and the violation case is saved to SQL Server.
4. **AI Summary**: The Gemini API generates a draft summary for the officer.
5. **Review**: The officer reviews the case, can correct the plate, and approves/rejects it.

## Running Tests

```bash
# .NET tests
dotnet test

# Python tests
cd src/VisionService
pytest test_main.py -v
```
