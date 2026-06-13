import type { Role, UserStatus } from "@/types";
import type { AdminUser } from "@/types/admin";

const ROLE_BADGE: Record<Role | "SuperAdmin", string> = {
  Manager: "bg-violet-100 text-violet-700",
  Cashier: "bg-sky-100 text-sky-700",
  KitchenStaff: "bg-orange-100 text-orange-700",
  SuperAdmin: "bg-zinc-900 text-zinc-50",
};

const STATUS_BADGE: Record<UserStatus, string> = {
  Active: "bg-status-ready-bg text-success",
  Inactive: "bg-surface-2 text-fg-subtle",
  Suspended: "bg-status-progress-bg text-warning",
};

const rtf = new Intl.RelativeTimeFormat("en", { numeric: "auto" });

function relativeTime(iso: string): string {
  const diffSec = Math.round((new Date(iso).getTime() - Date.now()) / 1000);
  const abs = Math.abs(diffSec);
  if (abs < 60) return rtf.format(diffSec, "second");
  const diffMin = Math.round(diffSec / 60);
  if (Math.abs(diffMin) < 60) return rtf.format(diffMin, "minute");
  const diffHr = Math.round(diffMin / 60);
  if (Math.abs(diffHr) < 24) return rtf.format(diffHr, "hour");
  return rtf.format(Math.round(diffHr / 24), "day");
}

function RoleBadge({ role }: { role: Role | "SuperAdmin" }) {
  return (
    <span
      className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold ${ROLE_BADGE[role]}`}
    >
      {role}
    </span>
  );
}

interface UsersTableProps {
  users: AdminUser[];
  isLoading?: boolean;
  isError?: boolean;
  emptyMessage?: string;
  errorMessage?: string;
}

const COLUMNS = [
  "Name",
  "Email",
  "Role",
  "Restaurant",
  "Status",
  "Last Login",
] as const;

export default function UsersTable({
  users,
  isLoading = false,
  isError = false,
  emptyMessage = "No users found.",
  errorMessage = "Failed to load users.",
}: UsersTableProps) {
  if (isLoading) {
    return (
      <p role="status" className="text-sm text-fg-subtle">
        Loading users…
      </p>
    );
  }

  if (isError) {
    return (
      <div className="rounded-lg border border-status-cancelled-border bg-status-cancelled-bg p-8 text-center">
        <p className="text-sm text-danger">{errorMessage}</p>
      </div>
    );
  }

  if (users.length === 0) {
    return (
      <div className="rounded-lg border border-dashed border-border-strong p-8 text-center">
        <p className="text-sm text-fg-subtle">{emptyMessage}</p>
      </div>
    );
  }

  return (
    <div className="overflow-x-auto rounded-lg border border-border">
      <table className="w-full text-sm text-fg">
        {/* caption is visually hidden but read by screen readers as the table label */}
        <caption className="sr-only">Users</caption>
        <thead className="border-b border-border bg-surface-2">
          <tr>
            {COLUMNS.map((col) => (
              <th
                key={col}
                scope="col"
                className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide text-fg-subtle"
              >
                {col}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-border">
          {users.map((user) => (
            <tr key={user.id} className="hover:bg-surface-2">
              {/* th scope="row" lets screen readers associate all cells in this row with the user's name */}
              <th
                scope="row"
                className="px-4 py-3 text-left font-medium text-fg"
              >
                {user.name}
              </th>
              <td className="px-4 py-3 text-fg-muted">{user.email}</td>
              <td className="px-4 py-3">
                <RoleBadge role={user.role} />
              </td>
              <td className="px-4 py-3 text-fg-muted">
                {user.restaurantName ?? "—"}
              </td>
              <td className="px-4 py-3">
                <span
                  className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold ${STATUS_BADGE[user.status]}`}
                >
                  {user.status}
                </span>
              </td>
              <td className="px-4 py-3 text-fg-subtle">
                {user.lastLogin ? relativeTime(user.lastLogin) : "Never"}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
