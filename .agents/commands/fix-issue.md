Fix GitHub issue #$ARGUMENTS in the d2c-launcher repository.

## Steps

1. **Fetch the issue** using `gh issue view $ARGUMENTS --comments --repo dota2classic/launcher` to get the title, description, and all comments.

2. **Read memory-bank context** - read `memory-bank/activeContext.md` and `memory-bank/systemPatterns.md` to understand current state and architecture patterns.

3. **Investigate** - read the relevant source files to understand what needs to change. Do not modify code you haven't read.

4. **Cross-cutting checks** - after reading the relevant files, explicitly verify:
   - **Auth timing** - if the feature makes authenticated API calls, check where they fire relative to `SetBearerToken`. Read `MainLauncherViewModel` constructor order to confirm the token is set before the calls are queued.
   - **Pattern consistency** - if touching a parsing or serialization path, check whether the codebase already has a preferred approach (e.g. generated DTO deserializer vs. manual `JsonDocument` extraction) and use it.
   - **Fix the class, not the instance** - once a bug pattern is identified (missing guard, unhandled exception, unchecked return value, etc.), search for other occurrences of the same pattern before declaring the fix complete.

5. **Ask clarifying questions** - if the implementation is not crystal clear (ambiguous requirements, multiple valid approaches, unclear scope, or any non-obvious decision), stop and ask the user before proceeding. Skip this step only for trivial, unambiguous fixes.

6. **Present a plan** - write a clear, numbered plan: which files change, how, and why. Then stop and wait for the user to confirm before making any code changes.

7. **Create a branch** - fetch latest master and branch from it:
   ```bash
   git fetch origin
   git checkout -b fix/issue-$ARGUMENTS origin/master
   ```

8. **Implement the fix** - once confirmed, make the minimal change necessary. Follow the project's C# + Avalonia MVVM patterns.
   - UI text in Russian - add new strings to `Resources/Locales/ru.json` and access via `I18n.T("section.key")` in C# or `{l:T 'section.key'}` in XAML. Do not use `Strings.cs` for new strings (it is legacy and will be removed).

9. **Build and test** - run `dotnet build` and fix any compiler errors. Then run `dotnet test` to ensure all tests pass. Do not proceed to commit if any tests fail - fix the failures first.

10. **Visual verification** - if the fix has a visual / UI target, run the component preview tool and use `Read` on the screenshot to confirm the result looks correct before proceeding:
   ```powershell
   powershell -ExecutionPolicy Bypass -File tools/preview.ps1 <ComponentName>
   ```

11. **Update memory-bank** - update `memory-bank/activeContext.md` and `memory-bank/progress.md` to reflect what was changed.

12. **Commit** - stage the changed files and commit with a message following the project's git conventions (`<type>: <short description>` subject, `Closes #$ARGUMENTS` in the body). Use a HEREDOC.

13. **Open a pull request** - push the branch and create a PR against `master`:
   ```bash
   git push -u origin fix/issue-$ARGUMENTS
   gh pr create --repo dota2classic/launcher --base master --title "..." --body "..."
   ```
   Include a brief summary and `Closes #$ARGUMENTS` in the PR body.

14. **Code review and auto-fix** - run `/code-review` on the changes, then:
   - **Fix immediately** any issues that do not require user input: null guards, thread-safety gaps, caching a repeated I/O call, wrong access modifier, pattern inconsistency, localization violations, etc. Build again after fixing to confirm clean.
   - **Post to PR** only the issues that require a human decision: architectural trade-offs, design alternatives, ambiguous requirements, UX choices. If there are no such issues, skip the PR comment entirely.
   ```bash
   gh pr comment <PR-number> --repo dota2classic/launcher --body "<issues requiring human decision only>"
   ```

15. **Summarize** - share the PR URL and a one-line description of what was changed.
