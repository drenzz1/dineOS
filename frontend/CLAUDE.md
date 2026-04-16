@AGENTS.md

# DineOS Frontend Rules

## Project

Next.js 15 App Router, TypeScript strict mode, Tailwind CSS, TanStack Query v5, Zustand, React Hook Form + Zod, Framer Motion, SignalR client, Playwright, Jest + RTL.

## TypeScript

- Strict mode is on. Never use `any`. Use proper generics and utility types.
- Define all API response shapes in `src/types/`. Import from there — never inline.
- Use `type` for data shapes, `interface` for component props.

## Components

- One component per file. Filename = component name (PascalCase).
- All components go in `src/components/<feature>/` or `src/components/ui/` for shared ones.
- Always type props explicitly — no implicit `any` or untyped props.
- Use Tailwind only for styling. No inline styles. No CSS modules.
- Mobile-first: always write the base style for mobile, then `md:` and `lg:` variants.

## Data Fetching

- All API calls go through `src/lib/api/`. Never call axios directly from a component.
- Use TanStack Query for all server state. Define query keys in `src/lib/api/queryKeys.ts`.
- Never use `useEffect` to fetch data. Use `useQuery` or `useMutation` instead.

## State Management

- Server state → TanStack Query. Client UI state → Zustand.
- Zustand stores live in `src/stores/`. One store per feature domain.

## Forms

- All forms use React Hook Form + Zod. Define the Zod schema first, then infer the type.
- Zod schemas go in `src/lib/validations/`.

## Hooks

- Custom hooks go in `src/hooks/`. Prefix with `use`. One hook per file.
- If logic is reused in 2+ places, extract it into a custom hook.

## Naming

- Components: PascalCase (`OrderCard.tsx`)
- Hooks: camelCase with `use` prefix (`useOrderStatus.ts`)
- Stores: camelCase with `Store` suffix (`orderStore.ts`)
- API files: camelCase with `Api` suffix (`ordersApi.ts`)
- Types: PascalCase (`Order`, `OrderStatus`, `MenuCategory`)

## DineOS Domain Language

- Use these exact names: `Order`, `OrderItem`, `MenuCategory`, `MenuItem`, `KitchenTicket`, `ShiftSummary`, `OrderStatus` (enum: `New | InProgress | Ready | Delivered | Cancelled`)
- Roles: `Manager`, `Cashier`, `KitchenStaff`

## General

- No commented-out code in commits.
- Every component file should have a single default export.
- Prefer named exports for hooks, utils, and types.
- When generating a new page, always include `loading.tsx` and `error.tsx` in the same folder.
