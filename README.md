# Phisio API (Backend)

Standalone backend repository for the Phisio physiotherapy platform.

**Location:** `c:\Users\Mahboubeh\source\repos\phisio-api`  
**Frontend repo:** [phisio-web](https://github.com/majidfad/phisio-web)

## Stack

- .NET 8, EF Core, PostgreSQL, JWT auth

## Local development

```bash
dotnet restore Phisio.sln
dotnet run --project Phisio.Api
```

## Tests

All automated backend tests live in a single project: `Phisio.Tests`
(unit tests under folders such as `Api/`, `Application/`, `Infrastructure/`,
and integration tests under `Integration/`).

Integration tests use EF Core InMemory hosts; they do **not** require
PostgreSQL, Docker, Redis, or other external services.

Run the complete suite (same command used by GitHub Actions CI):

```bash
dotnet test Phisio.sln
```

Or against the test project directly:

```bash
dotnet test Phisio.Tests/Phisio.Tests.csproj
```

CI runs this suite on every push and pull request. The job fails if any
unit or integration test fails.

### Pre-push hook (local enforcement)

A repository-managed Git `pre-push` hook runs the same full suite before
any `git push`. If tests fail, the push is aborted.

**Install once per clone** (Windows / macOS / Linux):

```bash
git config core.hooksPath .githooks
```

Or:

```bash
sh scripts/install-git-hooks.sh
```

Git does not enable custom hook paths automatically on clone, so this
one-time local config is required. The hook script itself lives in
`.githooks/pre-push` and is shared via the repository.

**How it works:** on `git push`, Git runs `.githooks/pre-push`, which
executes `dotnet test Phisio.sln`. A non-zero test exit code blocks the push.

**Bypass (use only when absolutely necessary):**

```bash
git push --no-verify
```

CI still runs the suite on the remote even if the local hook is bypassed.

## Docker

```bash
docker build -t phisio-api:local .
```

## Production deploy

Unified stack at `/opt/phisio`. Images are built in CI and pulled from **GHCR** (SHA tags in `.env`).

Push `main` → Deploy bootstraps the server, migrates data if needed, pulls `ghcr.io/.../phisio-api:<sha>`, starts `postgres` + `api`. Web CI updates the `web` profile only.

See [deploy/GITHUB_SECRETS.md](deploy/GITHUB_SECRETS.md).


