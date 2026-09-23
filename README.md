Backend
   cd backend/SEF_Project.Api
   dotnet restore
   dotnet build

3. Frontend (React)
   cd <react-folder>
   npm install

4. Flutter
   cd <flutter-folder>
   flutter pub get

5. PostgreSQL
   - Install PostgreSQL
   - Create your local database
   - Configure your local connection string
   - Do NOT commit passwords/API keys/secrets

6. EF Core
   - Check that the connection string is correct
   - Apply the existing migrations to your local database
   - Seed data if the project has seed data


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
