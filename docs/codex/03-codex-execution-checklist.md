# Codex execution checklist (must follow)

Before editing:
- Identify existing patterns and reuse them.
- Confirm if CPM is enabled (Directory.Packages.props exists).
- Confirm target framework (net10.0).

While editing:
- Smallest diff only.
- Do not introduce new libraries without explicit instruction.
- No folder renames/moves (unless explicitly instructed).

After editing:
- Run all gates (restore/build/test).
- If a build fails, stop and fix before moving on.
