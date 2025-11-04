# maidsflow-backend
asdasdasdas


## 2025-11-03: Checklist now has CompanyId
- Added CompanyId to Checklist model, DTOs, filters and controller details.
- Added EF mapping + migration `AddCompanyIdToChecklists` with backfill from Customers.
- After pulling, run: `dotnet ef database update --startup-project ControlApi --project Infrastructure`.
