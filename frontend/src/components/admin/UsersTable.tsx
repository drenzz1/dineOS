import type { Role, UserStatus } from "@/types";
import type { AdminUser } from "@/types/admin";

const ROLE_BADGE: Record<Role, string> = {
  Manager: "bg-violet-100 text-violet-700",
  Cashier: "bg-sky-100 text-sky-700",
  KitchenStaff: "bg-orange-100 text-orange-700",
  SuperAdmin: "bg-zinc-900 text-zinc-50",
};

const STATUS_BADGE: Record<UserStatus, string> = {
  Active: "bg-green-100 text-green-700",
  Inactive: "bg-zinc-100 text-zinc-500",
  Suspended: "bg-amber-100 text-amber-700",
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

function RoleBadge({ role }: { role: Role }) {
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
  emptyMessage?: string;
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
  emptyMessage = "No users found.",
}: UsersTableProps) {
  if (isLoading) {
    return (
      <p role="status" className="text-sm text-zinc-500">
        Loading users…
      </p>
    );
  }

  if (users.length === 0) {
    return (
      <div className="rounded-lg border border-dashed border-zinc-300 p-8 text-center">
        <p className="text-sm text-zinc-500">{emptyMessage}</p>
      </div>
    );
  }

  return (
    <div className="overflow-x-auto rounded-lg border border-zinc-200">
      <table className="w-full text-sm text-zinc-900">
        {/* caption is visually hidden but read by screen readers as the table label */}
        <caption className="sr-only">Users</caption>
        <thead className="border-b border-zinc-200 bg-zinc-50">
          <tr>
            {COLUMNS.map((col) => (
              <th
                key={col}
                scope="col"
                className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide text-zinc-500"
              >
                {col}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-zinc-100">
          {users.map((user) => (
            <tr key={user.id} className="hover:bg-zinc-50">
              {/* th scope="row" lets screen readers associate all cells in this row with the user's name */}
              <th
                scope="row"
                className="px-4 py-3 text-left font-medium text-zinc-900"
              >
                {user.name}
              </th>
              <td className="px-4 py-3 text-zinc-600">{user.email}</td>
              <td className="px-4 py-3">
                <RoleBadge role={user.role} />
              </td>
              <td className="px-4 py-3 text-zinc-600">
                {user.restaurantName ?? "—"}
              </td>
              <td className="px-4 py-3">
                <span
                  className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold ${STATUS_BADGE[user.status]}`}
                >
                  {user.status}
                </span>
              </td>
              <td className="px-4 py-3 text-zinc-500">
                {user.lastLogin ? relativeTime(user.lastLogin) : "Never"}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
