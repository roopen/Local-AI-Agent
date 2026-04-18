# EF Core Migrations

All migrations target `UserContext` in the `LocalAIAgent.API` project and must be run from the solution root (`F:\Local-AI-Agent`).

## Add a migration

```powershell
dotnet ef migrations add <MigrationName> `
  --project LocalAIAgent.API `
  --startup-project LocalAIAgent.API `
  --context UserContext `
  --output-dir Infrastructure/Migrations
```

## Apply migrations to the database

```powershell
dotnet ef database update `
  --project LocalAIAgent.API `
  --startup-project LocalAIAgent.API `
  --context UserContext
```

## Remove the last migration (if not yet applied)

```powershell
dotnet ef migrations remove `
  --project LocalAIAgent.API `
  --startup-project LocalAIAgent.API `
  --context UserContext
```

## Notes

- The `Id` property must be present on every entity — removing it causes EF model validation to fail with *"requires a primary key to be defined"*.
- The snapshot (`UserContextModelSnapshot.cs`) is auto-generated and should not be edited manually.
- Migration files live in `LocalAIAgent.API/Infrastructure/Migrations/`.
