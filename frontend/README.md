# dineOS Frontend

Next.js 16 App Router client for the dineOS restaurant management platform.

## Stack

- Next.js 16 / React 19, TypeScript strict mode
- Tailwind CSS 4
- TanStack Query v5, Zustand, React Hook Form + Zod
- SignalR client (`@microsoft/signalr`)
- Jest + React Testing Library, Playwright

## Running Locally

The simplest way is to run the full stack from the repo root:

```bash
cp .env.example .env
docker compose up -d --build
```

To run the frontend in watch mode against a local backend:

```bash
cd frontend
npm ci
npm run dev
```

Set the API base URL when running the frontend outside the full Docker stack:

```bash
NEXT_PUBLIC_API_URL=http://localhost:5138/api npm run dev
```

## Scripts

| Script | Command | Purpose |
|--------|---------|---------|
| Dev server | `npm run dev` | Next.js local dev with hot reload |
| Lint | `npm run lint` | ESLint |
| Type-check | `npx tsc --noEmit` | TypeScript strict check |
| Unit tests | `npm test` | Jest + React Testing Library |
| E2E tests | `npm run test:e2e` | Playwright (requires a running app) |
| Build | `npm run build` | Production Next.js build (`output: standalone`) |

## CI/CD

The frontend CI workflow (`.github/workflows/ci.yml`) runs on every push and pull request:

- `quality` — ESLint + `tsc --noEmit`
- `test` — Jest with coverage
- `e2e` — Playwright (Chromium)
- `build` — `npm run build`, uploads `.next/standalone` as the `frontend-build` artifact

Docker images are built and pushed to GHCR by `.github/workflows/build-push.yml` on every push to `main` or a `v*.*.*` tag. The three `NEXT_PUBLIC_*` build-time variables must be set as GitHub Actions secrets before the first production build — see [../docs/devops/cicd.md](../docs/devops/cicd.md) for setup commands and the full pipeline diagram.
