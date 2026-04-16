# Claude Instructions for dineOS

## AI Development Log Requirement

At the start of **every conversation** about this project, append a new entry to `docs/AI-Development-Log.md` under the **Development Log** table.

### What to log

For each session, add a row with:

| Field | What to fill in |
|-------|-----------------|
| **Date** | Today's date in `YYYY-MM-DD` format |
| **Tool Used** | `Claude Code` |
| **Purpose** | Brief description of what the user asked for in this session |
| **Prompt / Input** | Short summary of the user's first prompt or main request |
| **Output Quality** | To be filled after the session (default: `Good`) |
| **Time Saved** | Estimated time saved (make a reasonable estimate) |
| **Lessons Learned** | Key insight or note from this interaction |

### When to append

- Append the entry **immediately after each task is completed**, once you have enough context to fill in all fields accurately.
- If multiple distinct tasks are completed in one session, add one row per major task.

### File location

`docs/AI-Development-Log.md` — append inside the Development Log table, after the last existing row.
