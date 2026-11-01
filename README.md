# Gocgun.Results.Http

> **Beta.** This package is in beta: the public API may change between releases.
>
> It references `Gocgun.Results` through a sibling `ProjectReference`; the base package repository must sit next to this one to build. Publish order: `Gocgun.Results` first.

Maps `Gocgun.Results` to ASP.NET Core `TypedResults`. `Gocgun.Results` does not reference ASP.NET.

| `ErrorType` | Status |
|---|---|
| `Validation` | 400 |
| `Unauthorized` | 401 |
| `PaymentRequired` | 402 |
| `Forbidden` | 403 |
| `NotFound` | 404 |
| `Conflict`, `Concurrency` | 409 |
| `UnsupportedMediaType` | 415 |
| `BusinessRule` | 422 |
| `RateLimited` | 429 |
| Any other value | 500 |

Success with no payload is 204. Success with `Item` is 200. `ToCreatedHttpResult` is 201. Failures are `application/problem+json` and never HTTP 200. The problem body carries `code`.

`RateLimited` sets `Retry-After` when metadata contains `retryAfterSeconds`.

Tests are unit tests that execute `IResult` on `DefaultHttpContext`. No socket is opened.

License: AGPL-3.0-only (`LICENSE`). Copyright holder exception: `LICENSE-EXCEPTION`.

## Publishing to nuget.org

Run `scripts/publish.sh` (or `scripts/publish.ps1` on Windows). It dispatches the manual `release.yml` workflow of each dependency first, in dependency order (Gocgun.Results, Gocgun.Results.Http), then of this package, waiting for each run and stopping on the first failure. Requires the GitHub CLI signed in with `gh auth login`; the `nuget` environment and the nuget.org Trusted Publishing policy must exist in each repository.
