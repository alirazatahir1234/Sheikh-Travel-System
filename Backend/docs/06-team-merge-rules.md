# Team Merge Rules

## Merge only when:

* PR has been reviewed and approved
* all critical comments are resolved
* tests pass
* the branch is up to date with `main`
* no merge conflicts remain

## Do not merge when:

* there are unresolved must-fix comments
* tests are failing
* code quality is poor
* the PR includes unrelated changes

---

## Pre-merge verification commands

### Backend (.NET)

```bash
# Pull latest main and rebase
git fetch origin
git rebase origin/main

# Build and test
dotnet build --no-restore
dotnet test --no-build --verbosity normal
```

### Frontend (Angular)

```bash
# Pull latest main and rebase
git fetch origin
git rebase origin/main

# Build, test, and lint
ng build --configuration production
ng test --watch=false --browsers=ChromeHeadless
ng lint
```

---

## Merge checklist

Before clicking merge, confirm:

| Check | Backend (.NET) | Frontend (Angular) |
|-------|---------------|-------------------|
| Build passes | `dotnet build` | `ng build` |
| Tests pass | `dotnet test` | `ng test` |
| Lint passes | — | `ng lint` |
| No `any` types | — | No `any` in new code |
| No `console.log` | No `Console.WriteLine` | No `console.log` |
| No hardcoded secrets | `appsettings` + user secrets | `environment.ts` only |
| SQL parameterized | All Dapper queries | — |
| Auth enforced | `[Authorize]` on endpoints | Route guards in place |
| DTOs used | No entity exposure | Typed interfaces used |
| Validators present | FluentValidation rules | Reactive form validators |
