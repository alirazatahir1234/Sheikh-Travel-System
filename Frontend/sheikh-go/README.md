# SheikhGo (Flutter)

Customer mobile app with staff-style sign-in (same API as Sheikh Travel Control Center).

## Run

Start **SQL Server** (required for login) and the .NET API on port **5082**:

```bash
# Terminal 1 — local SQL Server (Docker Desktop must be running)
docker compose -f docker-compose.db.yml up -d

# Terminal 2 — API
cd Backend/SheikhTravelSystem.API
dotnet run --launch-profile http
```

The API uses the `ConnectionStrings:DefaultConnection` user secret (`localhost,1433` / `sa` / `Alisheikh@123`). First startup may take a minute while the database is seeded.

Then run the app:

```bash
cd Frontend/sheikh-go
flutter pub get
flutter run
```

### API URL (local dev)

The app picks a default API base URL by platform:

| Target | Default API base URL |
|--------|----------------------|
| Android emulator | `http://10.0.2.2:5082/api` |
| iOS simulator / macOS / desktop | `http://127.0.0.1:5082/api` |

Override with `--dart-define` (on Android emulator, `127.0.0.1` / `localhost` are rewritten to `10.0.2.2` automatically):

```bash
flutter run --dart-define=API_BASE_URL=http://10.0.2.2:5082/api
```

**Physical Android device:** use your Mac's LAN IP instead of `10.0.2.2`, e.g. `http://192.168.1.42:5082/api`.

## Demo credentials (seed data)

| Tab | Identifier | Password |
|-----|------------|----------|
| Email | `admin@sheikhtravel.com` | `Pass@123` |
| Phone | `03001234567` | `Pass@123` |

Auth endpoint: `POST /api/auth/login` (email or phone + password).
