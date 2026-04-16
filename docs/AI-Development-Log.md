# AI-Assisted Logging System

## Description

Design and implement an AI-assisted logging system that captures, organizes, and analyzes application events in real time. The goal is to provide clear visibility into system behavior, errors, and performance while enabling intelligent insights from logs.

## Objectives

- Centralize application logs from different services.
- Structure logs in a consistent format.
- Use AI to detect anomalies, patterns, and potential issues.
- Improve debugging, monitoring, and operational awareness.

## Scope

- Implement structured logging for backend services.
- Store logs in a centralized logging system (e.g., ELK, Loki, or Cloud logging).
- Integrate AI models to:
  - Identify unusual activity or errors
  - Summarize log events
  - Provide recommendations for troubleshooting
- Build dashboards or alerts based on important log events.

## Key Features

- Structured log format (JSON)
- Log aggregation and storage
- AI-based log analysis
- Error detection and anomaly alerts
- Searchable log history
- Automated summaries of system activity

## Expected Outcome

A scalable logging infrastructure that not only records system activity but also uses AI to interpret logs, highlight issues, and assist developers in diagnosing and resolving problems faster.

## Deliverables

- Logging framework implementation
- AI log analysis integration
- Monitoring dashboards
- Documentation for log structure and usage

---

## AI Development Log

This document tracks the use of AI tools during the development of this project.
The goal is to document how AI assisted the development process, evaluate the quality of AI outputs, and reflect on lessons learned while using AI effectively.

Each entry records:

- The AI tool used
- The prompt or input provided
- The quality of the output
- Estimated time saved
- Lessons learned from the interaction

### Entry Template

| Date | Name | Tool Used | Purpose | Prompt / Input | Output Quality | Time Saved | Lessons Learned |
|------|------|-----------|---------|----------------|----------------|------------|-----------------|
| YYYY-MM-DD | Dreni / Endriti / Hera | Tool name | What task AI helped with | Prompt used | Good / Needed edits / Failed | Estimated time saved | What worked or didn't |

---

### Development Log

| Date | Name | Tool Used | Purpose | Prompt / Input | Output Quality | Time Saved | Lessons Learned |
|------|------|-----------|---------|----------------|----------------|------------|-----------------|
| 2026-03-24 | — | ChatGPT | Generate MVP feature descriptions | "Create an MVP scope for a restaurant operations platform with order tracking and kitchen status." | Needed minor edits | ~30 minutes | Clear prompts with context about the product idea produce better structured results. |
| 2026-03-25 | — | ChatGPT | Generate RICE scoring explanation | "Explain RICE scoring and calculate it for given features." | Good | ~20 minutes | Providing structured data helps AI produce more accurate calculations. |
| 2026-04-16 | Dreni | Claude Code | Create AI Development Log and CLAUDE.md instruction file | "Add a md file for the AI Development Log and create a CLAUDE.md so Claude logs each session automatically." | Good | ~25 minutes | Using CLAUDE.md to automate recurring documentation tasks saves consistent manual effort across sessions. |
| 2026-04-16 | Dreni | Claude Code | Update CLAUDE.md to log after each task completion | "Update CLAUDE.md so Claude appends the log right after each task instead of waiting for session end." | Good | ~5 minutes | Triggering log writes after task completion is more reliable than relying on session-end detection. |
| 2026-04-16 | Dreni | Claude Code | Remove empty leftover file ai-log.md.txt | "Why do I need this file ai-log.md.txt?" | Good | ~2 minutes | Keeping the docs folder clean avoids confusion about which file is the source of truth. |
| 2026-04-16 | Dreni | Claude Code | Migrate .cursorrules into frontend/CLAUDE.md | "Does Claude follow rules inside cursorrules under frontend directory?" | Good | ~10 minutes | Claude Code reads CLAUDE.md not .cursorrules — migrating rules ensures they are actually applied. |
| 2026-04-16 | Dreni | Claude Code | Strengthen CLAUDE.md to enforce mandatory logging | "We should be sure that you never miss!" | Good | ~5 minutes | Making rules explicit and non-negotiable in CLAUDE.md reduces the chance of Claude skipping the logging step. |
| 2026-04-16 | Dreni | Claude Code | Add Name column to Development Log for team tracking | "We are a group of 3, update the md file to show logs with a name tag." | Good | ~5 minutes | Adding a name column makes it easy to track individual contributions across a team. |

| 2026-04-16 | Dreni | Claude Code | Create 7 epic issues + 14 individual task issues for Milestone 2 Frontend MVP | "Create cards and issues for M2 milestone for 3 people to complete" | Good | ~60 minutes | Breaking deliverables into per-person assigned issues with checklists and branch names makes sprint planning much faster; including domain types and file paths directly in issue bodies reduces ambiguity for developers. |
| 2026-04-16 | Dreni | Claude Code | Review CLAUDE.md and correct all GitHub issues for domain language, schema paths, Next.js version, and M2.9/M2.10 mistakes | "We have claude.md file inside the directory so you can check there how we handle audit so change cards if you made any mistake" | Needed edits | ~20 minutes | Claude initially generated issues without reading the existing project conventions — always verify CLAUDE.md before writing detailed technical tickets. Key fixes: Next.js 14→15, role names, schema paths, .cursorrules already existed. |
---

## Reflection

AI significantly accelerated the process of generating structured documentation, feature descriptions, and prioritization frameworks. However, AI-generated outputs often required review and refinement to ensure they matched the project's context and requirements.

The most effective prompts included:

- Clear context about the project
- Structured inputs (lists, tables, or data)
- Explicit instructions about format (Markdown, tables, etc.)

Less effective prompts were vague or lacked project-specific context, which resulted in more generic outputs.

