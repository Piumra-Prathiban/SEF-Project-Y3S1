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
