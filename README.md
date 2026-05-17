# 🕌 Tarteel Clone

A full-stack Quranic recitation assistant inspired by [Tarteel.ai](https://tarteel.ai).
Listens to your recitation in real-time, identifies the verse, and highlights any errors.

---

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────┐
│                     CLIENT LAYER                        │
│  ┌────────────────────────────────────────────────────┐ │
│  │   .NET MAUI Mobile App (iOS / Android / Windows)   │ │
│  │   - Mic input  - Verse highlight  - Progress UI    │ │
│  └───────────────────────┬────────────────────────────┘ │
└──────────────────────────┼──────────────────────────────┘
                           │ HTTPS / WebSocket (SignalR)
┌──────────────────────────▼──────────────────────────────┐
│               ASP.NET Core API Gateway                  │
│         JWT Auth · Rate Limiting · SignalR Hub           │
└────┬──────────────────┬──────────────────────┬──────────┘
     │                  │                      │
┌────▼───┐        ┌─────▼──────┐        ┌──────▼──────┐
│  ASR   │        │   Quran    │        │    User     │
│Service │        │  Engine    │        │   Service   │
│(Python │        │            │        │             │
│Whisper)│        │ - Matching │        │ - Auth/JWT  │
│        │        │ - Errors   │        │ - Progress  │
└────┬───┘        │ - Tafsir   │        │ - Streaks   │
     │            └──────┬─────┘        └──────┬──────┘
     │                   │                     │
     └─────────┬─────────┘                     │
               │                               │
┌──────────────▼───────────────────────────────▼──────────┐
│                       DATA LAYER                        │
│  PostgreSQL (Quran)  ·  PostgreSQL (Users)              │
│  Redis (sessions / audio chunks)                        │
│  Elasticsearch (semantic verse search)                  │
└─────────────────────────────────────────────────────────┘
```

---

## 📁 Repository Structure

```
tarteel-clone/
├── TarteelClone.slnx          # .NET solution (SDK-style XML)
├── src/
│   ├── Api/                   # ASP.NET Core Web API gateway
│   │   ├── Controllers/       # REST endpoints (Auth, Quran, Progress, Search)
│   │   ├── Hubs/              # SignalR hub (real-time recitation stream)
│   │   └── Dockerfile
│   ├── QuranEngine/           # Verse matching & error detection (FuzzySharp)
│   ├── UserService/           # Auth (JWT + BCrypt), progress tracking
│   ├── SearchService/         # Elasticsearch client (NEST)
│   └── ASRService/            # Python Whisper microservice
│       ├── main.py
│       ├── requirements.txt
│       └── Dockerfile
├── mobile/
│   └── TarteelMobile/         # .NET MAUI app (iOS / Android / Windows)
│       ├── Views/             # XAML pages
│       ├── ViewModels/        # CommunityToolkit.Mvvm
│       ├── Services/          # API client, SignalR, Audio
│       └── Models/
├── data/
│   ├── quran/                 # Quran text datasets (see README.txt)
│   └── migrations/            # SQL schema + seed scripts
├── docker-compose.yml
└── README.md
```

---

## 🚀 Quick Start

### Prerequisites
- Docker & Docker Compose
- .NET 10 SDK (for local development)
- Python 3.12+ (for ASR service, if running locally)

### 1. Start all services with Docker Compose

```bash
docker compose up --build
```

This starts:
| Service | URL |
|---|---|
| ASP.NET Core API | http://localhost:7001 |
| Python ASR service | http://localhost:8000 |
| PostgreSQL (Quran) | localhost:5432 |
| PostgreSQL (Users) | localhost:5433 |
| Redis | localhost:6379 |
| Elasticsearch | http://localhost:9200 |

### 2. Run the API locally (without Docker)

```bash
# Start infrastructure only
docker compose up quran-db user-db redis elasticsearch -d

# Run the API
cd src/Api
dotnet run
```

### 3. Run the Python ASR service locally

```bash
cd src/ASRService
pip install -r requirements.txt
uvicorn main:app --reload --port 8000
```

### 4. Build & run the MAUI app

Open `TarteelClone.slnx` in Visual Studio 2022 (17.10+) or Rider, select
`TarteelMobile` as startup project, choose your target platform, and run.

---

## 🔑 API Endpoints

| Method | Path | Auth | Description |
|---|---|---|---|
| `POST` | `/api/auth/register` | — | Register + receive JWT |
| `POST` | `/api/auth/login` | — | Login + receive JWT |
| `GET` | `/api/quran/{surah}/{ayah}` | — | Get a verse |
| `GET` | `/api/quran/{surah}` | — | Get full surah |
| `POST` | `/api/quran/match` | ✅ | Match transcribed text |
| `GET` | `/api/progress` | ✅ | Get memorization progress |
| `POST` | `/api/progress/record` | ✅ | Record recitation result |
| `GET` | `/api/search?q=...` | — | Search Quran |
| `WS` | `/hubs/recitation` | ✅ | Real-time SignalR hub |

---

## 📊 Database Schema

```sql
verses (id, surah_num, ayah_num, arabic_text, uthmani_text)
translations (id, verse_id, language, text, translator)
tafsir (id, verse_id, source, content)

users (id, email, password_hash, created_at)
recitation_sessions (id, user_id, started_at, ended_at)
recitation_errors (id, session_id, verse_id, error_type, timestamp)
memorization_progress (id, user_id, surah_num, ayah_num, mastery_score)
```

---

## 🗺️ Roadmap

- [ ] Phase 1 — Scaffold ✅ (this PR)
- [ ] Phase 2 — Load full 6236-verse Quran dataset
- [ ] Phase 3 — Implement Elasticsearch indexing script
- [ ] Phase 4 — Polish MAUI UI (Arabic font rendering, word-level colour highlights)
- [ ] Phase 5 — Tajweed error classification (makhraj analysis)
- [ ] Phase 6 — Streak tracking & push notifications
- [ ] Phase 7 — Azure deployment (AKS + Azure OpenAI Whisper endpoint)

---

## ⚙️ Configuration

All secrets are in `src/Api/appsettings.json`. Override via environment variables
in production (never commit real secrets):

| Key | Default | Description |
|---|---|---|
| `Jwt__Key` | `CHANGE_ME_...` | HMAC-SHA256 signing key (≥32 chars) |
| `ConnectionStrings__QuranDb` | localhost postgres | Quran DB connection string |
| `ConnectionStrings__UserDb` | localhost postgres | User DB connection string |
| `ASRService__BaseUrl` | `http://localhost:8000` | Python Whisper service URL |
| `Elasticsearch__Uri` | `http://localhost:9200` | Elasticsearch URL |

---

## 📜 License

MIT — see [LICENSE](LICENSE).
