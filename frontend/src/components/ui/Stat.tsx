import type { ReactNode } from "react";

type Trend = "up" | "down" | "flat";

interface StatProps {
  label: string;
  value: ReactNode;
  delta?: string;
  trend?: Trend;
  sub?: string;
  icon?: ReactNode;
  sparkData?: number[];
  className?: string;
}

const trendClasses: Record<Trend, string> = {
  up: "text-success bg-status-ready-bg",
  down: "text-danger bg-status-cancelled-bg",
  flat: "text-fg-subtle bg-surface-2",
};

function mergeClasses(...classes: Array<string | undefined | false>): string {
  return classes.filter(Boolean).join(" ");
}

function ArrowUp() {
  return (
    <svg width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="m6 14 6-6 6 6" />
    </svg>
  );
}

function ArrowDown() {
  return (
    <svg width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="m6 10 6 6 6-6" />
    </svg>
  );
}

function Sparkline({ data, id }: { data: number[]; id: string }) {
  const max = Math.max(...data);
  const min = Math.min(...data);
  const W = 100;
  const H = 34;
  const range = max - min || 1;

  const pts = data.map((v, i) => {
    const x = (i / (data.length - 1)) * W;
    const y = H - ((v - min) / range) * H * 0.85 - H * 0.08;
    return `${x},${y}`;
  });

  const linePath = pts.map((p, i) => (i === 0 ? `M${p}` : `L${p}`)).join(" ");
  const areaPath = `${linePath} L${W},${H} L0,${H} Z`;
  const gid = `spark-${id}`;

  return (
    <svg
      viewBox={`0 0 ${W} ${H}`}
      preserveAspectRatio="none"
      aria-hidden="true"
      style={{ width: "100%", height: H, marginTop: 10, display: "block" }}
    >
      <defs>
        <linearGradient id={gid} x1="0" x2="0" y1="0" y2="1">
          <stop offset="0%" stopColor="var(--accent)" stopOpacity="0.2" />
          <stop offset="100%" stopColor="var(--accent)" stopOpacity="0" />
        </linearGradient>
      </defs>
      <path d={areaPath} fill={`url(#${gid})`} />
      <path
        d={linePath}
        fill="none"
        stroke="var(--accent)"
        strokeWidth="1.5"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

export function Stat({ label, value, delta, trend = "up", sub, icon, sparkData, className }: StatProps) {
  const sparkId = label.replace(/[^a-z0-9]/gi, "-").toLowerCase();

  return (
    <div
      className={mergeClasses(
        "bg-surface border border-border rounded-md shadow-sm p-4",
        "transition-[box-shadow,border-color] duration-200",
        className,
      )}
    >
      <div className="flex items-center justify-between mb-2.5">
        <div className="flex items-center gap-2 text-xs font-medium text-fg-muted">
          {icon && <span className="text-fg-subtle">{icon}</span>}
          {label}
        </div>
        {delta != null && (
          <span
            className={mergeClasses(
              "inline-flex items-center gap-0.5 rounded-full px-1.5 py-0.5 text-[11px] font-semibold",
              trendClasses[trend],
            )}
          >
            {trend === "up" && <ArrowUp />}
            {trend === "down" && <ArrowDown />}
            {delta}
          </span>
        )}
      </div>
      <div className="dos-num text-[28px] font-semibold tracking-[-0.02em] text-fg">
        {value}
      </div>
      {sub && <div className="text-[11.5px] text-fg-subtle mt-1">{sub}</div>}
      {sparkData && <Sparkline data={sparkData} id={sparkId} />}
    </div>
  );
}
