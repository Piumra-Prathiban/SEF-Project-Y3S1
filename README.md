## Local Database Setup

1. Make sure PostgreSQL is installed and running.
2. Create a database named `sef_project_db`.
3. Copy:

   `appsettings.Development.example.json`

   to:

   `appsettings.Development.json`

4. Update the PostgreSQL password in
   `appsettings.Development.json`.

5. Run:

   `dotnet ef database update`

6. Start the API:

   `dotnet run`

## Member 1 Product & Inventory Documentation

- Backend component documentation:
  `backend/MEMBER1_PRODUCT_INVENTORY_DOCUMENTATION.md`
- Frontend integration contract:
  `backend/MEMBER1_FRONTEND_CONTRACT.md`

The existing backend CI workflow is:

- `.github/workflows/backend-ci.yml`

It restores dependencies, builds the ASP.NET Core solution and runs the backend
test suite.
