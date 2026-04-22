# Planner Split App (React + .NET API + PostgreSQL)

## What changed
- API now reads data from **PostgreSQL** instead of local JSON files.
- Added SQL setup script: `PlannerApi/Sql/planner_postgres_setup.sql`
- Added working **New Task** drawer in React dashboard.
- New task can be created by:
  - pasting email content
  - uploading `.txt`, `.eml`, or `.msg` mail file
- API persists created task into PostgreSQL and also adds a mailbox item.
- Recommended candidates are recalculated for the new task.

## API setup
1. Create PostgreSQL database and schema using:
   - `PlannerApi/Sql/planner_postgres_setup.sql`
2. Update `PlannerApi/appsettings.json`
3. Restore and run API:
   ```bash
   dotnet restore
   dotnet run
   ```
4. Swagger opens at:
   - `https://localhost:44302/swagger`
   - or the port shown in your local launch profile

## React setup
1. Open `planner-ui/public/config.json`
2. Set `apiBaseUrl` to your API base, for example:
   ```json
   { "apiBaseUrl": "https://localhost:44302/api" }
   ```
3. Run UI:
   ```bash
   npm install
   npm run dev
   ```

## Main API endpoints
- `GET /api/dashboard/summary`
- `GET /api/dashboard/tasks/top`
- `GET /api/tasks`
- `GET /api/tasks/{id}`
- `GET /api/tasks/{id}/recommended-candidates`
- `POST /api/tasks`
- `POST /api/tasks/upload-mail`
- `GET/POST /api/rules`
- `GET/POST /api/vendors`
- `GET/POST /api/candidates`
- `GET /api/mailbox`
