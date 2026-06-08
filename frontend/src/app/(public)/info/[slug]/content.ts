// Content registry for the public /info/[slug] pages linked from the landing-page
// footer. Each entry is plain data (no JSX) so it can be imported by the server
// component in page.tsx. Slugs here are the single source of truth for which
// /info/* routes exist — generateStaticParams + the footer links both derive from them.

export type InfoSection = {
  heading?: string;
  body?: string[];
  bullets?: string[];
};

export type InfoContent = {
  eyebrow: string;
  title: string;
  lede: string;
  note?: string;
  sections: InfoSection[];
};

export const INFO_PAGES: Record<string, InfoContent> = {
  careers: {
    eyebrow: "Careers",
    title: "Build the software restaurants run on.",
    lede:
      "We are a small team in Prishtina building dineOS for restaurants across Kosovo and the region. If you have worked a service shift or shipped software people rely on daily, we want to talk.",
    sections: [
      {
        heading: "Why dineOS",
        body: [
          "We replace the tangle of disconnected tools a restaurant runs on with one platform for the floor, the line, and the back office. Every decision is reviewed by someone who has worked a real service.",
        ],
      },
      {
        heading: "Open roles",
        bullets: [
          "Frontend Engineer — Next.js / React (Prishtina, hybrid)",
          "Backend Engineer — .NET / PostgreSQL (Prishtina, hybrid)",
          "Product Designer — design system & operator UX (Prishtina, hybrid)",
          "Customer Success — restaurant onboarding (Prishtina)",
        ],
      },
      {
        heading: "How to apply",
        body: [
          "Send a short note about what you have built to careers@dineos.app. Tell us about a problem you solved for people on their feet for ten hours.",
        ],
      },
    ],
  },
  press: {
    eyebrow: "Press",
    title: "Press & media.",
    lede: "Resources for journalists and partners covering dineOS.",
    sections: [
      {
        heading: "About dineOS",
        body: [
          "dineOS is the operating system for the modern restaurant — one platform for order taking, kitchen display, and management reporting. Founded in 2023 in Prishtina, it now runs in restaurants across Kosovo.",
        ],
      },
      {
        heading: "Media kit",
        bullets: [
          "Logo and wordmark (light & dark)",
          "Brand guidelines and color tokens",
          "Product screenshots — Order board, Kitchen display, Manager dashboard",
        ],
      },
      {
        heading: "Media inquiries",
        body: ["For interviews and assets, email press@dineos.app."],
      },
    ],
  },
  "design-system": {
    eyebrow: "Design system",
    title: "The dineOS design system.",
    lede: "The tokens, components, and patterns behind every dineOS surface.",
    sections: [
      {
        heading: "Foundations",
        body: [
          "A single token set drives the floor, the line, and the back office: an ember accent scale, semantic status colors (new, in progress, ready, stalled), a mono type ramp for timers and figures, and a consistent spacing and radius scale.",
        ],
      },
      {
        heading: "Components",
        bullets: [
          "Order cards with severity rails and live timers",
          "Kitchen tickets tuned for glance-ability from the pass",
          "KPI tiles, charts, and shift-note panels",
          "Buttons, modals, and form primitives",
        ],
      },
      {
        heading: "Status",
        body: [
          "The full interactive component gallery is internal today; a public preview is on the way.",
        ],
      },
    ],
  },
  docs: {
    eyebrow: "Documentation",
    title: "dineOS documentation.",
    lede: "Guides for setting up and running your restaurant on dineOS.",
    sections: [
      {
        heading: "Getting started",
        bullets: [
          "Import your menu and organize categories",
          "Add tables and floor sections",
          "Invite staff and assign roles",
          "Take your first order on the Order board",
        ],
      },
      {
        heading: "Guides",
        bullets: [
          "Order board — table-aware order taking and split checks",
          "Kitchen display — station routing, timers, and bump/recall",
          "Manager dashboard — live KPIs, shift notes, and reports",
          "Billing — Stripe-managed subscription and invoices",
        ],
      },
      {
        heading: "Try it first",
        body: [
          "The fastest way to learn dineOS is the live demo — request credentials and explore a fully seeded restaurant.",
        ],
      },
    ],
  },
  changelog: {
    eyebrow: "Changelog",
    title: "What's new in dineOS.",
    lede: "Product updates, shipped from the floor out.",
    sections: [
      {
        heading: "May 2026 — Platform 2.0",
        bullets: [
          "Faster Kitchen Display System with high-contrast tickets",
          "Redesigned Manager dashboard with live KPIs",
          "Staff PIN sessions — switch operators without a full re-login",
        ],
      },
      {
        heading: "April 2026",
        bullets: [
          "Server-side staff-session refresh and revocation",
          "Real loginable PINs for demo staff",
        ],
      },
      {
        heading: "March 2026",
        bullets: [
          "Public demo access flow — emailed credentials, 7-day shared tenant",
          "Simplified pricing: Demo + $50 Pro plan",
        ],
      },
    ],
  },
  api: {
    eyebrow: "API",
    title: "The dineOS API.",
    lede: "A versioned REST API for orders, menu, kitchen, and reporting.",
    sections: [
      {
        heading: "Overview",
        body: [
          "All endpoints are URL-versioned under /api/v1 and authenticated with a Keycloak-issued JWT. Access is scoped by role — Manager, Cashier, KitchenStaff, and SuperAdmin.",
        ],
      },
      {
        heading: "Resources",
        bullets: [
          "Auth — login, refresh, logout, staff sessions",
          "Orders — create, update status, list",
          "Menu — categories and items",
          "Kitchen — realtime ticket stream over SignalR (/hubs/orders)",
          "Reports — labor and prep-time analytics",
        ],
      },
      {
        heading: "Interactive reference",
        body: [
          "A full Swagger reference ships with each deployment at /swagger.",
        ],
      },
    ],
  },
  status: {
    eyebrow: "Status",
    title: "System status.",
    lede: "Live operational status of the dineOS platform.",
    sections: [
      {
        heading: "Current status",
        body: ["All systems operational."],
        bullets: [
          "API — Operational",
          "Order board — Operational",
          "Kitchen display — Operational",
          "Realtime (SignalR) — Operational",
          "Billing (Stripe) — Operational",
        ],
      },
      {
        heading: "Reliability",
        body: ["99.98% uptime over the last 90 days of dinner service."],
      },
    ],
  },
  privacy: {
    eyebrow: "Legal",
    title: "Privacy Policy",
    lede: "How dineOS collects, uses, and protects your data.",
    note: "This is a plain-language summary for the demo and is not legal advice.",
    sections: [
      {
        heading: "Data we collect",
        bullets: [
          "Account details — name, email, restaurant, and role",
          "Operational data — orders, menu, tables, and shifts you create",
          "Usage and diagnostic logs to keep the service reliable",
        ],
      },
      {
        heading: "How we use it",
        body: [
          "We use your data only to provide and improve dineOS — running your restaurant, processing billing, and supporting you. We do not sell personal data.",
        ],
      },
      {
        heading: "Your rights",
        body: [
          "You can request access, correction, or deletion of your personal data at any time. To exercise these rights, email privacy@dineos.app.",
        ],
      },
    ],
  },
  terms: {
    eyebrow: "Legal",
    title: "Terms of Service",
    lede: "The terms that govern your use of dineOS.",
    note: "This is a plain-language summary for the demo and is not legal advice.",
    sections: [
      {
        heading: "Using dineOS",
        body: [
          "dineOS is provided to restaurants to manage orders, kitchen operations, staff, and billing. You are responsible for the accuracy of the data you enter and for your staff's use of their accounts.",
        ],
      },
      {
        heading: "Billing",
        body: [
          "Paid plans are billed monthly through Stripe. You can cancel at any time; access continues until the end of the current billing period.",
        ],
      },
      {
        heading: "Acceptable use",
        body: [
          "Do not misuse the service, attempt to disrupt it, or access data that is not yours. We may suspend accounts that violate these terms.",
        ],
      },
    ],
  },
  security: {
    eyebrow: "Legal",
    title: "Security",
    lede: "How we keep your restaurant's data safe.",
    sections: [
      {
        heading: "How we protect data",
        bullets: [
          "Encryption in transit for all API and realtime traffic",
          "Authentication via Keycloak-issued JWTs with short-lived sessions",
          "Role-based access control enforced on every request",
          "Server-side session revocation for staff and owners",
        ],
      },
      {
        heading: "Reporting a vulnerability",
        body: [
          "If you believe you have found a security issue, please email security@dineos.app. We investigate every report and will keep you updated.",
        ],
      },
    ],
  },
  dpa: {
    eyebrow: "Legal",
    title: "Data Processing Agreement",
    lede: "How dineOS processes data on behalf of your restaurant.",
    note: "This is a plain-language summary for the demo and is not legal advice.",
    sections: [
      {
        heading: "Scope",
        body: [
          "When you use dineOS, you are the data controller for your restaurant's data and dineOS acts as the data processor, handling that data only on your documented instructions.",
        ],
      },
      {
        heading: "Sub-processors",
        bullets: [
          "Keycloak — authentication",
          "Stripe — billing and payments",
          "Cloud hosting and logging providers",
        ],
      },
      {
        heading: "Requests",
        body: [
          "To request a signed DPA or ask about data handling, email privacy@dineos.app.",
        ],
      },
    ],
  },
};

export const infoSlugs = Object.keys(INFO_PAGES);
