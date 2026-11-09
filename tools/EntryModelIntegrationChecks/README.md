Checks the client-specific Sage 100 entry-model service, permissions, VAT calculation, preview conflicts, archived file integrity and duplicate-export protection.

Run from the repository root against a local PostgreSQL configuration:

```powershell
dotnet run --project tools/EntryModelIntegrationChecks/EntryModelIntegrationChecks.csproj -- backend/BankStatementConverter.API/appsettings.Development.json
```

The configured database role must be able to create a schema. The checks create a uniquely named `alias_entry_model_test_<guid>` schema, seed only generated test fixtures, use memory-backed file storage and remove that schema in a `finally` block. Existing application tables are never changed. Connection details are read from the supplied configuration file and are not printed.

Pure classification and monetary checks also run with the existing accounting check runner:

```powershell
dotnet run --project tools/AccountingExportChecks/AccountingExportChecks.csproj
```
