# Code Review

Lightweight single-pass code review. Fast and cheap — use `/ultracodereview` for the full 9-agent sweep.

## Scope

Determine what to review using this priority:

1. **User specifies scope** — branch name, commit SHA, PR number/URL, or file paths
2. **On a feature branch** — all changes vs remote master (`git fetch origin --quiet && git diff origin/master...HEAD`)
3. **On master with staged changes** — staged files (`git diff --staged`)
4. **On master, nothing staged** — latest commit (`git show HEAD`)

## Instructions

1. **Get the diff.** Run the appropriate git command above. If it's large (>200 lines), skim for the highest-risk areas rather than reading every line.

2. **Read project conventions.** Check `CLAUDE.md` if present. Read only the files that changed — don't explore the whole codebase.

3. **Run tests and build.**
   ```bash
   cd "c:/Users/enchantinggg4/Documents/d2c-launcher" && dotnet build d2c-launcher.csproj 2>&1 | tail -10
   dotnet test d2c-launcher.Tests 2>&1 | tail -10
   ```

4. **Review for the things that matter most:**
   - Bugs and correctness issues (wrong logic, unhandled edge cases, off-by-one)
   - Security issues (injection, missing validation, leaked secrets)
   - Pattern violations (localization, naming, MVVM conventions per CLAUDE.md)
   - Obvious missing tests for non-trivial logic

   Skip: formatting, whitespace, naming nitpicks, style that matches existing code, things that are already caught by the build.

5. **Fix immediately** any issue that doesn't require a human decision: wrong constant, missing clamp, incorrect doc comment, pattern violation, localization violation. Rebuild after fixing.

6. **Report findings** in this format:

```
## Code Review

### Fixed
- [brief description of auto-fixed issues, or "nothing to fix"]

### Needs Attention
- file:line — description (only issues requiring human decision)

### Build & Tests
[pass/fail counts]

### Verdict: Ready to Merge | Needs Attention | Needs Work
```

Keep the output short. If everything is clean, say so in two lines.
