import Link from "next/link";

const restaurantLogos = [
  "Olio & Sale",
  "Harbor & Hearth",
  "Kumo Izakaya",
  "La Lecheria",
  "Noor Kitchen",
];

const surfaces = [
  {
    title: "For the floor",
    copy:
      "A cashier-grade order board built for the rush. Kanban columns for every lifecycle stage, tableside modifiers, and stall detection before the guest notices.",
    items: ["Table-aware order taking", "Split checks and tip splitting", "Takeout and delivery channels"],
  },
  {
    title: "For the line",
    copy:
      "A Kitchen Display System glanceable from across the pass. High-contrast tickets, severity rails, and bold mono timers for fast expo decisions.",
    items: ["Station routing and timers", "Amber at 10 min, red at 20", "Bump and recall with one tap"],
  },
  {
    title: "For the back office",
    copy:
      "A Manager dashboard that fits a manager's day: live KPIs, shift notes that carry context between teams, and reports without a BI tool.",
    items: ["Live KPIs and revenue chart", "Shift handoff notes", "Labor and prep-time reports"],
  },
];

const stats = [
  ["80ms", "From tap on the floor to ticket on the line."],
  ["-22%", "Average ticket-to-plate time across 84 restaurants."],
  ["99.98%", "Uptime over the last 90 days of dinner service."],
  ["1 day", "Typical onboarding: menu imported, staff trained."],
] as const;

const values = [
  ["Service speed", "The kitchen does not wait for a loading spinner. Neither does the product."],
  ["Operator-owned", "Every decision reviewed by a working GM before it ships."],
  ["One platform", "No integrations, no data drift, no checking another system."],
  ["Kind by default", "Software for people on their feet for ten hours."],
] as const;

const plans = [
  {
    name: "Demo",
    description: "Drop your email — we'll send credentials to a shared demo restaurant. No card, no commitment.",
    price: "$0",
    suffix: "",
    featured: false,
    cta: "Try the demo",
    href: "/demo",
    features: [
      "Credentials emailed in under a minute",
      "Full Order Board, Kitchen Display, and Manager Dashboard",
      "Sample menu, tables, and staff already loaded",
      "7-day access — re-request any time",
    ],
  },
  {
    name: "Pro",
    description: "Everything you need to run one restaurant on dineOS.",
    price: "$50",
    suffix: "/ month",
    featured: true,
    cta: "Get started",
    href: "/signup",
    features: [
      "One restaurant, unlimited staff accounts",
      "Order Board, Kitchen Display, and Manager Dashboard",
      "Live KPIs, shift notes, and prep-time reports",
      "Stripe-managed billing and invoices",
    ],
  },
];

const navLinks = [
  ["Product", "#product"],
  ["Company", "#story"],
  ["Pricing", "#pricing"],
  ["Customers", "#customers"],
  ["Docs", "#footer"],
] as const;

function Brand() {
  return (
    <Link href="/" className="flex items-center gap-2.5 text-fg">
      <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-gradient-to-br from-ember-500 to-ember-700 text-[13px] font-bold text-white shadow-sm">
        d
      </span>
      <span className="text-sm font-semibold tracking-[-0.01em]">dineOS</span>
    </Link>
  );
}

function CheckIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" className="mt-0.5 h-3.5 w-3.5 shrink-0 text-accent" aria-hidden="true">
      <path d="m5 12 5 5L20 7" stroke="currentColor" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

function SurfaceIcon({ index }: { index: number }) {
  const paths = [
    "M4 2v20l2-1.5L8 22l2-1.5L12 22l2-1.5L16 22l2-1.5L20 22V2l-2 1.5L16 2l-2 1.5L12 2l-2 1.5L8 2 6 3.5Z M8 7h8M8 11h8M8 15h5",
    "M6 13.87A4 4 0 0 1 7.41 6a5.11 5.11 0 0 1 1.05-1.54 5 5 0 0 1 7.08 0A5.11 5.11 0 0 1 16.59 6 4 4 0 0 1 18 13.87V21H6Z M6 17h12",
    "M3 3v18h18 M7 14l4-4 4 4 5-5",
  ];

  return (
    <span className="mb-1 flex h-9 w-9 items-center justify-center rounded-[9px] bg-accent-soft text-accent">
      <svg viewBox="0 0 24 24" fill="none" className="h-5 w-5" aria-hidden="true">
        {paths[index].split(" M").map((path, pathIndex) => (
          <path
            key={path}
            d={pathIndex === 0 ? path : `M${path}`}
            stroke="currentColor"
            strokeWidth="2"
            strokeLinecap="round"
            strokeLinejoin="round"
          />
        ))}
      </svg>
    </span>
  );
}

function OrderCardMock({
  id,
  timer,
  items,
  tone = "default",
}: {
  id: string;
  timer: string;
  items: string;
  tone?: "default" | "amber" | "red";
}) {
  const toneClass =
    tone === "red"
      ? "ring-2 ring-status-stalled-red-solid animate-pulse-red"
      : tone === "amber"
        ? "[&_span:last-child]:bg-status-stalled-amber-solid [&_span:last-child]:text-white"
        : "";

  return (
    <div className={`rounded-[7px] border border-border bg-surface p-2.5 shadow-xs ${toneClass}`}>
      <div className="mb-1.5 flex items-center justify-between text-[10.5px]">
        <span className="font-mono font-medium text-fg-muted">#{id}</span>
        <span className="rounded bg-surface-3 px-1.5 py-0.5 font-mono font-semibold text-fg-muted">{timer}</span>
      </div>
      <p className="text-[10.5px] leading-snug text-fg">{items}</p>
    </div>
  );
}

function HeroMock() {
  return (
    <div className="relative aspect-[4/3]">
      <div className="absolute inset-0 overflow-hidden rounded-[14px] border border-border-strong bg-surface shadow-xl">
        <div className="flex items-center gap-2 border-b border-border bg-bg-elevated px-3.5 py-2.5">
          <span className="h-2.5 w-2.5 rounded-full bg-surface-3" />
          <span className="h-2.5 w-2.5 rounded-full bg-surface-3" />
          <span className="h-2.5 w-2.5 rounded-full bg-surface-3" />
          <span className="ml-1.5 text-[11.5px] font-medium text-fg-muted">Orders · Olio & Sale</span>
          <span className="ml-auto inline-flex items-center gap-1.5 text-[10.5px] font-semibold text-[var(--success)]">
            <span className="h-1.5 w-1.5 animate-ping rounded-full bg-[var(--success)]" />
            LIVE
          </span>
        </div>
        <div className="grid h-[calc(100%-42px)] grid-cols-3 gap-2.5 bg-bg-sunken p-3">
          <div className="flex min-w-0 flex-col gap-2">
            <h4 className="flex items-center gap-1.5 px-1 pb-0.5 pt-1 text-[11px] font-semibold text-fg-muted">
              <span className="h-1.5 w-1.5 rounded-full bg-status-new-solid" />
              New · 2
            </h4>
            <OrderCardMock id="247" timer="00:42" items="1x Smash Burger · 2x Lemonade" />
            <OrderCardMock id="248" timer="00:18" items="2x Cacio e Pepe · 1x Caesar" />
          </div>
          <div className="flex min-w-0 flex-col gap-2">
            <h4 className="flex items-center gap-1.5 px-1 pb-0.5 pt-1 text-[11px] font-semibold text-fg-muted">
              <span className="h-1.5 w-1.5 rounded-full bg-status-progress-solid" />
              In progress · 3
            </h4>
            <OrderCardMock id="244" timer="04:02" items="2x Margherita · 2x Caesar" />
            <OrderCardMock id="242" timer="12:40" items="2x Smash Burger · 2x Fries" tone="amber" />
            <OrderCardMock id="239" timer="21:10" items="1x Ribeye · 1x Risotto" tone="red" />
          </div>
          <div className="flex min-w-0 flex-col gap-2">
            <h4 className="flex items-center gap-1.5 px-1 pb-0.5 pt-1 text-[11px] font-semibold text-fg-muted">
              <span className="h-1.5 w-1.5 rounded-full bg-status-ready-solid" />
              Ready · 2
            </h4>
            <OrderCardMock id="241" timer="01:20" items="1x Margherita · 2x Tiramisu" />
            <OrderCardMock id="240" timer="03:04" items="1x Wings · 1x Lemonade" />
          </div>
        </div>
      </div>

      <div className="absolute -left-7 top-7 hidden animate-float items-center gap-2.5 rounded-[10px] border border-border-strong bg-surface px-3 py-2.5 text-xs shadow-lg md:flex">
        <span className="font-mono text-[10.5px] text-fg-subtle">avg prep</span>
        <span className="font-semibold">11:42</span>
      </div>
      <div className="absolute -right-8 bottom-5 hidden animate-float items-center gap-2.5 rounded-[10px] border border-border-strong bg-surface px-3 py-2.5 text-xs shadow-lg md:flex">
        <span className="font-mono text-[10.5px] text-fg-subtle">today</span>
        <span className="font-semibold">$4,820 <span className="text-[11px] text-[var(--success)]">+12%</span></span>
      </div>
    </div>
  );
}

export default function Home() {
  return (
    <main id="main-content" className="min-h-screen bg-bg text-fg">
      <nav className="sticky top-0 z-50 border-b border-border bg-bg/80 backdrop-blur-xl">
        <div className="mx-auto flex h-[60px] max-w-6xl items-center gap-8 px-5 md:px-8">
          <Brand />
          <ul className="hidden items-center gap-6 md:flex">
            {navLinks.map(([label, href]) => (
              <li key={label}>
                <a href={href} className="text-[13px] font-medium text-fg-muted transition-colors hover:text-fg">
                  {label}
                </a>
              </li>
            ))}
          </ul>
          <div className="ml-auto flex items-center gap-2">
            <Link href="/login" className="hidden h-[34px] items-center rounded-md px-3.5 text-[13px] font-semibold text-fg-muted hover:text-fg sm:inline-flex">
              Sign in
            </Link>
            <Link href="/signup" className="inline-flex h-[34px] items-center rounded-md bg-accent px-3.5 text-[13px] font-semibold text-accent-fg shadow-xs transition hover:-translate-y-px hover:bg-accent-hover">
              Get started
            </Link>
          </div>
        </div>
      </nav>

      <header className="relative overflow-hidden px-5 py-20 md:px-8 md:py-24">
        <div className="absolute -right-48 -top-52 h-[640px] w-[640px] rounded-full bg-[radial-gradient(circle,oklch(0.78_0.12_42/.35),transparent_65%)]" />
        <div className="absolute -left-60 top-24 h-[520px] w-[520px] rounded-full bg-[radial-gradient(circle,oklch(0.85_0.08_60/.28),transparent_65%)]" />
        <div className="relative mx-auto grid max-w-6xl items-center gap-14 lg:grid-cols-[1.1fr_1fr]">
          <div>
            <span className="mb-5 inline-flex items-center gap-2 rounded-full border border-border-strong bg-surface py-1 pl-1 pr-2.5 text-[11.5px] font-medium text-fg-muted shadow-xs">
              <span className="rounded-full bg-accent-soft px-2 py-0.5 text-[10px] font-bold uppercase tracking-[0.06em] text-accent">New</span>
              Platform 2.0: faster KDS, new Manager dashboard
            </span>
            <h1 className="max-w-2xl text-[44px] font-semibold leading-none tracking-[-0.04em] text-fg md:text-[62px]">
              The operating system for the <em className="not-italic text-accent">modern restaurant</em>.
            </h1>
            <p className="mt-5 max-w-[520px] text-[17px] leading-7 text-fg-muted">
              One platform for the floor, the line, and the back office. dineOS replaces disconnected tools with a single system that moves at service speed.
            </p>
            <div className="mt-8 flex flex-col gap-2.5 sm:flex-row">
              <Link href="/demo" className="inline-flex h-11 items-center justify-center rounded-[9px] bg-accent px-5 text-sm font-semibold text-accent-fg shadow-xs transition hover:-translate-y-px hover:bg-accent-hover">
                Try the live demo
              </Link>
              <a href="#product" className="inline-flex h-11 items-center justify-center rounded-[9px] border border-border-strong bg-surface px-5 text-sm font-semibold text-fg shadow-xs transition hover:-translate-y-px hover:bg-surface-2">
                See how it works
              </a>
            </div>
            <div className="mt-8 flex flex-wrap gap-6 text-xs text-fg-subtle">
              <div><span className="block font-mono text-sm font-semibold text-fg">84</span>restaurants live</div>
              <div><span className="block font-mono text-sm font-semibold text-fg">1,284</span>active staff</div>
              <div><span className="block font-mono text-sm font-semibold text-fg">99.98%</span>uptime, 90 days</div>
            </div>
          </div>
          <HeroMock />
        </div>
      </header>

      <section id="customers" className="border-b border-border px-5 pb-12 md:px-8">
        <div className="mx-auto max-w-6xl">
          <p className="mb-5 text-center text-[10.5px] font-semibold uppercase tracking-[0.08em] text-fg-subtle">
            Trusted by restaurants from 12-seat cafes to 120-seat rooms
          </p>
          <div className="grid gap-5 text-center sm:grid-cols-2 lg:grid-cols-5">
            {restaurantLogos.map((logo) => (
              <div key={logo} className="flex items-center justify-center gap-1.5 text-[15px] font-semibold text-fg-subtle">
                <span className="h-4 w-4 rounded bg-current opacity-60" />
                {logo}
              </div>
            ))}
          </div>
        </div>
      </section>

      <section id="product" className="border-y border-border bg-bg-sunken px-5 py-24 md:px-8">
        <div className="mx-auto max-w-6xl">
          <div className="mx-auto mb-14 max-w-2xl text-center">
            <span className="mb-2.5 block text-[11px] font-semibold uppercase tracking-[0.08em] text-accent">One platform</span>
            <h2 className="text-[34px] font-semibold leading-tight tracking-[-0.03em] md:text-[40px]">Every surface your restaurant runs on, in one place.</h2>
            <p className="mt-3 text-[15px] leading-6 text-fg-muted">Three purpose-built experiences sharing a single data model. No integrations, no syncing delays.</p>
          </div>
          <div className="grid gap-5 lg:grid-cols-3">
            {surfaces.map((surface, index) => (
              <article key={surface.title} className="rounded-[14px] border border-border bg-surface p-7 shadow-xs transition hover:-translate-y-0.5 hover:border-border-strong hover:shadow-lg">
                <SurfaceIcon index={index} />
                <h3 className="text-lg font-semibold tracking-[-0.015em]">{surface.title}</h3>
                <p className="mt-3 text-[13.5px] leading-6 text-fg-muted">{surface.copy}</p>
                <ul className="mt-4 space-y-2">
                  {surface.items.map((item) => (
                    <li key={item} className="flex gap-2 text-[12.5px] text-fg-muted">
                      <CheckIcon />
                      {item}
                    </li>
                  ))}
                </ul>
              </article>
            ))}
          </div>
        </div>
      </section>

      <section className="px-5 py-24 md:px-8">
        <div className="mx-auto max-w-5xl">
          <div className="mx-auto mb-14 max-w-2xl text-center">
            <span className="mb-2.5 block text-[11px] font-semibold uppercase tracking-[0.08em] text-accent">Deep dives</span>
            <h2 className="text-[34px] font-semibold leading-tight tracking-[-0.03em] md:text-[40px]">Built for service speed, not software demos.</h2>
          </div>

          <div className="grid gap-12 border-b border-dashed border-border py-14 lg:grid-cols-[1fr_1.1fr] lg:items-center">
            <div>
              <div className="mb-3 text-[11px] font-semibold uppercase tracking-[0.08em] text-accent">Kitchen display</div>
              <h3 className="text-[32px] font-semibold leading-tight tracking-[-0.02em]">Tickets you can read from the pass.</h3>
              <p className="mt-3 max-w-md text-[14.5px] leading-7 text-fg-muted">Big mono timers. Bold left-rail severity. Modifiers called out in warning yellow. Every KDS decision is tuned so your expo can make a call at a glance.</p>
              <Link href="/kitchen" className="mt-5 inline-flex text-[13px] font-semibold text-accent">Open the KDS demo</Link>
            </div>
            <div className="rounded-[14px] border border-border-strong bg-warm-1000 p-4 text-warm-50 shadow-lg">
              <div className="mb-3 flex items-center gap-2.5 text-[11px]">
                <span className="h-2 w-2 rounded-full bg-[var(--success)] shadow-[0_0_12px_var(--success)]" />
                <b className="text-[13px]">Line 1</b>
                <span className="ml-auto text-right text-[9px] font-semibold uppercase tracking-wider text-warm-500">Active<br /><span className="font-mono text-lg text-white">3</span></span>
              </div>
              <div className="grid gap-2.5 sm:grid-cols-2">
                {["#244 · T-07 · 04:02 · 2x Margherita", "#242 · Takeout · 12:40 · 2x Smash Burger", "#239 · T-02 · 21:10 · 1x Ribeye, 1x Risotto"].map((ticket, index) => (
                  <div key={ticket} className={`rounded-lg bg-warm-900 p-3 text-[11px] ${index === 2 ? "border-l-4 border-status-stalled-red-solid sm:col-span-2" : index === 1 ? "border-l-4 border-status-stalled-amber-solid" : "border-l-4 border-status-progress-solid"}`}>
                    <span className="font-mono font-bold">{ticket}</span>
                    {index === 1 ? <p className="mt-1 text-status-stalled-amber-fg">extra cheese</p> : null}
                  </div>
                ))}
              </div>
            </div>
          </div>

          <div className="grid gap-12 border-b border-dashed border-border py-14 lg:grid-cols-[1.1fr_1fr] lg:items-center">
            <div className="order-2 rounded-[14px] border border-border-strong bg-bg-elevated p-4 shadow-lg lg:order-1">
              <div className="grid gap-2 sm:grid-cols-3">
                {["Orders today|312|+18%", "Revenue|$4,820|+12%", "Avg prep|11:42|-1:18"].map((kpi) => {
                  const [label, value, delta] = kpi.split("|");
                  return (
                    <div key={label} className="rounded-lg border border-border bg-surface p-3">
                      <p className="text-[10px] text-fg-subtle">{label}</p>
                      <p className="mt-1 font-mono text-lg font-semibold">{value}</p>
                      <p className="text-[10px] font-semibold text-[var(--success)]">{delta}</p>
                    </div>
                  );
                })}
              </div>
              <div className="mt-3 rounded-[10px] border border-border bg-surface p-4">
                <div className="mb-4 flex text-[11px] font-semibold">Revenue · today <span className="ml-auto text-[10px] font-medium text-fg-subtle">hourly</span></div>
                <div className="flex h-44 items-end gap-1">
                  {["h-[22%]", "h-[28%]", "h-[34%]", "h-[42%]", "h-[54%]", "h-[62%]", "h-[78%]", "h-[92%]", "h-full", "h-[88%]"].map((height, index) => (
                    <span key={`${height}-${index}`} className={`flex-1 rounded-t bg-gradient-to-b from-ember-400 to-ember-600 ${height} ${index < 4 ? "opacity-50" : ""}`} />
                  ))}
                </div>
              </div>
            </div>
            <div className="order-1 lg:order-2">
              <div className="mb-3 text-[11px] font-semibold uppercase tracking-[0.08em] text-accent">Manager dashboard</div>
              <h3 className="text-[32px] font-semibold leading-tight tracking-[-0.02em]">The numbers that matter, live.</h3>
              <p className="mt-3 max-w-md text-[14.5px] leading-7 text-fg-muted">Orders, revenue, prep time, completion rate: refreshed as service unfolds, not at end-of-day.</p>
              <Link href="/dashboard" className="mt-5 inline-flex text-[13px] font-semibold text-accent">Open the Dashboard</Link>
            </div>
          </div>

          <div className="grid gap-12 py-14 lg:grid-cols-[1fr_1.1fr] lg:items-center">
            <div>
              <div className="mb-3 text-[11px] font-semibold uppercase tracking-[0.08em] text-accent">Multi-location</div>
              <h3 className="text-[32px] font-semibold leading-tight tracking-[-0.02em]">Grow from one room to a group, without switching tools.</h3>
              <p className="mt-3 max-w-md text-[14.5px] leading-7 text-fg-muted">The Platform console gives owners and operators a cross-tenant view: GMV, orders, staff, health, per restaurant or rolled up.</p>
              <Link href="/admin/dashboard" className="mt-5 inline-flex text-[13px] font-semibold text-accent">Open Platform console</Link>
            </div>
            <div className="rounded-[14px] border border-border-strong bg-cool-50 p-4 shadow-lg">
              <div className="mb-3 flex items-center border-b border-cool-200 pb-3">
                <span className="rounded border border-cool-300 bg-cool-150 px-1.5 py-0.5 text-[9px] font-bold uppercase tracking-wider text-cool-700">Platform</span>
                <b className="ml-2 text-[13px]">Restaurants</b>
                <span className="ml-auto font-mono text-[11px] text-cool-700">8</span>
              </div>
              {["Olio & Sale|Pro|$48.2k|Healthy", "Harbor & Hearth|Pro|$62.1k|Healthy", "Noor Kitchen|Pro|$21.4k|Healthy", "Biblioteca Cafe|Pro|$9.8k|Attention", "Tsukemen Club|Pro|$4.2k|Critical"].map((row) => {
                const [name, plan, revenue, health] = row.split("|");
                const healthClass = health === "Healthy" ? "text-[var(--success)]" : health === "Attention" ? "text-[var(--warning)]" : "text-[var(--danger)]";
                return (
                  <div key={name} className="grid grid-cols-[1fr_58px_72px_72px] items-center rounded-md px-2.5 py-2 text-[11px] hover:bg-cool-100">
                    <span className="font-semibold">{name}</span>
                    <span className="font-semibold text-cool-700">{plan}</span>
                    <span className="text-right font-mono font-semibold">{revenue}</span>
                    <span className={`text-right font-semibold ${healthClass}`}>{health}</span>
                  </div>
                );
              })}
            </div>
          </div>
        </div>
      </section>

      <section className="bg-warm-1000 px-5 py-20 text-warm-50 md:px-8">
        <div className="mx-auto max-w-6xl">
          <h2 className="text-center text-[32px] font-semibold tracking-[-0.02em] text-white">Built for the speed of service.</h2>
          <p className="mt-2 text-center text-sm text-warm-400">Numbers from restaurants running dineOS in production.</p>
          <div className="mt-12 grid gap-8 md:grid-cols-4">
            {stats.map(([number, label]) => (
              <div key={number}>
                <div className="bg-gradient-to-b from-ember-300 to-ember-600 bg-clip-text font-mono text-[52px] font-semibold leading-none tracking-[-0.04em] text-transparent">{number}</div>
                <p className="mt-2 max-w-44 text-[13px] leading-6 text-warm-400">{label}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      <section className="border-y border-border bg-bg-sunken px-5 py-24 md:px-8">
        <blockquote className="mx-auto max-w-3xl text-center text-[26px] font-medium leading-snug tracking-[-0.015em]">
          &ldquo;We switched off four separate tools the week we went live. My line cooks stopped asking what is next and started cooking.&rdquo;
        </blockquote>
        <div className="mt-6 flex items-center justify-center gap-3">
          <span className="flex h-9 w-9 items-center justify-center rounded-full bg-gradient-to-br from-ember-300 to-ember-700 text-[13px] font-bold text-white">AC</span>
          <div>
            <p className="text-[13px] font-semibold">Ava Chen</p>
            <p className="text-[11.5px] text-fg-subtle">General Manager · Olio & Sale · Brooklyn</p>
          </div>
        </div>
      </section>

      <section id="story" className="px-5 py-24 md:px-8">
        <div className="mx-auto grid max-w-5xl items-center gap-16 lg:grid-cols-2">
          <div>
            <span className="mb-2.5 block text-[11px] font-semibold uppercase tracking-[0.08em] text-accent">Our story</span>
            <h2 className="text-[36px] font-semibold leading-tight tracking-[-0.025em]">We spent a decade on the line. Then we built the software we wanted.</h2>
            <p className="mt-4 text-[14.5px] leading-7 text-fg-muted">dineOS started in 2023 in a 38-seat dining room in Brooklyn, after our founding team realized their restaurant was running on six disconnected tools.</p>
            <p className="mt-3 text-[14.5px] leading-7 text-fg-muted">We started over. One data model. One design system. One team that ships from the floor out.</p>
            <div className="mt-6 grid gap-4 sm:grid-cols-2">
              {values.map(([title, copy]) => (
                <div key={title} className="rounded-[10px] border border-border bg-surface p-4">
                  <h4 className="text-[13px] font-semibold">{title}</h4>
                  <p className="mt-1 text-xs leading-5 text-fg-muted">{copy}</p>
                </div>
              ))}
            </div>
          </div>
          <div className="relative flex aspect-square items-center justify-center overflow-hidden rounded-[18px] border border-border-strong bg-gradient-to-br from-ember-200 to-warm-100">
            <span className="absolute inset-10 rounded-full border border-black/10" />
            <span className="absolute inset-20 rounded-full border border-black/10" />
            <span className="absolute inset-32 rounded-full border border-black/10" />
            <span className="z-10 flex h-22 w-22 items-center justify-center rounded-3xl bg-gradient-to-br from-ember-500 to-ember-700 text-[40px] font-bold tracking-[-0.04em] text-white shadow-xl">d</span>
          </div>
        </div>
      </section>

      <section id="pricing" className="border-y border-border bg-bg-sunken px-5 py-24 md:px-8">
        <div className="mx-auto max-w-6xl">
          <div className="mx-auto mb-14 max-w-2xl text-center">
            <span className="mb-2.5 block text-[11px] font-semibold uppercase tracking-[0.08em] text-accent">Pricing</span>
            <h2 className="text-[34px] font-semibold leading-tight tracking-[-0.03em] md:text-[40px]">Try the demo, or run your restaurant for $50 a month.</h2>
            <p className="mt-3 text-[15px] leading-6 text-fg-muted">Start with a hands-on demo. When you&rsquo;re ready, one plan covers everything dineOS does.</p>
          </div>
          <div className="mx-auto grid max-w-3xl gap-4 lg:grid-cols-2">
            {plans.map((plan) => (
              <article key={plan.name} className={`relative rounded-[14px] border bg-surface p-7 ${plan.featured ? "border-accent shadow-[0_0_0_3px_var(--accent-soft)] lg:-translate-y-1" : "border-border"}`}>
                {plan.featured ? <span className="absolute -top-3 left-5 rounded-full bg-accent px-2.5 py-1 text-[10px] font-bold uppercase tracking-wider text-white">Most popular</span> : null}
                <h3 className="text-base font-semibold">{plan.name}</h3>
                <p className="mt-3 min-h-10 text-[12.5px] leading-5 text-fg-muted">{plan.description}</p>
                <div className="mt-4">
                  <span className="font-mono text-[40px] font-semibold tracking-[-0.03em]">{plan.price}</span>
                  {plan.suffix ? <span className="ml-1 text-[12.5px] font-medium text-fg-subtle">{plan.suffix}</span> : null}
                </div>
                <Link href={plan.href} className={`mt-4 inline-flex h-[34px] w-full items-center justify-center rounded-md px-3.5 text-[13px] font-semibold shadow-xs ${plan.featured ? "bg-accent text-accent-fg hover:bg-accent-hover" : "border border-border-strong bg-surface text-fg hover:bg-surface-2"}`}>
                  {plan.cta}
                </Link>
                <ul className="mt-4 space-y-2.5">
                  {plan.features.map((feature) => (
                    <li key={feature} className="flex gap-2 text-[12.5px] leading-5 text-fg-muted">
                      <CheckIcon />
                      {feature}
                    </li>
                  ))}
                </ul>
              </article>
            ))}
          </div>
        </div>
      </section>

      <section className="relative overflow-hidden bg-gradient-to-br from-warm-1000 to-warm-950 px-5 py-28 text-center text-white md:px-8">
        <div className="absolute left-1/2 top-[-300px] h-[600px] w-[900px] -translate-x-1/2 rounded-full bg-[radial-gradient(circle,oklch(0.68_0.17_42/.25),transparent_65%)]" />
        <div className="relative mx-auto max-w-3xl">
          <h2 className="text-[38px] font-semibold leading-tight tracking-[-0.03em] md:text-[52px]">Run your restaurant on software your team actually likes.</h2>
          <p className="mx-auto mt-4 max-w-xl text-base leading-7 text-warm-400">Poke around the demo with seeded credentials, or get started on the $50 Pro plan. We will help you import your menu, train your staff, and cut over in a single weekend.</p>
          <div className="mt-8 flex flex-col justify-center gap-2.5 sm:flex-row">
            <Link href="/signup" className="inline-flex h-11 items-center justify-center rounded-[9px] bg-accent px-5 text-sm font-semibold text-white hover:bg-accent-hover">Get started</Link>
            <Link href="/demo" className="inline-flex h-11 items-center justify-center rounded-[9px] border border-white/15 bg-white/10 px-5 text-sm font-semibold text-white backdrop-blur hover:bg-white/15">Try the demo</Link>
          </div>
        </div>
      </section>

      <footer id="footer" className="border-t border-border bg-bg-sunken px-5 py-14 md:px-8">
        <div className="mx-auto max-w-6xl">
          <div className="grid gap-10 border-b border-border pb-10 md:grid-cols-[1.6fr_repeat(4,1fr)]">
            <div>
              <Brand />
              <p className="mt-3 max-w-64 text-[12.5px] leading-6 text-fg-muted">The operating system for the modern restaurant. Built in Brooklyn, running in restaurants from Portland to Miami.</p>
            </div>
            {[
              ["Product", ["Order board", "Kitchen display", "Manager dashboard", "Platform console", "Pricing"]],
              ["Company", ["About", "Customers", "Careers", "Press", "Contact"]],
              ["Resources", ["Design system", "Docs", "Changelog", "API", "Status"]],
              ["Legal", ["Privacy", "Terms", "Security", "DPA"]],
            ].map(([heading, links]) => (
              <div key={heading as string}>
                <h5 className="mb-3 text-[11px] font-semibold uppercase tracking-[0.08em] text-fg-subtle">{heading}</h5>
                <ul className="space-y-2">
                  {(links as string[]).map((link) => (
                    <li key={link}><a href={link === "Pricing" ? "#pricing" : "#"} className="text-[13px] text-fg-muted hover:text-fg">{link}</a></li>
                  ))}
                </ul>
              </div>
            ))}
          </div>
          <div className="flex flex-col gap-3 pt-5 text-[11.5px] text-fg-subtle sm:flex-row sm:items-center sm:justify-between">
            <div>© 2026 dineOS, Inc. Made for restaurants.</div>
            <div className="flex gap-4">
              <span>All systems operational</span>
              <span className="inline-flex items-center gap-1.5 font-semibold text-[var(--success)]"><span className="h-1.5 w-1.5 rounded-full bg-[var(--success)]" />99.98% uptime</span>
            </div>
          </div>
        </div>
      </footer>
    </main>
  );
}
