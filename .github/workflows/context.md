# `.github/workflows` Context

CI/CD pipelines defined as GitHub Actions live in this folder. Review individual workflow YAML files here when adjusting build/test automation.

- [`ci.yml`](ci.yml) builds and tests the solution on pushes and pull requests targeting `main`. When a GitHub release is published it reuses the same workflow to pack the `dotnet-otelmcp` tool and push the resulting package to NuGet using the `NUGET_API_KEY` secret.
