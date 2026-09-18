# Fleet Cards

Angular frontend and ASP.NET Core API for a lightweight fleet command deck.

## Development

Start the API:

```powershell
dotnet run --project backend\FleetCards.Api
```

Start the frontend in a second terminal:

```powershell
cd frontend
npm install
npm start
```

The frontend is available at `http://localhost:4200`; the API exposes fleet cards at
`GET http://localhost:5000/api/fleet-cards` (the exact local HTTPS port is printed by
`dotnet run`).
