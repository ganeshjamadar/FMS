# Frontend Architecture Research: React 18 Web + React Native (Expo) Mobile

**Date**: 2026-02-20  
**Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Backend Research**: [research.md](research.md)  
**Scope**: Technology decisions for the React 18 web client and React Native (Expo) mobile client serving a multi-fund community lending platform

---

## Table of Contents

1. [Code Sharing Between Web and Mobile](#1-code-sharing-between-web-and-mobile)
2. [State Management](#2-state-management)
3. [API Client Layer](#3-api-client-layer)
4. [Authentication Flow](#4-authentication-flow)
5. [Offline Support (React Native)](#5-offline-support-react-native)
6. [Form Validation](#6-form-validation)
7. [PDF/CSV Export](#7-pdfcsv-export)
8. [Push Notifications](#8-push-notifications)
9. [Summary of Decisions](#9-summary-of-decisions)

---

## 1. Code Sharing Between Web and Mobile

### Decision

**pnpm workspaces** monorepo with a `packages/shared` workspace containing: generated TypeScript types from OpenAPI specs, API client functions, Zod validation schemas, Zustand store slices, and pure utility functions. Web and mobile apps are separate workspace packages that import from `@fundmanager/shared`.

### Monorepo Structure

```text
/
├── package.json                     # pnpm workspace root
├── pnpm-workspace.yaml
├── turbo.json                       # Turborepo pipeline config
├── packages/
│   └── shared/                      # @fundmanager/shared
│       ├── package.json
│       ├── tsconfig.json
│       └── src/
│           ├── api/                 # Generated OpenAPI client + custom hooks wrapper
│           │   ├── generated/       # Auto-generated types & client from OpenAPI
│           │   ├── client.ts        # Axios/fetch instance factory
│           │   ├── queries/         # TanStack Query query key factories + query fns
│           │   └── mutations/       # TanStack Query mutation helpers
│           ├── types/               # Hand-written domain types (extend generated)
│           ├── validation/          # Zod schemas (shared between web + mobile forms)
│           ├── stores/              # Zustand store slices (auth, fund context)
│           ├── utils/               # Pure functions: money formatting, date helpers
│           │   ├── money.ts         # INR formatting, decimal parsing, big.js wrappers
│           │   ├── dates.ts         # IST conversion, due date calculation
│           │   └── idempotency.ts   # UUID v4 key generation
│           └── constants/           # Shared constants (roles, statuses, limits)
├── apps/
│   ├── web/                         # fund-manager-web (React 18 + Vite)
│   │   ├── package.json
│   │   ├── vite.config.ts
│   │   └── src/
│   │       ├── components/          # Web-specific UI (Tailwind, Radix UI)
│   │       ├── pages/               # Route-level components
│   │       ├── layouts/             # Admin layout, auth layout
│   │       ├── hooks/               # Web-specific hooks (useMediaQuery, etc.)
│   │       └── router.tsx           # React Router v6 configuration
│   └── mobile/                      # fund-manager-mobile (React Native + Expo)
│       ├── package.json
│       ├── app.json                 # Expo config
│       └── src/
│           ├── components/          # RN-specific UI (React Native Paper / NativeWind)
│           ├── screens/             # Screen-level components
│           ├── navigation/          # React Navigation stack/tab config
│           ├── hooks/               # Mobile-specific (useOfflineQueue, useBiometric)
│           └── services/
│               └── offline-queue.ts # Offline write queue (mobile-only)
└── contracts/                       # OpenAPI spec files from backend services
    ├── identity-api.yaml
    ├── fundadmin-api.yaml
    ├── contributions-api.yaml
    ├── loans-api.yaml
    └── dissolution-api.yaml
```

### What Is Shared vs Platform-Specific

| Layer | Shared (`@fundmanager/shared`) | Web-Only | Mobile-Only |
|-------|-------------------------------|----------|-------------|
| **TypeScript types** | All API request/response types, domain enums, DTOs | — | — |
| **API client** | Axios instance factory, request/response interceptors, error types | Cookie-based auth interceptor | SecureStore token interceptor, offline queue interceptor |
| **TanStack Query hooks** | Query key factories, query functions, mutation functions | `useQuery`/`useMutation` wrappers (identical API) | Same hooks + offline mutation queue |
| **Validation schemas** | All Zod schemas (contribution amount, loan request, payment recording) | — | — |
| **State management** | Zustand slices: auth state, fund context, user profile | Web-specific UI state (sidebar open, modal) | Mobile-specific UI state (bottom sheet, offline indicator) |
| **Money utilities** | `formatINR()`, `parseDecimalInput()`, `big.js` arithmetic | — | — |
| **Date utilities** | `toIST()`, `formatDueDate()`, `isOverdue()` | — | — |
| **Idempotency** | `generateIdempotencyKey()` (UUID v4) | — | — |
| **UI components** | **None** — UI is always platform-specific | Tailwind CSS + Radix UI primitives + custom components | React Native Paper / NativeWind + custom components |
| **Navigation/Routing** | **None** | React Router v6 | React Navigation v6 |
| **Storage** | **None** | localStorage / cookies | AsyncStorage / SecureStore |
| **Push notifications** | **None** | — | Expo Notifications |

### Monorepo Tool: Turborepo + pnpm Workspaces

| Tool | Pros | Cons | Verdict |
|------|------|------|---------|
| **pnpm workspaces + Turborepo** | Fast installs (content-addressable store), excellent workspace protocol (`workspace:*`), Turborepo adds build caching + task orchestration, minimal config, no code generation overhead | Turborepo is less feature-rich than Nx for custom generators | **Selected** |
| **Nx** | Most powerful: dependency graph, affected command (only rebuild what changed), generators for scaffolding, excellent caching | Heavy setup, opinionated project structure, learning curve, migration complexity if you outgrow it, larger community mostly in Angular ecosystem | Rejected |
| **Yarn workspaces + Lerna** | Mature ecosystem | Lerna is end-of-life (maintenance mode under Nx), Yarn 4 (Berry) PnP has compatibility issues with React Native / Expo | Rejected |
| **Bare pnpm workspaces (no Turborepo)** | Simplest setup | No build caching, no task orchestration, no parallel builds with dependency awareness | Rejected — Turborepo adds trivial config cost for significant build-time gains |

#### pnpm-workspace.yaml

```yaml
packages:
  - "packages/*"
  - "apps/*"
```

#### turbo.json

```json
{
  "$schema": "https://turbo.build/schema.json",
  "globalDependencies": ["**/.env.*local"],
  "pipeline": {
    "build": {
      "dependsOn": ["^build"],
      "outputs": ["dist/**", ".next/**", ".expo/**"]
    },
    "dev": {
      "cache": false,
      "persistent": true
    },
    "lint": {},
    "test": {
      "dependsOn": ["build"]
    },
    "generate:api": {
      "outputs": ["packages/shared/src/api/generated/**"]
    }
  }
}
```

### OpenAPI Type Generation

**Decision**: Use **openapi-typescript** (for types) + **openapi-fetch** (for a type-safe fetch client) to generate TypeScript types and a client from the backend's OpenAPI specs.

#### Why openapi-typescript + openapi-fetch

| Tool | Types Only | Runtime Client | Bundle Size | Type Safety |
|------|-----------|----------------|-------------|-------------|
| **openapi-typescript + openapi-fetch** | Yes (zero runtime for types) | Thin fetch wrapper (~2KB) | Minimal | Excellent — paths, params, request/response fully typed |
| **openapi-generator (typescript-axios)** | + runtime client | Full Axios-based client | Larger (~50KB+) | Good but generates large class-based code |
| **orval** | Yes | Generates TanStack Query hooks | Medium | Excellent — can generate query/mutation hooks directly |
| **swagger-typescript-api** | Yes | Fetch/Axios client | Medium | Good |

**Primary**: `openapi-typescript` for types + `openapi-fetch` for the client. This gives the thinnest runtime with the best type safety.

**Considered alternative**: `orval` is compelling because it generates TanStack Query hooks directly from the OpenAPI spec. However, its generated hooks are opinionated and harder to customize for our specific needs (offline queue, optimistic updates with fund-scoped cache invalidation). We write the TanStack Query layer manually using the generated types — this gives full control over query keys, stale times, and mutation side effects.

#### Generation Script

```json
// package.json (packages/shared)
{
  "scripts": {
    "generate:api": "openapi-typescript ../contracts/identity-api.yaml -o src/api/generated/identity.ts && openapi-typescript ../contracts/contributions-api.yaml -o src/api/generated/contributions.ts && openapi-typescript ../contracts/loans-api.yaml -o src/api/generated/loans.ts && openapi-typescript ../contracts/fundadmin-api.yaml -o src/api/generated/fundadmin.ts && openapi-typescript ../contracts/dissolution-api.yaml -o src/api/generated/dissolution.ts"
  }
}
```

Types are regenerated when the backend OpenAPI specs change (`turbo run generate:api`). The generated files are committed to the repo for CI stability.

### Risks & Gotchas

- **React Native compatibility of shared packages**: All shared code must be pure TypeScript — no DOM APIs, no Node.js APIs, no browser-specific globals. Enforce this with a custom ESLint rule or `tsconfig.json` `lib` set to `["ES2022"]` only (no `"DOM"`).
- **big.js in React Native**: `big.js` is a pure JS library and works in React Native without issues. Verified compatible.
- **Turborepo + Expo**: Turborepo works with Expo managed workflow. The key is that `apps/mobile` must declare `@fundmanager/shared` as a dependency, and Metro bundler must be configured to resolve the workspace symlink:
  ```js
  // metro.config.js
  const { getDefaultConfig } = require('expo/metro-config');
  const path = require('path');
  
  const config = getDefaultConfig(__dirname);
  
  // Resolve workspace packages
  config.watchFolders = [path.resolve(__dirname, '../../packages/shared')];
  config.resolver.nodeModulesPaths = [
    path.resolve(__dirname, 'node_modules'),
    path.resolve(__dirname, '../../node_modules'),
  ];
  
  module.exports = config;
  ```
- **Shared package build**: The `@fundmanager/shared` package should be consumed as TypeScript source (not pre-built). Both Vite (web) and Metro (mobile) can transpile TS directly. Use `"main": "src/index.ts"` in the shared package's `package.json` with `tsconfig` paths.

---

## 2. State Management

### Decision

**Zustand** for global client state + **TanStack Query** for all server state. Zustand manages auth context, current fund selection, and UI preferences. TanStack Query manages all API data (contributions, loans, members, etc.) with its built-in cache.

### Why This Split

The critical distinction is **server state** (data that lives on the backend and is fetched/mutated via API) vs **client state** (data that exists only in the client: which fund is selected, is the user logged in, UI toggles).

| State Type | Examples | Managed By |
|-----------|----------|------------|
| **Server state** | Contribution list, loan details, member list, fund balance, repayment schedule, voting results | TanStack Query (cache + fetch lifecycle) |
| **Client state — Auth** | Logged-in user, access token, token expiry | Zustand `authSlice` |
| **Client state — Fund context** | Currently selected fund ID, fund role | Zustand `fundSlice` |
| **Client state — UI** | Sidebar collapsed, active tab, dark mode | Zustand `uiSlice` (platform-specific) |

### Zustand vs Alternatives

| Criterion | Zustand | Redux Toolkit (RTK) | Jotai | Verdict |
|-----------|---------|---------------------|-------|---------|
| **Bundle size** | ~1KB | ~11KB (RTK) + ~2KB (react-redux) | ~3KB | Zustand wins |
| **Boilerplate** | Minimal — define a store function, done | Moderate — slices, reducers, configureStore, Provider | Minimal — atom-based | Zustand ≈ Jotai < RTK |
| **DevTools** | Built-in middleware (`devtools()`) | Excellent (Redux DevTools) | Limited | RTK wins |
| **Middleware** | `persist`, `devtools`, `immer` — composable | Extensive (RTK Query, Listener) | Limited built-in | RTK wins |
| **RTK Query overlap** | No overlap — Zustand does not manage server state | RTK Query overlaps with TanStack Query — must choose one | No overlap | Zustand avoids duplication |
| **React Native compatibility** | Excellent | Excellent | Excellent | Tie |
| **Persistence** | `zustand/middleware` persist → pluggable storage (localStorage, AsyncStorage) | redux-persist (works but extra package with maintenance concerns) | Custom | Zustand wins |
| **Learning curve** | Low | Moderate (actions, reducers, thunks, slices) | Low | Zustand ≈ Jotai < RTK |
| **Community / ecosystem** | Large, growing | Largest | Medium, growing | RTK wins |
| **Suitability for this project** | **Excellent** — small client state surface, TanStack Query handles server state | Overkill — RTK Query would duplicate TanStack Query, and plain RTK adds ceremony for ~3 slices of state | Good for atomic UI state, but less suited for auth/fund context that needs to be read across the tree | **Zustand selected** |

#### Why Not Redux Toolkit

Redux Toolkit is excellent, but for this project:

1. **RTK Query vs TanStack Query**: We'd have to choose one for server state. TanStack Query is more mature for React-specific patterns (suspense, infinite queries, optimistic updates, offline support). Using RTK Query would lock us into the Redux ecosystem for server state. Using RTK without RTK Query means we're using Redux just for ~3 slices of client state — excessive ceremony.
2. **Boilerplate ratio**: For 3 client state slices (auth, fund context, UI prefs), Redux requires `configureStore`, `Provider`, action creators, reducers, and selectors. Zustand achieves the same with a single function per store.
3. **No large team coordination need**: Redux shines when many developers need strict conventions for state mutations. This is a small-team project where Zustand's simplicity is a strength.

#### Why Not Jotai

Jotai's atom model is elegant for granular, derived state (e.g., computed values that depend on other atoms). But:

1. **Auth state is not atomic**: The auth slice is a cohesive unit (user, token, expiry, isAuthenticated) — storing these as separate atoms introduces coordination complexity.
2. **Fund context is single-source**: The current fund ID + role is one stateful unit that many components read — a Zustand store is more natural than an atom.
3. **Persistence story**: Zustand's `persist` middleware with pluggable storage (localStorage for web, AsyncStorage for mobile) is turnkey. Jotai persistence requires custom implementation.

### Zustand Store Design

```typescript
// packages/shared/src/stores/auth-store.ts
import { create } from 'zustand';
import { persist, createJSONStorage } from 'zustand/middleware';

interface AuthState {
  user: { id: string; name: string; phone: string; email: string } | null;
  accessToken: string | null;
  refreshToken: string | null;
  tokenExpiresAt: number | null;
  isAuthenticated: boolean;
  
  // Actions
  setAuth: (user: AuthState['user'], accessToken: string, refreshToken: string, expiresAt: number) => void;
  clearAuth: () => void;
  updateToken: (accessToken: string, expiresAt: number) => void;
}

// Storage is injected by the platform (web: localStorage, mobile: AsyncStorage)
export const createAuthStore = (storage: any) =>
  create<AuthState>()(
    persist(
      (set) => ({
        user: null,
        accessToken: null,
        refreshToken: null,
        tokenExpiresAt: null,
        isAuthenticated: false,

        setAuth: (user, accessToken, refreshToken, expiresAt) =>
          set({ user, accessToken, refreshToken, tokenExpiresAt: expiresAt, isAuthenticated: true }),

        clearAuth: () =>
          set({ user: null, accessToken: null, refreshToken: null, tokenExpiresAt: null, isAuthenticated: false }),

        updateToken: (accessToken, expiresAt) =>
          set({ accessToken, tokenExpiresAt: expiresAt }),
      }),
      {
        name: 'fundmanager-auth',
        storage: createJSONStorage(() => storage),
        // Don't persist tokens on web if using httpOnly cookies
        partialize: (state) => ({
          user: state.user,
          isAuthenticated: state.isAuthenticated,
        }),
      }
    )
  );
```

```typescript
// packages/shared/src/stores/fund-store.ts
import { create } from 'zustand';

interface FundState {
  currentFundId: string | null;
  currentFundName: string | null;
  currentRole: 'Admin' | 'Editor' | 'Guest' | null;
  
  setFund: (fundId: string, fundName: string, role: FundState['currentRole']) => void;
  clearFund: () => void;
}

export const useFundStore = create<FundState>()((set) => ({
  currentFundId: null,
  currentFundName: null,
  currentRole: null,
  
  setFund: (fundId, fundName, role) =>
    set({ currentFundId: fundId, currentFundName: fundName, currentRole: role }),
    
  clearFund: () =>
    set({ currentFundId: null, currentFundName: null, currentRole: null }),
}));
```

### Risks & Gotchas

- **Zustand and SSR**: Not relevant for this project (Vite SPA, not Next.js). No SSR hydration concerns.
- **Token storage split**: On web, if using httpOnly cookies, the Zustand store doesn't hold the access token — only the user profile and `isAuthenticated` flag. On mobile, the Zustand store holds the token reference but the actual token is in SecureStore. The persist middleware's `partialize` function controls what gets persisted.
- **Fund context vs URL**: On web, the current fund should be part of the URL (`/funds/:fundId/contributions`). The `fundSlice` reflects what's in the route param. On mobile, it's stored in the Zustand store and passed via React Navigation params.
- **Avoid server state in Zustand**: Never store fetched API data (contribution lists, loan details) in Zustand. Always use TanStack Query. This avoids stale data and cache synchronization issues.

---

## 3. API Client Layer

### Decision

**TanStack Query v5** for all server state management with: query key factories per domain, paginated queries using `useInfiniteQuery`, optimistic mutations with rollback, idempotency key generation per mutation, configurable retry logic, and an offline mutation queue for React Native.

### TanStack Query Architecture

```typescript
// packages/shared/src/api/client.ts
import createClient from 'openapi-fetch';
import type { paths as ContributionPaths } from './generated/contributions';
import type { paths as LoanPaths } from './generated/loans';

// Platform-specific: web sets baseUrl + credentials: 'include'
// Mobile sets baseUrl + Authorization header from SecureStore
export type ApiClientConfig = {
  baseUrl: string;
  getAccessToken?: () => Promise<string | null>;
  onUnauthorized?: () => void;
};

export function createApiClients(config: ApiClientConfig) {
  const commonFetch: typeof fetch = async (input, init) => {
    const headers = new Headers(init?.headers);
    
    // Mobile: attach bearer token
    if (config.getAccessToken) {
      const token = await config.getAccessToken();
      if (token) headers.set('Authorization', `Bearer ${token}`);
    }
    
    const response = await fetch(input, { ...init, headers });
    
    if (response.status === 401) {
      config.onUnauthorized?.();
    }
    
    return response;
  };

  return {
    contributions: createClient<ContributionPaths>({
      baseUrl: config.baseUrl,
      fetch: commonFetch,
    }),
    loans: createClient<LoanPaths>({
      baseUrl: config.baseUrl,
      fetch: commonFetch,
    }),
    // ... other service clients
  };
}
```

### Query Key Factories

A structured query key system enables precise cache invalidation after mutations:

```typescript
// packages/shared/src/api/queries/keys.ts

export const contributionKeys = {
  all: ['contributions'] as const,
  
  // Fund-scoped
  lists: (fundId: string) => [...contributionKeys.all, 'list', fundId] as const,
  list: (fundId: string, filters: { month?: number; year?: number; status?: string }) =>
    [...contributionKeys.lists(fundId), filters] as const,
  
  // Individual due
  details: (fundId: string) => [...contributionKeys.all, 'detail', fundId] as const,
  detail: (fundId: string, dueId: string) =>
    [...contributionKeys.details(fundId), dueId] as const,
  
  // Ledger (paginated)
  ledger: (fundId: string, filters?: { page?: number; pageSize?: number }) =>
    [...contributionKeys.all, 'ledger', fundId, filters] as const,
};

export const loanKeys = {
  all: ['loans'] as const,
  lists: (fundId: string) => [...loanKeys.all, 'list', fundId] as const,
  list: (fundId: string, filters: { status?: string; page?: number }) =>
    [...loanKeys.lists(fundId), filters] as const,
  detail: (fundId: string, loanId: string) =>
    [...loanKeys.all, 'detail', fundId, loanId] as const,
  repayments: (fundId: string, loanId: string) =>
    [...loanKeys.all, 'repayments', fundId, loanId] as const,
};

export const fundKeys = {
  all: ['funds'] as const,
  list: (filters?: { status?: string }) => [...fundKeys.all, 'list', filters] as const,
  detail: (fundId: string) => [...fundKeys.all, 'detail', fundId] as const,
  members: (fundId: string) => [...fundKeys.all, 'members', fundId] as const,
  dashboard: (fundId: string) => [...fundKeys.all, 'dashboard', fundId] as const,
};
```

### Paginated List Queries

Contribution dues and loan lists require cursor or offset-based pagination:

```typescript
// packages/shared/src/api/queries/contributions.ts
import { useInfiniteQuery, useQuery } from '@tanstack/react-query';
import { contributionKeys } from './keys';

export function useContributionDues(fundId: string, month: number, year: number) {
  return useQuery({
    queryKey: contributionKeys.list(fundId, { month, year }),
    queryFn: async () => {
      const { data, error } = await api.contributions.GET(
        '/api/contributions/funds/{fundId}/dues',
        { params: { path: { fundId }, query: { month, year } } }
      );
      if (error) throw error;
      return data;
    },
    staleTime: 30_000, // 30s — contribution data changes when payments are recorded
  });
}

// Paginated ledger with infinite scroll
export function useContributionLedger(fundId: string) {
  return useInfiniteQuery({
    queryKey: contributionKeys.ledger(fundId),
    queryFn: async ({ pageParam = 1 }) => {
      const { data, error } = await api.contributions.GET(
        '/api/contributions/funds/{fundId}/ledger',
        { params: { path: { fundId }, query: { page: pageParam, pageSize: 20 } } }
      );
      if (error) throw error;
      return data;
    },
    getNextPageParam: (lastPage) =>
      lastPage.hasMore ? lastPage.page + 1 : undefined,
    initialPageParam: 1,
    staleTime: 60_000, // 1min — ledger is append-only, changes infrequently
  });
}
```

### Optimistic Mutations with Rollback

Payment recording is the primary optimistic update scenario. The user presses "Record Payment" and immediately sees the UI reflect the payment while the server processes it:

```typescript
// packages/shared/src/api/mutations/contributions.ts
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { contributionKeys } from '../queries/keys';
import { generateIdempotencyKey } from '../../utils/idempotency';

interface RecordPaymentParams {
  fundId: string;
  dueId: string;
  amount: number; // decimal as number — serialized to string in request
}

export function useRecordContributionPayment() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (params: RecordPaymentParams) => {
      const idempotencyKey = generateIdempotencyKey();
      
      const { data, error } = await api.contributions.POST(
        '/api/contributions/funds/{fundId}/dues/{dueId}/payments',
        {
          params: { path: { fundId: params.fundId, dueId: params.dueId } },
          body: { amount: params.amount.toString(), idempotencyKey },
          headers: { 'Idempotency-Key': idempotencyKey },
        }
      );
      if (error) throw error;
      return data;
    },

    // Optimistic update
    onMutate: async (params) => {
      // Cancel any in-flight refetches to prevent overwriting our optimistic update
      await queryClient.cancelQueries({
        queryKey: contributionKeys.lists(params.fundId),
      });

      // Snapshot previous value
      const previousDues = queryClient.getQueryData(
        contributionKeys.list(params.fundId, {})
      );

      // Optimistically update the due
      queryClient.setQueriesData(
        { queryKey: contributionKeys.lists(params.fundId) },
        (old: any) => {
          if (!old) return old;
          return {
            ...old,
            items: old.items.map((due: any) =>
              due.id === params.dueId
                ? {
                    ...due,
                    amountPaid: due.amountPaid + params.amount,
                    remainingBalance: due.remainingBalance - params.amount,
                    status: due.remainingBalance - params.amount <= 0 ? 'Paid' : 'Partial',
                  }
                : due
            ),
          };
        }
      );

      return { previousDues };
    },

    // Rollback on error
    onError: (_err, params, context) => {
      if (context?.previousDues) {
        queryClient.setQueryData(
          contributionKeys.list(params.fundId, {}),
          context.previousDues
        );
      }
    },

    // Refetch after success or error to ensure consistency
    onSettled: (_data, _error, params) => {
      queryClient.invalidateQueries({
        queryKey: contributionKeys.lists(params.fundId),
      });
      // Also invalidate the fund dashboard (balance changed)
      queryClient.invalidateQueries({
        queryKey: fundKeys.dashboard(params.fundId),
      });
    },
  });
}
```

### Idempotency Key Generation

```typescript
// packages/shared/src/utils/idempotency.ts
import { v4 as uuidv4 } from 'uuid';

/**
 * Generate a client-side idempotency key for payment/repayment mutations.
 * Per spec NFR-010: "All payment and repayment posting endpoints accept 
 * a client-generated idempotency key to prevent duplicate transactions."
 * 
 * Keys are UUID v4 — globally unique, no coordination needed.
 */
export function generateIdempotencyKey(): string {
  return uuidv4();
}

/**
 * For offline queue: generate the idempotency key at mutation creation time
 * (not at send time) so retries use the same key.
 */
export function createIdempotentMutation<T>(params: T): T & { idempotencyKey: string } {
  return { ...params, idempotencyKey: generateIdempotencyKey() };
}
```

### Retry Logic

```typescript
// packages/shared/src/api/query-client.ts
import { QueryClient } from '@tanstack/react-query';

export function createQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 30_000,       // 30 seconds
        gcTime: 5 * 60_000,     // 5 minutes garbage collection
        retry: (failureCount, error: any) => {
          // Don't retry 4xx errors (client errors)
          if (error?.status >= 400 && error?.status < 500) return false;
          // Don't retry 409 (concurrency conflict) — user must refresh
          if (error?.status === 409) return false;
          // Retry server errors up to 3 times
          return failureCount < 3;
        },
        retryDelay: (attemptIndex) =>
          Math.min(1000 * 2 ** attemptIndex, 30000), // Exponential backoff, max 30s
      },
      mutations: {
        retry: false, // Don't auto-retry mutations — idempotency key complicates retries
      },
    },
  });
}
```

### Concurrency Conflict Handling (409 Response)

Per spec FR-035a (optimistic locking), the server returns 409 when a `ContributionDue` or `RepaymentEntry` has been modified by another user:

```typescript
// packages/shared/src/api/errors.ts
export class ConcurrencyConflictError extends Error {
  constructor(
    message: string,
    public entityId: string,
    public entityType: string,
  ) {
    super(message);
    this.name = 'ConcurrencyConflictError';
  }
}

// In the mutation's onError:
onError: (error, params) => {
  if (error instanceof ConcurrencyConflictError) {
    // Show user-friendly message: "This payment was updated by another user. Refreshing..."
    // Auto-refetch the due
    queryClient.invalidateQueries({
      queryKey: contributionKeys.detail(params.fundId, params.dueId),
    });
  }
}
```

### Offline Mutation Queue (React Native)

See [Section 5](#5-offline-support-react-native) for the full offline queue design. TanStack Query v5 supports `onlineMutationManager` and `mutationCache` persistence for this purpose.

### Alternatives Considered

| Alternative | Why Rejected |
|-------------|-------------|
| **SWR** | Less feature-complete than TanStack Query: no built-in mutation support, no infinite queries in the core, no offline persistence, no devtools. SWR is simpler but the fund management domain needs the advanced features. |
| **RTK Query** | Ties server state to Redux. Would require Redux as a dependency even though we chose Zustand for client state. TanStack Query is framework-agnostic and more flexible for React Native offline patterns. |
| **Apollo Client (GraphQL)** | The backend exposes REST/OpenAPI APIs (per plan.md). Adopting GraphQL would require either a GraphQL gateway or a complete backend rewrite. Not viable. |
| **Custom fetch hooks** | Reinvents caching, deduplication, background refetching, pagination, optimistic updates. TanStack Query exists precisely to avoid this. |

### Risks & Gotchas

- **Monetary values as strings in JSON**: Per backend research Section 6, monetary `decimal` values are serialized as JSON strings to avoid JavaScript floating-point precision loss. The API client must parse these into `big.js` instances (not `Number`) for display and calculation. The shared `money.ts` utility handles this.
- **Query key consistency**: All components accessing the same data must use the exact same query key. The key factory pattern above enforces this — never construct keys inline.
- **Fund-scoped invalidation**: When switching funds, invalidate all queries for the previous fund to prevent stale data leaks. Use `queryClient.removeQueries({ queryKey: contributionKeys.all })` on fund switch.
- **Stale time tuning**: Contribution dues need a shorter stale time (30s) than the fund ledger (1min) because dues change more frequently during active payment recording. Tune based on real usage patterns.

---

## 4. Authentication Flow

### Decision

OTP-based authentication (no password) with platform-specific token handling: **httpOnly cookies** for web (set by the backend), **Expo SecureStore** for mobile (storing JWT access/refresh tokens). A shared auth state store (Zustand) tracks the current user and authentication status on both platforms.

### OTP Authentication Flow

```
┌─────────────┐          ┌──────────────┐          ┌───────────────────┐
│  Client App  │         │  API Gateway  │         │  Identity Service  │
└─────┬───────┘          └──────┬───────┘          └─────────┬─────────┘
      │                         │                            │
      │ POST /auth/otp/request  │                            │
      │ { phone: "+91..." }     │                            │
      ├────────────────────────►├───────────────────────────►│
      │                         │                            │ Generate OTP
      │                         │                            │ Send via SMS/Email
      │       202 Accepted      │       202 Accepted         │
      │◄────────────────────────┤◄───────────────────────────┤
      │ { challengeId: "..." }  │                            │
      │                         │                            │
      │  [User receives OTP]    │                            │
      │                         │                            │
      │ POST /auth/otp/verify   │                            │
      │ { challengeId, code }   │                            │
      ├────────────────────────►├───────────────────────────►│
      │                         │                            │ Validate OTP
      │                         │                            │ Issue tokens
      │                         │                            │
      │  ── WEB: Set-Cookie ──  │                            │
      │  httpOnly, Secure,      │       200 OK               │
      │  SameSite=Strict        │  { user, accessToken,      │
      │◄────────────────────────┤   refreshToken, expiresAt }│
      │                         │◄───────────────────────────┤
      │  ── MOBILE: JSON body ─ │                            │
      │  { accessToken,         │                            │
      │    refreshToken,        │                            │
      │    expiresAt }          │                            │
      │                         │                            │
```

### Web: httpOnly Cookie Strategy

**Decision**: The backend sets httpOnly cookies for web. The web client never touches tokens directly.

| Aspect | Implementation |
|--------|---------------|
| **Token delivery** | `Set-Cookie` header on OTP verify response |
| **Cookie attributes** | `httpOnly`, `Secure`, `SameSite=Strict`, `Path=/api` |
| **Token in requests** | Automatic — browser includes cookies on every request to the same origin |
| **CSRF protection** | `SameSite=Strict` + CSRF token in a separate non-httpOnly cookie or response header |
| **Token refresh** | Background `POST /auth/refresh` that returns a new `Set-Cookie` |
| **Logout** | `POST /auth/logout` clears the cookie server-side |

```typescript
// apps/web/src/api/setup.ts
import { createApiClients } from '@fundmanager/shared';

export const apiClients = createApiClients({
  baseUrl: import.meta.env.VITE_API_BASE_URL,
  // No getAccessToken — cookies are automatic
  onUnauthorized: () => {
    window.location.href = '/login';
  },
});

// Fetch options for web — include credentials (cookies)
// openapi-fetch configuration:
const client = createClient<paths>({
  baseUrl: import.meta.env.VITE_API_BASE_URL,
  credentials: 'include', // Sends cookies with every request
});
```

#### Why httpOnly Cookies for Web (Not localStorage)

| Factor | httpOnly Cookie | localStorage + Bearer Token |
|--------|-----------------|----------------------------|
| **XSS attack surface** | Token inaccessible to JavaScript — XSS cannot steal it | Token accessible via `localStorage.getItem()` — XSS can exfiltrate |
| **CSRF attack surface** | Requires CSRF protection (SameSite=Strict mitigates) | No CSRF risk (token must be explicitly added to headers) |
| **Automatic inclusion** | Browser sends cookie automatically | Must add `Authorization` header to every request |
| **Refresh UX** | Seamless — browser handles cookie replacement | Must implement token rotation logic in JS |
| **SSR compatibility** | Cookies travel with server-side requests | N/A (SPA) |

**Verdict**: XSS is a more common and severe attack vector than CSRF. httpOnly cookies eliminate the most dangerous token theft vector. `SameSite=Strict` handles CSRF with zero additional code.

### Mobile: Expo SecureStore Strategy

**Decision**: Store JWT tokens in Expo SecureStore (encrypted device storage). Attach as `Authorization: Bearer <token>` header on every request.

```typescript
// apps/mobile/src/services/auth-storage.ts
import * as SecureStore from 'expo-secure-store';

const ACCESS_TOKEN_KEY = 'fm_access_token';
const REFRESH_TOKEN_KEY = 'fm_refresh_token';

export const authStorage = {
  async getAccessToken(): Promise<string | null> {
    return SecureStore.getItemAsync(ACCESS_TOKEN_KEY);
  },

  async setAccessToken(token: string): Promise<void> {
    await SecureStore.setItemAsync(ACCESS_TOKEN_KEY, token);
  },

  async getRefreshToken(): Promise<string | null> {
    return SecureStore.getItemAsync(REFRESH_TOKEN_KEY);
  },

  async setRefreshToken(token: string): Promise<void> {
    await SecureStore.setItemAsync(REFRESH_TOKEN_KEY, token);
  },

  async clearTokens(): Promise<void> {
    await SecureStore.deleteItemAsync(ACCESS_TOKEN_KEY);
    await SecureStore.deleteItemAsync(REFRESH_TOKEN_KEY);
  },
};
```

```typescript
// apps/mobile/src/api/setup.ts
import { createApiClients } from '@fundmanager/shared';
import { authStorage } from '../services/auth-storage';

export const apiClients = createApiClients({
  baseUrl: process.env.EXPO_PUBLIC_API_BASE_URL!,
  getAccessToken: () => authStorage.getAccessToken(),
  onUnauthorized: () => {
    // Trigger token refresh, or navigate to login
    authStorage.clearTokens();
    // Navigation to login screen handled by auth state listener
  },
});
```

#### Why SecureStore for Mobile (Not AsyncStorage)

| Storage | Encryption | Biometric Protection | Capacity | Verdict |
|---------|-----------|---------------------|----------|---------|
| **Expo SecureStore** | Hardware-backed keychain (iOS) / Keystore (Android) | Optional | 2KB per item (sufficient for JWT) | **Selected** for tokens |
| **AsyncStorage** | None (plaintext on disk) | None | Large | Rejected for sensitive data; OK for offline queue |
| **MMKV** | Optional encryption | No native support | Large, fast | Overkill for token storage; consider for offline queue if AsyncStorage perf is insufficient |

### Token Refresh Flow

```typescript
// packages/shared/src/api/token-refresh.ts

let refreshPromise: Promise<string> | null = null;

/**
 * Refresh the access token. Deduplicates concurrent refresh attempts.
 * Platform-specific: web calls POST /auth/refresh (cookie-based);
 * mobile calls POST /auth/refresh with refresh token in body.
 */
export async function refreshAccessToken(
  refreshFn: () => Promise<{ accessToken: string; expiresAt: number }>,
  onRefreshed: (accessToken: string, expiresAt: number) => void,
  onFailed: () => void,
): Promise<string> {
  // Deduplicate: if a refresh is already in-flight, wait for it
  if (refreshPromise) return refreshPromise;

  refreshPromise = (async () => {
    try {
      const { accessToken, expiresAt } = await refreshFn();
      onRefreshed(accessToken, expiresAt);
      return accessToken;
    } catch {
      onFailed(); // Clear auth state, redirect to login
      throw new Error('Token refresh failed');
    } finally {
      refreshPromise = null;
    }
  })();

  return refreshPromise;
}
```

#### Proactive Token Refresh

Refresh the token **before** it expires to avoid interrupting user actions:

```typescript
// Schedule refresh at 80% of token lifetime
function scheduleTokenRefresh(expiresAt: number) {
  const now = Date.now();
  const lifetime = expiresAt - now;
  const refreshAt = lifetime * 0.8;
  
  setTimeout(() => {
    refreshAccessToken(/* ... */);
  }, refreshAt);
}
```

### Protected Routes

#### Web (React Router v6)

```typescript
// apps/web/src/router.tsx
import { Navigate, Outlet } from 'react-router-dom';
import { useAuthStore } from '@fundmanager/shared';

function ProtectedRoute() {
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated);
  
  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }
  
  return <Outlet />;
}

function RoleGuard({ requiredRole }: { requiredRole: string }) {
  const role = useFundStore((s) => s.currentRole);
  
  if (role && !hasPermission(role, requiredRole)) {
    return <Navigate to="/unauthorized" replace />;
  }
  
  return <Outlet />;
}

// Router setup
const router = createBrowserRouter([
  { path: '/login', element: <LoginPage /> },
  {
    element: <ProtectedRoute />,
    children: [
      { path: '/funds', element: <FundListPage /> },
      {
        path: '/funds/:fundId',
        element: <FundLayout />,
        children: [
          { path: 'dashboard', element: <FundDashboard /> },
          { path: 'contributions', element: <ContributionDashboard /> },
          {
            element: <RoleGuard requiredRole="Admin" />,
            children: [
              { path: 'settings', element: <FundSettings /> },
              { path: 'audit', element: <AuditLogViewer /> },
            ],
          },
        ],
      },
    ],
  },
]);
```

#### Mobile (React Navigation)

```typescript
// apps/mobile/src/navigation/RootNavigator.tsx
import { useAuthStore } from '@fundmanager/shared';

export function RootNavigator() {
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated);
  
  return (
    <NavigationContainer>
      {isAuthenticated ? (
        <AppNavigator />  // Tab navigator with fund screens
      ) : (
        <AuthNavigator />  // Login + OTP verification stack
      )}
    </NavigationContainer>
  );
}
```

### Rate Limiting on OTP (Client-Side UX)

Per NFR-006: "max 5 OTP requests per phone/email per 15 minutes." Client UX:

- Disable "Resend OTP" button for 60 seconds after each request (countdown timer).
- After 5 requests, show "Too many attempts. Please try again in X minutes."
- Server enforces this regardless; client UX prevents unnecessary requests.

### Alternatives Considered

| Alternative | Why Rejected |
|-------------|-------------|
| **localStorage for web tokens** | XSS vulnerability. Any injected script can steal the token. httpOnly cookies are more secure for a financial application. |
| **Session-based auth (no JWT)** | Requires server-side session storage. In a microservices architecture, every service would need access to the session store, or the gateway would need to exchange sessions for tokens. JWTs are self-contained and validated locally. |
| **OAuth2 / OIDC with external provider** | Overengineered for OTP auth. The spec mandates phone/email OTP only. Adding an OAuth2 server (Keycloak, Auth0) introduces significant complexity and cost. Could be added later if needed. |
| **Biometric-only auth** | Biometric is a device-local unlock, not a server authentication method. It can be used to unlock the stored tokens (SecureStore supports this) but cannot replace OTP for initial authentication. |

### Risks & Gotchas

- **httpOnly cookie + CORS**: The web app and API must be on the same domain (or subdomains) for `SameSite=Strict` cookies to work. If the API is on a different domain, use `SameSite=None; Secure` with CORS `credentials: true` — but this weakens CSRF protection.
- **Token expiry race**: If the access token expires while a mutation is in-flight, the mutation fails. The 401 interceptor should trigger a token refresh and retry the failed request. Implement a request queue that pauses during refresh.
- **SecureStore size limit**: Expo SecureStore has a 2KB limit per item. JWT tokens can be large if they contain many claims. Keep JWT claims minimal (userId, roles) and fetch profile data separately.
- **Expo SecureStore on web**: `expo-secure-store` does not work on web. If using Expo for web (which we're not — we have a separate Vite web app), this would be an issue. Our architecture avoids this by having separate web and mobile apps.

---

## 5. Offline Support (React Native)

### Decision

**Lightweight offline queue** using TanStack Query's built-in `MutationCache` persistence + AsyncStorage for queued mutations. No local database for MVP. Queue writes when offline, sync sequentially on reconnect, use idempotency keys for dedup, and show optimistic UI with pending indicators.

### MVP Offline Scope

Per plan.md: "React Native queues writes in AsyncStorage when offline; sync on reconnect with conflict resolution preserving ledger integrity."

For MVP, offline capability covers:

| Feature | Offline Support | Rationale |
|---------|----------------|-----------|
| **View fund dashboard** | Yes (cached) | TanStack Query serves stale cached data when offline |
| **View contribution dues** | Yes (cached) | Last fetched data shown with "offline" indicator |
| **Record contribution payment** | Yes (queued) | Most critical offline action — members may pay at group meetings without connectivity |
| **Record loan repayment** | Yes (queued) | Important — repayment recording in low-connectivity areas |
| **Request a loan** | No | Low urgency; can wait for connectivity |
| **View reports** | Partial (cached) | Previously viewed reports available; no new generation |
| **Cast a vote** | No | Time-sensitive but requires fresh data |
| **Dissolution actions** | No | Admin-only, complex, requires server validation |

### Architecture: TanStack Query Offline Persistence

TanStack Query v5 supports persisting the mutation queue and query cache:

```typescript
// apps/mobile/src/api/offline-setup.ts
import { QueryClient } from '@tanstack/react-query';
import { createAsyncStoragePersister } from '@tanstack/query-async-storage-persister';
import { PersistQueryClientProvider } from '@tanstack/react-query-persist-client';
import AsyncStorage from '@react-native-async-storage/async-storage';
import NetInfo from '@react-native-community/netinfo';
import { onlineManager } from '@tanstack/react-query';

// 1. Tell TanStack Query about network status
onlineManager.setEventListener((setOnline) => {
  return NetInfo.addEventListener((state) => {
    setOnline(!!state.isConnected);
  });
});

// 2. Persist query cache to AsyncStorage
const asyncStoragePersister = createAsyncStoragePersister({
  storage: AsyncStorage,
  key: 'FUND_MANAGER_QUERY_CACHE',
  throttleTime: 1000, // Debounce writes to AsyncStorage
});

// 3. Configure QueryClient for offline
const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      gcTime: 1000 * 60 * 60 * 24, // 24 hours — keep cache for offline access
      staleTime: 30_000,
      networkMode: 'offlineFirst', // Serve cache first, refetch in background
    },
    mutations: {
      networkMode: 'offlineFirst', // Queue mutations when offline
      retry: 3,
      retryDelay: (attemptIndex) => Math.min(1000 * 2 ** attemptIndex, 30000),
    },
  },
});

// 4. App wrapper
export function OfflineQueryProvider({ children }: { children: React.ReactNode }) {
  return (
    <PersistQueryClientProvider
      client={queryClient}
      persistOptions={{
        persister: asyncStoragePersister,
        maxAge: 1000 * 60 * 60 * 24, // 24 hours
        dehydrateOptions: {
          shouldDehydrateQuery: (query) => {
            // Only persist fund-scoped queries, not ephemeral UI queries
            const key = query.queryKey[0] as string;
            return ['contributions', 'loans', 'funds'].includes(key);
          },
        },
      }}
    >
      {children}
    </PersistQueryClientProvider>
  );
}
```

### Offline Mutation Queue

When the device is offline and the user records a payment:

1. TanStack Query's `mutationFn` pauses (doesn't execute the fetch).
2. The mutation is stored in the `MutationCache` with its variables.
3. The persister saves the MutationCache to AsyncStorage.
4. When connectivity resumes, TanStack Query replays paused mutations **in order**.
5. The idempotency key (generated at mutation creation time, not execution time) ensures no duplicates.

```typescript
// packages/shared/src/api/mutations/contributions.ts
// The idempotency key must be generated when the user initiates the action,
// NOT when the mutation is sent to the server. This way, retries (both automatic
// and from the offline queue) use the same key.

export function useRecordContributionPayment() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationKey: ['record-payment'], // Required for persistence
    mutationFn: async (params: RecordPaymentParams & { idempotencyKey: string }) => {
      const { data, error } = await api.contributions.POST(
        '/api/contributions/funds/{fundId}/dues/{dueId}/payments',
        {
          params: { path: { fundId: params.fundId, dueId: params.dueId } },
          body: { amount: params.amount.toString() },
          headers: { 'Idempotency-Key': params.idempotencyKey },
        }
      );
      if (error) throw error;
      return data;
    },
    // ... onMutate, onError, onSettled as in Section 3
  });
}

// In the UI component:
function PayContributionButton({ fundId, dueId, amount }: Props) {
  const recordPayment = useRecordContributionPayment();
  
  const handlePress = () => {
    // Generate idempotency key NOW, not in mutationFn
    const idempotencyKey = generateIdempotencyKey();
    recordPayment.mutate({ fundId, dueId, amount, idempotencyKey });
  };
  
  return (
    <Button 
      onPress={handlePress} 
      loading={recordPayment.isPending}
      title={recordPayment.isPaused ? "Queued (offline)" : "Record Payment"} 
    />
  );
}
```

### Offline UI Indicators

```typescript
// apps/mobile/src/hooks/useNetworkStatus.ts
import { useOnlineManager } from '@tanstack/react-query';
import NetInfo from '@react-native-community/netinfo';
import { useEffect, useState } from 'react';

export function useNetworkStatus() {
  const [isOffline, setIsOffline] = useState(false);

  useEffect(() => {
    return NetInfo.addEventListener((state) => {
      setIsOffline(!state.isConnected);
    });
  }, []);

  return { isOffline };
}

// In the app shell:
function AppShell() {
  const { isOffline } = useNetworkStatus();
  const pendingMutations = useMutationState({
    filters: { status: 'pending' },
    select: (m) => m.state.variables,
  });
  
  return (
    <>
      {isOffline && (
        <OfflineBanner 
          pendingCount={pendingMutations.length} 
          message={`Offline — ${pendingMutations.length} pending ${pendingMutations.length === 1 ? 'action' : 'actions'}`}
        />
      )}
      <AppNavigator />
    </>
  );
}
```

### Conflict Resolution (Optimistic Locking)

Per spec clarification: "Optimistic locking — first write wins; second gets a conflict error and must refresh & retry."

When an offline mutation syncs and hits a 409 Conflict:

```typescript
// apps/mobile/src/api/mutation-error-handler.ts

// Global mutation cache listener for handling sync conflicts
queryClient.getMutationCache().subscribe((event) => {
  if (event.type === 'updated' && event.mutation.state.status === 'error') {
    const error = event.mutation.state.error as any;
    
    if (error?.status === 409) {
      // Concurrency conflict on a previously queued offline mutation
      // Strategy: notify user, invalidate the affected cache, do NOT auto-retry
      // (the due's version has changed — user must review current state)
      
      const variables = event.mutation.state.variables as any;
      
      showConflictNotification({
        title: 'Payment conflict',
        message: 'This payment was already recorded by another user while you were offline. Please review the current status.',
        entityId: variables?.dueId,
      });
      
      // Refresh the stale cache
      queryClient.invalidateQueries({
        queryKey: contributionKeys.lists(variables?.fundId),
      });
    }
  }
});
```

### Why Not WatermelonDB or MMKV

| Option | Pros | Cons | Verdict |
|--------|------|------|---------|
| **TanStack Query cache + AsyncStorage** | Minimal new dependencies; built-in mutation queue; idempotency keys handle dedup; the server is source of truth per spec | Limited offline read depth (only previously fetched data) | **Selected for MVP** |
| **WatermelonDB** | Full local SQLite database; sync engine; offline-first reads with rich queries | Massive added complexity; requires defining a full local schema mirroring backend; sync protocol must be built; overkill for "queue a few writes" scope | Rejected for MVP |
| **MMKV** | Very fast key-value store; encrypted; synchronous reads | No query capability; doesn't solve the sync/queue problem; AsyncStorage is sufficient for persisting the TanStack Query cache | Rejected — no clear advantage over AsyncStorage for this use case |
| **Custom offline queue (AsyncStorage + manual)** | Full control | Reinvents what TanStack Query's offline persistence already provides | Rejected |

### Post-MVP Offline Enhancement Path

If offline requirements grow (e.g., full offline ledger browsing, offline loan request with local validation):

1. **Phase 1 (MVP)**: TanStack Query persistence + mutation queue — current design.
2. **Phase 2**: Add MMKV for faster cache hydration (replace AsyncStorage persister with MMKV persister — TanStack Query supports custom persisters).
3. **Phase 3**: If truly offline-first usage becomes a requirement, evaluate WatermelonDB with a proper sync protocol. This is a significant architectural change.

### Risks & Gotchas

- **AsyncStorage size limits**: AsyncStorage has a default 6MB limit on Android. The query cache for a fund with 1,000 members and 12 months of data could approach this. Mitigate by limiting persisted queries (only the current fund's data, not all funds) and setting `maxAge` on the persister.
- **Stale optimistic UI**: If a payment is queued offline and the user keeps browsing, the optimistic update persists in the UI. When they reconnect and the mutation succeeds, the real data replaces the optimistic data. But if it fails (409 conflict), the optimistic data rolls back — which could be confusing if hours have passed. Solution: show "pending sync" badges on optimistically updated items.
- **Mutation ordering**: TanStack Query replays queued mutations in FIFO order. This is correct for our use case (payments are independent). But if a user records a partial payment and then a second partial payment offline, both must succeed in order. The idempotency key ensures the first isn't duplicated, and the second uses the updated state from the first (if the first succeeds). If the first fails, the second will also fail (version conflict) — the user must manually reconcile.
- **Battery / background sync**: TanStack Query's mutation replay only happens when the app is in the foreground. If the user queues a payment and closes the app, the mutation syncs next time the app opens. Consider adding a background task (Expo's `expo-background-fetch`) for critical payment syncs, but this adds complexity.

---

## 6. Form Validation

### Decision

**Zod** for all validation schemas, shared between web and mobile via the `@fundmanager/shared` package. Zod schemas serve triple duty: runtime validation, TypeScript type inference, and form integration (via `@hookform/resolvers/zod` on web and mobile).

### Zod vs Yup

| Criterion | Zod | Yup | Verdict |
|-----------|-----|-----|---------|
| **TypeScript integration** | First-class — `z.infer<typeof schema>` generates perfect types | Types are inferred but less precise; requires `InferType<typeof schema>` | Zod wins |
| **Bundle size** | ~13KB minified | ~12KB minified | Tie |
| **API design** | Immutable, chainable, composable; `.transform()` and `.refine()` for custom logic | Mutable builder pattern; `.test()` for custom logic | Zod wins (more idiomatic TS) |
| **Ecosystem** | `@hookform/resolvers/zod`, `trpc-zod`, `zod-to-openapi` | `@hookform/resolvers/yup`, long-established | Tie |
| **Custom types** | `z.string().transform()` for custom parsing (e.g., string → decimal) | `.transform()` available but less type-safe | Zod wins |
| **Error messages** | Customizable per-rule, `.superRefine()` for complex cross-field validation | Customizable, `.test()` for complex validation | Tie |
| **React Native compatibility** | Excellent — pure JS | Excellent — pure JS | Tie |
| **Learning curve** | Low if you know TypeScript | Low | Tie |

**Verdict**: Zod is the better choice for a TypeScript-first project. Its type inference is tighter, its immutable API is safer for shared schemas, and its `.transform()` pipeline handles decimal input parsing naturally.

### Shared Validation Schemas

```typescript
// packages/shared/src/validation/contribution.ts
import { z } from 'zod';
import Big from 'big.js';

/**
 * Parse a string or number input into a validated INR amount.
 * Handles: "1,000.50" → 1000.50, "1000" → 1000.00
 * Per spec: All monetary values rounded to 2 decimal places (paisa precision).
 */
export const inrAmountSchema = z
  .union([z.string(), z.number()])
  .transform((val) => {
    // Remove commas (Indian number format: "1,00,000.50")
    const cleaned = typeof val === 'string' ? val.replace(/,/g, '') : String(val);
    try {
      return new Big(cleaned);
    } catch {
      return null;
    }
  })
  .refine((val): val is Big => val !== null, { message: 'Invalid amount' })
  .refine((val) => val.gt(0), { message: 'Amount must be greater than 0' })
  .refine(
    (val) => val.round(2).eq(val), 
    { message: 'Amount cannot have more than 2 decimal places' }
  );

/**
 * Record a contribution payment.
 * Used by both web and mobile payment forms.
 */
export const recordContributionPaymentSchema = z.object({
  amount: inrAmountSchema,
  notes: z.string().max(500).optional(),
});

export type RecordContributionPayment = z.infer<typeof recordContributionPaymentSchema>;

/**
 * Loan request form validation.
 * Per spec FR-041: principal, start month, optional purpose.
 */
export const loanRequestSchema = z.object({
  principal: inrAmountSchema.refine(
    (val) => val.gte(1000),
    { message: 'Minimum loan amount is ₹1,000' }
  ),
  startMonth: z.string().regex(/^\d{4}-(0[1-9]|1[0-2])$/, {
    message: 'Start month must be in YYYY-MM format',
  }),
  purpose: z.string().max(500).optional(),
});

export type LoanRequest = z.infer<typeof loanRequestSchema>;

/**
 * Member joining a fund — contribution amount validation.
 * Per spec FR-022: amount >= fund minimum.
 */
export const joinFundSchema = (minContribution: number) =>
  z.object({
    monthlyContribution: inrAmountSchema.refine(
      (val) => val.gte(minContribution),
      { message: `Monthly contribution must be at least ₹${minContribution.toLocaleString('en-IN')}` }
    ),
  });
```

### Decimal / Currency Input Handling

The core challenge: user types "50000", "50,000", or "50,000.00" into an input. This must be parsed accurately into a `Big.js` instance (not JavaScript `Number`) for validation and display.

```typescript
// packages/shared/src/utils/money.ts
import Big from 'big.js';

// Configure Big.js for INR: 2 decimal places, round half to even (banker's)
Big.RM = Big.roundHalfEven; // 2 = ROUND_HALF_EVEN
Big.DP = 2;

/**
 * Format a Big.js or number value as INR.
 * e.g., 50000 → "₹50,000.00", 1000.5 → "₹1,000.50"
 */
export function formatINR(value: Big | number | string): string {
  const num = typeof value === 'string' ? new Big(value) : 
              typeof value === 'number' ? new Big(value) : value;
  
  // Indian number format: 1,00,00,000.00
  const formatter = new Intl.NumberFormat('en-IN', {
    style: 'currency',
    currency: 'INR',
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  });
  
  return formatter.format(Number(num.toFixed(2)));
}

/**
 * Parse user input to Big.js. Returns null if invalid.
 * Removes commas, validates structure, rejects NaN.
 */
export function parseINRInput(input: string): Big | null {
  const cleaned = input.replace(/[₹,\s]/g, '').trim();
  if (cleaned === '' || cleaned === '.') return null;
  
  try {
    const value = new Big(cleaned);
    if (value.lt(0)) return null;
    // Ensure max 2 decimal places
    if (value.round(2).minus(value).abs().gt(0)) return null;
    return value;
  } catch {
    return null;
  }
}
```

### Form Library Integration

#### Web: React Hook Form + Zod

```typescript
// apps/web/src/components/RecordPaymentForm.tsx
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { recordContributionPaymentSchema, type RecordContributionPayment } from '@fundmanager/shared';

function RecordPaymentForm({ dueId, fundId, amountDue }: Props) {
  const { register, handleSubmit, formState: { errors } } = useForm<RecordContributionPayment>({
    resolver: zodResolver(recordContributionPaymentSchema),
    defaultValues: { amount: amountDue },
  });

  const recordPayment = useRecordContributionPayment();

  const onSubmit = (data: RecordContributionPayment) => {
    recordPayment.mutate({
      fundId,
      dueId,
      amount: Number(data.amount.toFixed(2)),
      idempotencyKey: generateIdempotencyKey(),
    });
  };

  return (
    <form onSubmit={handleSubmit(onSubmit)}>
      <CurrencyInput {...register('amount')} error={errors.amount?.message} />
      <textarea {...register('notes')} />
      <button type="submit" disabled={recordPayment.isPending}>Record Payment</button>
    </form>
  );
}
```

#### Mobile: React Hook Form + Zod (identical resolver)

```typescript
// apps/mobile/src/screens/RecordPaymentScreen.tsx
import { useForm, Controller } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { recordContributionPaymentSchema } from '@fundmanager/shared';
import { TextInput } from 'react-native';

function RecordPaymentScreen({ route }) {
  const { fundId, dueId, amountDue } = route.params;
  const { control, handleSubmit, formState: { errors } } = useForm({
    resolver: zodResolver(recordContributionPaymentSchema),
    defaultValues: { amount: String(amountDue) },
  });

  return (
    <View>
      <Controller
        control={control}
        name="amount"
        render={({ field: { onChange, value } }) => (
          <CurrencyInput 
            value={value} 
            onChangeText={onChange} 
            keyboardType="decimal-pad"
            error={errors.amount?.message}
          />
        )}
      />
      <Button title="Record Payment" onPress={handleSubmit(onSubmit)} />
    </View>
  );
}
```

### Risks & Gotchas

- **Big.js vs server decimal**: The backend sends monetary values as JSON strings (per backend research Section 6, risk: "JavaScript `Number` is a 64-bit float"). The client must parse these with `new Big(value)`, never `Number(value)` or `parseFloat(value)`. Enforce this with a shared `parseMoneyFromApi(value: string): Big` utility.
- **Zod `.transform()` and form libraries**: When Zod transforms the input (string → Big), React Hook Form receives the transformed type. This works correctly with `zodResolver` but the form's internal value type differs from the input type. Use `.input` and `.output` type modifiers if TypeScript complaints arise.
- **Indian number format input**: Users may type "1,00,000" (Indian lakhs format) or "100000". The `inrAmountSchema` handles both by stripping commas before parsing. But a `CurrencyInput` component that formats as-you-type (adding commas) must be built for both web and mobile — this is platform-specific UI.
- **Server-side validation is authoritative**: Per NFR-007: "All financial calculations MUST be performed server-side; client inputs are treated as untrusted." Client Zod validation is for UX only. The server re-validates via FluentValidation with identical rules. Keep rules in sync manually (or generate from OpenAPI validation constraints if the backend publishes them).

---

## 7. PDF/CSV Export

### Decision

**Server-side generation** for all financial reports (settlement, contribution summary, loan portfolio). The frontend requests a report via API, the server generates the PDF/CSV, and returns a download URL. Client-side generation is used only for simple, non-authoritative exports (e.g., exporting a visible table to CSV).

### Why Server-Side for Financial Reports

| Factor | Server-Side | Client-Side | Verdict |
|--------|-------------|-------------|---------|
| **Data accuracy** | Server has authoritative data; no risk of stale/partial client cache | Client may have stale or paginated data — could produce incorrect reports | Server wins (critical for financial) |
| **Complex formatting** | QuestPDF (.NET) for pixel-perfect PDF with headers, tables, signatures | Limited — `jspdf` + `html2canvas` produce mediocre PDFs | Server wins |
| **Performance (large datasets)** | Server handles 1,000 member settlement report without client memory issues | Browser/RN may struggle with large datasets in memory | Server wins |
| **Consistency** | Report generated from the same service that owns the data — single source of truth | Client must aggregate data from multiple cached queries | Server wins |
| **Cacheability** | Generated report can be stored as a static file and served via CDN | Generated fresh each time | Server wins |
| **Offline availability** | Previously generated reports can be downloaded from a URL | Client-side generation could work offline for cached data | Tie |

### Server-Side Report Generation (Backend Implementation)

The backend services use:
- **QuestPDF** (or iText7 Community) for PDF generation — .NET library, produces high-quality PDFs with tables, headers, footers, and INR formatting.
- **CsvHelper** for CSV generation — standard .NET CSV library.

The frontend workflow:

```
1. User clicks "Export PDF" or "Export CSV" on a report screen.
2. Frontend calls POST /api/{service}/funds/{fundId}/reports/{reportType}
   Body: { format: "pdf" | "csv", dateRange: { from, to } }
3. Server generates the report asynchronously (for large reports) or synchronously (for small reports).
4. Server returns { reportId, downloadUrl, status: "ready" | "generating" }
5. If "generating": frontend polls GET /api/reports/{reportId}/status until ready.
6. Frontend opens downloadUrl in a new tab (web) or triggers a Share sheet (mobile).
```

```typescript
// packages/shared/src/api/mutations/reports.ts
export function useGenerateReport() {
  return useMutation({
    mutationFn: async (params: {
      fundId: string;
      reportType: 'contribution-summary' | 'loan-portfolio' | 'settlement' | 'personal-statement';
      format: 'pdf' | 'csv';
      dateRange?: { from: string; to: string };
    }) => {
      const { data, error } = await api.reports.POST(
        '/api/reports/funds/{fundId}/generate',
        {
          params: { path: { fundId: params.fundId } },
          body: {
            reportType: params.reportType,
            format: params.format,
            dateRange: params.dateRange,
          },
        }
      );
      if (error) throw error;
      return data; // { reportId, downloadUrl, status }
    },
  });
}
```

### Client-Side CSV Export (Simple Tables)

For non-authoritative, quick exports of visible table data (e.g., the user exports the currently displayed contribution list):

```typescript
// packages/shared/src/utils/csv-export.ts

/**
 * Export an array of objects to CSV and trigger download.
 * Used for quick exports of visible table data only — NOT for financial reports.
 */
export function generateCSV<T extends Record<string, any>>(
  data: T[],
  columns: { key: keyof T; header: string; format?: (val: any) => string }[],
): string {
  const header = columns.map((c) => `"${c.header}"`).join(',');
  const rows = data.map((row) =>
    columns
      .map((c) => {
        const val = c.format ? c.format(row[c.key]) : String(row[c.key] ?? '');
        return `"${val.replace(/"/g, '""')}"`;
      })
      .join(',')
  );
  return [header, ...rows].join('\n');
}
```

#### Web CSV Download

```typescript
// apps/web/src/utils/download.ts
export function downloadCSV(csv: string, filename: string) {
  const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  link.click();
  URL.revokeObjectURL(url);
}
```

#### Mobile CSV/PDF Share

```typescript
// apps/mobile/src/utils/share-file.ts
import * as Sharing from 'expo-sharing';
import * as FileSystem from 'expo-file-system';

export async function shareFile(url: string, filename: string) {
  // Download the server-generated file
  const localUri = `${FileSystem.documentDirectory}${filename}`;
  await FileSystem.downloadAsync(url, localUri);
  
  // Open share sheet
  if (await Sharing.isAvailableAsync()) {
    await Sharing.shareAsync(localUri);
  }
}
```

### React Native PDF Viewing

For viewing PDFs in-app on mobile (without downloading):

- **expo-web-browser**: Opens the PDF URL in an in-app browser (simplest).
- **react-native-pdf**: Renders PDF inline in a view (better UX but adds native dependency).

**Decision for MVP**: Use `expo-web-browser` to open the PDF download URL. Add inline PDF rendering post-MVP if user feedback demands it.

### Settlement Report (Special Case)

The dissolution settlement report (spec FR-085, FR-086) is the most complex report:
- Per-member breakdown: contributions paid, interest share, outstanding deductions, net payout.
- Must include calculation basis (formula inputs) for transparency.
- Must be generated server-side by the Dissolution service (it owns the data).
- Both PDF and CSV formats required.

The frontend simply triggers generation and displays the report:

```typescript
// apps/web/src/pages/DissolutionWizard/SettlementReport.tsx
function SettlementReport({ fundId }: { fundId: string }) {
  const generateReport = useGenerateReport();
  
  return (
    <div>
      <h2>Settlement Report</h2>
      <SettlementTable fundId={fundId} /> {/* Renders data from API */}
      <div>
        <button onClick={() => generateReport.mutate({ 
          fundId, reportType: 'settlement', format: 'pdf' 
        })}>
          Export PDF
        </button>
        <button onClick={() => generateReport.mutate({ 
          fundId, reportType: 'settlement', format: 'csv' 
        })}>
          Export CSV
        </button>
      </div>
    </div>
  );
}
```

### Alternatives Considered

| Alternative | Why Rejected |
|-------------|-------------|
| **Client-side PDF (jsPDF + html2canvas)** | Produces screenshot-quality PDFs, not structured document PDFs. Tables with 1,000 rows are slow to render client-side. Financial reports need precise formatting and are authoritative documents — server generation is correct. |
| **Client-side PDF (react-pdf/renderer)** | `@react-pdf/renderer` can generate structured PDFs in the browser. Decent quality, but: (a) adds ~500KB to bundle, (b) limited table support, (c) financial data may be stale, (d) doesn't work in React Native. |
| **react-native-html-to-pdf** | Generates PDF from HTML string on mobile. Quality is acceptable but the approach is fragile (HTML → PDF conversion varies by device). Server-side is more reliable. |
| **Client-side Excel (xlsx / SheetJS)** | Could generate .xlsx files client-side. Rejected because: (a) significant bundle size (~300KB), (b) CSV is sufficient per spec, (c) Excel-specific features not needed. |

### Risks & Gotchas

- **Report generation time**: Settlement reports for large funds may take 10–30 seconds (spec NFR-023: "under 30 seconds"). Use async generation with polling for large reports. Show a progress indicator.
- **Report caching**: Once generated, a settlement report should be cached server-side (it's for a specific fund state). The same download URL can be shared among fund members.
- **PDF accessibility**: Server-generated PDFs should be tagged for accessibility (PDF/UA). QuestPDF has limited accessibility support — consider iText7 if this becomes a requirement.
- **Mobile file permissions**: On iOS, files are shared via the Share sheet. On Android, you may need `WRITE_EXTERNAL_STORAGE` permission for older Android versions. Expo's `Sharing` API handles this transparently.

---

## 8. Push Notifications

### Decision

**Expo Notifications** for React Native push notifications. Register device push tokens on the server. The backend's Notification microservice sends push notifications via Expo Push Service (EAS) for mobile and web push (via Web Push API / Firebase Cloud Messaging) for browser notifications.

### Architecture

```
┌────────────────┐     ┌──────────────────────┐     ┌──────────────────┐
│  React Native   │     │  Notification Service │     │  Expo Push       │
│  (Expo)         │     │  (.NET 8)             │     │  Service (EAS)   │
└────────┬───────┘     └──────────┬───────────┘     └────────┬─────────┘
         │                        │                          │
         │ 1. Register push token │                          │
         │  (on app start)        │                          │
         ├───────────────────────►│ Store token in DB        │
         │                        │                          │
         │                        │ 2. Domain event triggers │
         │                        │    notification          │
         │                        │                          │
         │                        │ 3. Send push via         │
         │                        │    Expo Push API         │
         │                        ├─────────────────────────►│
         │                        │                          │ 4. Deliver to
         │                        │                          │    APNs / FCM
         │◄───────────────────────┼──────────────────────────┤
         │  Push notification     │                          │
         │  arrives on device     │                          │
```

### Expo Notifications Setup

```typescript
// apps/mobile/src/services/push-notifications.ts
import * as Notifications from 'expo-notifications';
import * as Device from 'expo-device';
import { Platform } from 'react-native';

// Configure notification behavior
Notifications.setNotificationHandler({
  handleNotification: async () => ({
    shouldShowAlert: true,
    shouldPlaySound: true,
    shouldSetBadge: true,
  }),
});

/**
 * Register for push notifications and return the Expo push token.
 * Must be called on app start (after login).
 */
export async function registerForPushNotifications(): Promise<string | null> {
  if (!Device.isDevice) {
    console.log('Push notifications require a physical device');
    return null;
  }

  // Check existing permissions
  const { status: existingStatus } = await Notifications.getPermissionsAsync();
  let finalStatus = existingStatus;

  if (existingStatus !== 'granted') {
    const { status } = await Notifications.requestPermissionsAsync();
    finalStatus = status;
  }

  if (finalStatus !== 'granted') {
    return null; // User denied — respect their choice
  }

  // Android: create notification channel
  if (Platform.OS === 'android') {
    await Notifications.setNotificationChannelAsync('payments', {
      name: 'Payment Notifications',
      importance: Notifications.AndroidImportance.HIGH,
      vibrationPattern: [0, 250, 250, 250],
      lightColor: '#FF6B35',
    });
    
    await Notifications.setNotificationChannelAsync('loans', {
      name: 'Loan Notifications',
      importance: Notifications.AndroidImportance.HIGH,
    });
    
    await Notifications.setNotificationChannelAsync('reminders', {
      name: 'Reminders',
      importance: Notifications.AndroidImportance.DEFAULT,
    });
  }

  // Get Expo push token
  const token = await Notifications.getExpoPushTokenAsync({
    projectId: process.env.EXPO_PUBLIC_PROJECT_ID,
  });

  return token.data; // "ExponentPushToken[xxxxxxxxxxxxx]"
}

/**
 * Send the push token to the backend for storage.
 */
export async function registerPushTokenOnServer(pushToken: string) {
  await api.notifications.POST('/api/notifications/devices', {
    body: {
      token: pushToken,
      platform: Platform.OS, // 'ios' | 'android'
      deviceName: Device.modelName,
    },
  });
}
```

### Notification Handling in the App

```typescript
// apps/mobile/src/hooks/useNotificationListeners.ts
import * as Notifications from 'expo-notifications';
import { useNavigation } from '@react-navigation/native';
import { useEffect, useRef } from 'react';

export function useNotificationListeners() {
  const navigation = useNavigation();
  const notificationListener = useRef<Notifications.Subscription>();
  const responseListener = useRef<Notifications.Subscription>();

  useEffect(() => {
    // Notification received while app is in foreground
    notificationListener.current = Notifications.addNotificationReceivedListener(
      (notification) => {
        // Update badge count, show in-app indicator
        const data = notification.request.content.data;
        // Could invalidate relevant TanStack Query cache here
        if (data.type === 'contribution_payment_received') {
          queryClient.invalidateQueries({
            queryKey: contributionKeys.lists(data.fundId),
          });
        }
      }
    );

    // User tapped on a notification
    responseListener.current = Notifications.addNotificationResponseReceivedListener(
      (response) => {
        const data = response.notification.request.content.data;
        
        // Deep-link to the relevant screen
        switch (data.type) {
          case 'contribution_due':
            navigation.navigate('MyContributions', { fundId: data.fundId });
            break;
          case 'loan_approved':
          case 'loan_rejected':
            navigation.navigate('LoanDetail', { fundId: data.fundId, loanId: data.loanId });
            break;
          case 'voting_session_opened':
            navigation.navigate('VotingScreen', { fundId: data.fundId, sessionId: data.sessionId });
            break;
          case 'repayment_due':
            navigation.navigate('LoanDetail', { fundId: data.fundId, loanId: data.loanId });
            break;
          default:
            navigation.navigate('Notifications');
        }
      }
    );

    return () => {
      if (notificationListener.current) {
        Notifications.removeNotificationSubscription(notificationListener.current);
      }
      if (responseListener.current) {
        Notifications.removeNotificationSubscription(responseListener.current);
      }
    };
  }, [navigation]);
}
```

### Notification Preferences UI

Per spec FR-102: "Users MUST be able to configure notification preferences per channel."

```typescript
// Shared preferences type
// packages/shared/src/types/notification-preferences.ts
export interface NotificationPreferences {
  channels: {
    push: boolean;
    email: boolean;
    sms: boolean;          // SMS is for critical events only (spec FR-101)
    inApp: boolean;        // In-app is always on (cannot disable)
  };
  events: {
    contributionDue: { push: boolean; email: boolean };
    paymentReceived: { push: boolean; email: boolean };
    contributionOverdue: { push: boolean; email: boolean; sms: boolean };
    loanStatusChange: { push: boolean; email: boolean };
    votingInvitation: { push: boolean; email: boolean };
    repaymentDue: { push: boolean; email: boolean };
    repaymentOverdue: { push: boolean; email: boolean; sms: boolean };
    dissolutionUpdate: { push: boolean; email: boolean };
  };
}
```

```typescript
// apps/mobile/src/screens/NotificationPreferencesScreen.tsx
function NotificationPreferencesScreen() {
  const { data: prefs, isLoading } = useNotificationPreferences();
  const updatePrefs = useUpdateNotificationPreferences();

  return (
    <ScrollView>
      <SectionHeader title="Channels" />
      <SwitchRow
        label="Push Notifications"
        value={prefs?.channels.push}
        onToggle={(v) => updatePrefs.mutate({ channels: { ...prefs!.channels, push: v } })}
      />
      <SwitchRow
        label="Email"
        value={prefs?.channels.email}
        onToggle={(v) => updatePrefs.mutate({ channels: { ...prefs!.channels, email: v } })}
      />
      <SwitchRow
        label="SMS (critical alerts only)"
        value={prefs?.channels.sms}
        onToggle={(v) => updatePrefs.mutate({ channels: { ...prefs!.channels, sms: v } })}
        description="Used for overdue payments and loan disbursements only"
      />

      <SectionHeader title="Event Types" />
      {/* Per-event toggles grouped by category */}
      <EventPreferenceRow event="contributionDue" label="Contribution due reminders" prefs={prefs} />
      <EventPreferenceRow event="paymentReceived" label="Payment receipts" prefs={prefs} />
      <EventPreferenceRow event="loanStatusChange" label="Loan status updates" prefs={prefs} />
      {/* ... etc */}
    </ScrollView>
  );
}
```

### Backend Notification Dispatch (Expo Push API)

The .NET Notification microservice uses the Expo Push API to send notifications:

```
POST https://exp.host/--/api/v2/push/send
Content-Type: application/json

{
  "to": "ExponentPushToken[xxxxxx]",
  "title": "Contribution Due",
  "body": "Your ₹1,000 contribution for January 2026 is due. Tap to pay.",
  "data": {
    "type": "contribution_due",
    "fundId": "uuid",
    "dueId": "uuid"
  },
  "channelId": "payments",          // Android channel
  "sound": "default",
  "badge": 1,
  "priority": "high"
}
```

Per spec FR-104, the Notification service retries 3 times with exponential backoff on push delivery failure, then falls back to email, then in-app.

### Alternatives Considered

| Alternative | Why Rejected |
|-------------|-------------|
| **Firebase Cloud Messaging (FCM) directly** | Requires Firebase setup, separate APNs configuration for iOS, and a Firebase project. Expo Notifications abstracts FCM (Android) and APNs (iOS) behind a single API. Since we're using Expo managed workflow, Expo Push is the natural choice. |
| **OneSignal** | Third-party push service. Adds a vendor dependency and cost. Expo Push is free (included with EAS) and sufficient for our scale. Consider if advanced analytics (delivery rates, A/B testing) are needed post-MVP. |
| **Custom APNs + FCM integration** | Maximum control but significant setup: separate Apple Developer certificates, FCM service account, message formatting per platform. Overkill when Expo abstracts this entirely. |
| **Expo Notifications for web** | Expo Notifications has experimental web support. For the web app, we use the standard Web Push API (via the browser's `PushManager`) or skip web push for MVP (in-app notifications are always available). |

### Web Push Notifications (Optional for MVP)

For the web app, push notifications are a nice-to-have:

- **In-app notifications**: Always available. A notification bell icon in the header with an unread count, backed by polling or WebSocket (SSE from the Notification service).
- **Browser push**: Optional enhancement using the Web Push API + a service worker. Adds complexity (VAPID keys, service worker registration). Defer to post-MVP.

**Decision for MVP**: Web uses in-app notifications only (polling `GET /api/notifications/unread` every 60 seconds). Browser push notifications are a post-MVP enhancement.

### Risks & Gotchas

- **Expo Push Token rotation**: Push tokens can change (e.g., app reinstall, OS update). Re-register the token on every app start, not just on first install. The backend should handle token updates (upsert by device + user).
- **Notification permissions**: iOS requires explicit user permission. If denied, only in-app notifications work. Show a one-time explanatory screen before requesting permissions.
- **Background notifications**: Expo supports background notification handling (`expo-task-manager`), but for MVP, we only handle notifications when the app is running or opened via notification tap.
- **Rate limits on Expo Push**: Expo Push Service has rate limits (600 notifications/second for free tier). Sufficient for 100 funds × 1,000 members = 100,000 members total. For batch notifications (monthly cycle → 100K notifications), implement server-side batching with rate limiting.
- **Notification data payload**: Keep the `data` field small (< 4KB for APNs). Include only IDs (fundId, dueId, loanId) and type. Fetch full details from the API when the user taps the notification.

---

## 9. Summary of Decisions

| Area | Decision | Key Technology | Rationale |
|------|----------|---------------|-----------|
| **Code sharing** | pnpm workspaces + Turborepo monorepo; `@fundmanager/shared` for types, validation, API client, stores | pnpm, Turborepo, openapi-typescript, openapi-fetch | Lightweight setup, fast builds, type-safe API layer from OpenAPI contracts |
| **State management** | Zustand for client state (auth, fund context, UI); TanStack Query for all server state | Zustand 4.x, TanStack Query v5 | Minimal boilerplate, no server state duplication, clean separation; RTK overkill for 3 client slices |
| **API client** | TanStack Query with query key factories, optimistic mutations, idempotency keys, offline queue | TanStack Query v5, openapi-fetch, uuid | Mature caching, dedup, pagination, offline support; query keys enable surgical cache invalidation |
| **Authentication** | OTP-based; httpOnly cookies (web), SecureStore (mobile); proactive token refresh; shared auth Zustand store | Expo SecureStore, httpOnly cookies, React Router guards, React Navigation auth flow | httpOnly cookies prevent XSS token theft on web; SecureStore provides hardware-backed encryption on mobile |
| **Offline support** | TanStack Query mutation queue + AsyncStorage persistence; idempotency keys for dedup; 409 conflict handling | TanStack Query persist, AsyncStorage, NetInfo | Minimal new dependencies; covers MVP scope (payment/repayment queueing); upgradeable to MMKV/WatermelonDB later |
| **Form validation** | Zod schemas shared between web and mobile; React Hook Form with zodResolver; Big.js for decimal handling | Zod 3.x, React Hook Form, @hookform/resolvers, big.js | Type-safe schemas, shared validation rules, accurate decimal parsing for INR |
| **PDF/CSV export** | Server-side generation for financial reports (QuestPDF, CsvHelper); client-side CSV for quick table exports only | Server: QuestPDF + CsvHelper; Client: Blob API (web), expo-sharing (mobile) | Financial reports need authoritative server data; client-side for convenience only |
| **Push notifications** | Expo Notifications for mobile; in-app notifications for web (MVP); Expo Push Service for delivery | Expo Notifications, expo-device, Expo Push API | Unified API for iOS + Android; free with EAS; web push deferred to post-MVP |

---

## Open Questions for Design Phase

1. **Shared package build strategy**: Should `@fundmanager/shared` be consumed as raw TypeScript source (transpiled by Vite/Metro) or pre-built to JavaScript? Raw TS is simpler but requires both build tools to handle the same TS config. Pre-built adds a build step but ensures consistency.
2. **Currency input component**: Should the INR currency input component (with as-you-type formatting) be a shared component with platform-specific renderers, or two entirely separate implementations? The formatting logic can be shared; the input element (`<input>` vs `<TextInput>`) cannot.
3. **Web push notifications timeline**: Should browser push be included in MVP, or is polling for in-app notifications sufficient? Polling every 60s means a worst-case 60s delay for web notification visibility.
4. **React Native minimum versions**: Expo SDK 52+ supports React Native 0.76+. Verify all dependencies (including TanStack Query async storage persister) are compatible.
5. **big.js vs decimal.js**: big.js is 6KB, decimal.js is 32KB. big.js provides arbitrary-precision arithmetic sufficient for INR handling. decimal.js adds trigonometric functions we don't need. Confirm big.js covers all financial math requirements (it does for addition, subtraction, multiplication, division, and rounding).
