# Agent Guidelines

## Environment Preparation (must be done first)
1. Install the .NET SDK **8.0.x** before performing any other tasks. On Debian/Ubuntu-based containers you can run:
   ```bash
   wget https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh
   bash /tmp/dotnet-install.sh --channel 8.0 --install-dir "$HOME/.dotnet"
   export PATH="$HOME/.dotnet:$PATH"
   dotnet --info
   ```
   Adjust the installation steps if the environment requires a different package manager, but ensure that the 8.0 SDK is available on the `PATH`.

## Knowledge Base (`context.md`)
* Every directory in this repository contains a `context.md` that summarises the purpose of the files underneath it. **Start by reading the most relevant `context.md` files before you inspect or change code.**
* Whenever you modify code, tests, assets, or configuration within a directory, update that directory's `context.md` (and any parent summaries if the high-level description needs to change). Keep the links between related contexts accurate.

## Build and Test Expectations
* Always run `dotnet build` and the relevant test suites (e.g., `dotnet test`) before handing work off to the user, unless explicitly instructed otherwise. Capture the command output so it can be referenced in the final report.

## Communication Preferences
* When adding code, tests, or examples, include concise comments that clarify the intent where helpful, but avoid excessive commenting.
