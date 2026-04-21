Apply suggested improvements from a skill retrospective. Additional context from the user (if any): $ARGUMENTS

## Step 1 - Find the retrospective

Determine where the feedback is coming from, in this order:

1. **Current PR** - if on a feature branch, find the open PR and fetch its comments:
   ```bash
   gh pr view --json number,comments --repo dota2classic/launcher
   gh pr comments <number> --repo dota2classic/launcher
   ```
   Look for a comment containing `## Skill retrospective`.

2. **Recent conversation** - if no PR or no retrospective comment found, look at the current conversation for any inline retrospective the user has described or pasted.

3. **User-provided** - if `$ARGUMENTS` contains the retrospective content directly, use that.

If no retrospective is found anywhere, ask the user to provide it.

## Step 2 - Identify the target

Read the retrospective's **Suggested improvement** block and determine what needs to change:

- If it references a specific skill (e.g. "add step to fix-issue") -> the target is `.agents/commands/<skill>.md`
- If it's a general process rule (e.g. "always check auth timing") -> the target is `AGENTS.md`
- If it's both -> update both

Read the target file(s) before editing.

## Step 3 - Apply the improvement

Make the minimal, precise edit that implements the suggestion. Follow the existing style and structure of the file being modified.

- For command files: add the new step or check in the most logical position (e.g. investigation checklist fits between "Investigate" and "Plan")
- For `AGENTS.md`: add under the most relevant section, or create a new section if it's a new category
- Do not rewrite surrounding content - surgical edits only

Incorporate `$ARGUMENTS` if the user provided additional constraints or refinements to how the improvement should be applied.

## Step 4 - Confirm and summarize

Show the user:
- Which file(s) were changed
- A brief diff-like summary of what was added/changed
- Why it addresses the retrospective finding
