/**
 * Restrained line-art illustrations for EmptyState and zero-data surfaces.
 * Colors come from the design tokens via CSS variables so they adapt to the
 * active theme and chrome automatically.
 */

const COMMON_PROPS = {
  width: 88,
  height: 64,
  viewBox: "0 0 88 64",
  fill: "none",
  "aria-hidden": true,
} as const;

export function PlateIllo() {
  return (
    <svg {...COMMON_PROPS}>
      <ellipse cx="44" cy="44" rx="32" ry="8" fill="var(--surface-3)" />
      <circle
        cx="44"
        cy="32"
        r="22"
        stroke="var(--border-strong)"
        strokeWidth="1.2"
        fill="var(--surface)"
      />
      <circle
        cx="44"
        cy="32"
        r="16"
        stroke="var(--border-strong)"
        strokeWidth="1"
        strokeDasharray="2 2"
        fill="none"
      />
      <circle cx="44" cy="32" r="2" fill="var(--accent)" opacity="0.5" />
    </svg>
  );
}

export function TicketIllo() {
  return (
    <svg {...COMMON_PROPS}>
      <path
        d="M20 10h48v44l-4-3-4 3-4-3-4 3-4-3-4 3-4-3-4 3-4-3-4 3-4-3-4 3z"
        fill="var(--surface)"
        stroke="var(--border-strong)"
        strokeWidth="1.2"
        strokeLinejoin="round"
      />
      <line x1="26" y1="20" x2="58" y2="20" stroke="var(--border-strong)" strokeWidth="1" />
      <line x1="26" y1="28" x2="52" y2="28" stroke="var(--border-strong)" strokeWidth="1" />
      <line x1="26" y1="36" x2="46" y2="36" stroke="var(--border-strong)" strokeWidth="1" />
    </svg>
  );
}

export function FlameIllo() {
  return (
    <svg {...COMMON_PROPS}>
      <circle cx="44" cy="34" r="22" fill="var(--accent-soft)" opacity="0.6" />
      <path
        d="M40 50c-6-2-10-7-10-14 0-5 3-8 5-10 1 3 3 4 4 2-1-6 2-10 6-14 0 6 4 9 7 13 3 4 4 7 4 11 0 7-4 12-10 14-2 0-4 0-6-2z"
        fill="var(--accent)"
        opacity="0.85"
      />
      <path
        d="M42 48c-3-1-5-4-5-7 0-3 2-5 3-6 0 3 2 3 3 1 0-3 1-5 4-7 0 3 2 5 3 7 2 3 2 5 2 7 0 4-2 6-5 7-2 0-3 0-5-2z"
        fill="var(--warm-0)"
        opacity="0.35"
      />
    </svg>
  );
}

export function StaffIllo() {
  return (
    <svg {...COMMON_PROPS}>
      <circle cx="32" cy="28" r="8" stroke="var(--border-strong)" strokeWidth="1.2" fill="var(--surface)" />
      <path
        d="M16 56c0-8 7-14 16-14s16 6 16 14"
        stroke="var(--border-strong)"
        strokeWidth="1.2"
        fill="var(--surface)"
      />
      <circle cx="60" cy="22" r="6" stroke="var(--border-strong)" strokeWidth="1.2" fill="var(--surface-2)" />
      <path
        d="M48 50c0-6 6-10 12-10s12 4 12 10"
        stroke="var(--border-strong)"
        strokeWidth="1.2"
        fill="var(--surface-2)"
      />
    </svg>
  );
}

export function MenuIllo() {
  return (
    <svg {...COMMON_PROPS}>
      <rect x="22" y="10" width="44" height="48" rx="4" fill="var(--surface)" stroke="var(--border-strong)" strokeWidth="1.2" />
      <line x1="30" y1="22" x2="58" y2="22" stroke="var(--border-strong)" strokeWidth="1.2" />
      <line x1="30" y1="30" x2="52" y2="30" stroke="var(--border-strong)" strokeWidth="1" />
      <line x1="30" y1="38" x2="54" y2="38" stroke="var(--border-strong)" strokeWidth="1" />
      <line x1="30" y1="46" x2="48" y2="46" stroke="var(--border-strong)" strokeWidth="1" />
      <circle cx="66" cy="14" r="3" fill="var(--accent)" />
    </svg>
  );
}

export function StoreIllo() {
  return (
    <svg {...COMMON_PROPS}>
      <path d="M18 24h52v30H18z" fill="var(--surface)" stroke="var(--border-strong)" strokeWidth="1.2" />
      <path d="M14 24l4-12h52l4 12" fill="var(--surface-2)" stroke="var(--border-strong)" strokeWidth="1.2" />
      <rect x="36" y="36" width="16" height="18" fill="var(--accent-soft)" stroke="var(--border-strong)" strokeWidth="1" />
      <path d="M22 24v4M32 24v4M42 24v4M52 24v4M62 24v4" stroke="var(--border-strong)" strokeWidth="1" />
    </svg>
  );
}

export function NoteIllo() {
  return (
    <svg {...COMMON_PROPS}>
      <rect
        x="22"
        y="10"
        width="40"
        height="44"
        rx="3"
        fill="var(--surface-2)"
        stroke="var(--border-strong)"
        strokeWidth="1.2"
        transform="rotate(-4 42 32)"
      />
      <line x1="28" y1="22" x2="54" y2="22" stroke="var(--border-strong)" strokeWidth="1" />
      <line x1="28" y1="30" x2="50" y2="30" stroke="var(--border-strong)" strokeWidth="1" />
      <line x1="28" y1="38" x2="46" y2="38" stroke="var(--border-strong)" strokeWidth="1" />
    </svg>
  );
}

export const Illo = {
  Plate: PlateIllo,
  Ticket: TicketIllo,
  Flame: FlameIllo,
  Staff: StaffIllo,
  Menu: MenuIllo,
  Store: StoreIllo,
  Note: NoteIllo,
} as const;
