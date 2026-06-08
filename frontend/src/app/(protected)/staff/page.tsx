"use client";

import { useState, useEffect } from "react";
import { useRouter } from "next/navigation";
import dynamic from "next/dynamic";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Button } from "@/components/ui/Button";
import { StaffTable, StaffTableSkeleton } from "@/components/staff/StaffTable";
import StaffMemberForm from "@/components/staff/StaffMemberForm";
import { useStaff } from "@/hooks/useStaff";
import { useMe } from "@/hooks/useMe";
import { useIsClient } from "@/hooks/useIsClient";
import { useAuthStore } from "@/stores/authStore";
import { getPrimaryRole } from "@/lib/auth/keycloak";
import { setStaffActive } from "@/lib/api/staffApi";
import { queryKeys } from "@/lib/api/queryKeys";
import type { StaffMember, Role } from "@/types/staff";

const Modal = dynamic(
  () => import("@/components/ui/Modal").then((m) => m.Modal),
  { ssr: false }
);

type RoleFilter = "All" | "Manager" | "Cashier" | "KitchenStaff";

const FILTER_OPTIONS: Array<{ label: string; value: RoleFilter }> = [
  { label: "All", value: "All" },
  { label: "Manager", value: "Manager" },
  { label: "Cashier", value: "Cashier" },
  { label: "Kitchen Staff", value: "KitchenStaff" },
];

interface FilterStripProps {
  active: RoleFilter;
  onChange: (value: RoleFilter) => void;
  counts: Record<RoleFilter, number>;
}

function FilterStrip({ active, onChange, counts }: FilterStripProps) {
  return (
    <div className="flex gap-1.5 overflow-x-auto pb-1">
      {FILTER_OPTIONS.map(({ label, value }) => {
        const isActive = active === value;
        return (
          <button
            key={value}
            type="button"
            onClick={() => onChange(value)}
            aria-pressed={isActive}
            className={`shrink-0 inline-flex items-center gap-1.5 rounded-full border h-7 px-3 text-[12px] font-semibold transition-colors duration-150 ${
              isActive
                ? "bg-accent text-accent-fg border-accent"
                : "bg-surface text-fg-muted border-border hover:bg-surface-2 hover:text-fg hover:border-border-strong"
            }`}
          >
            {label}
            <span className={`dos-num text-[10.5px] ${isActive ? "opacity-80" : "text-fg-subtle"}`}>
              {counts[value]}
            </span>
          </button>
        );
      })}
    </div>
  );
}

export default function StaffPage() {
  const router = useRouter();
  const queryClient = useQueryClient();

  const [filter, setFilter] = useState<RoleFilter>("All");
  const [addOpen, setAddOpen] = useState(false);
  const [editTarget, setEditTarget] = useState<StaffMember | null>(null);
  const [deactivateTarget, setDeactivateTarget] = useState<StaffMember | null>(
    null
  );

  const { staff, isLoading, isError } = useStaff();

  // Resolve the active role exactly as ProtectedSidebar does: during a PIN staff
  // session the staff-session token carries its role in a `role` claim (not
  // realm_access.roles), so prefer the stored role; otherwise derive from /me.
  // The isClient gate keeps role null on the server and the first hydration
  // render (the persisted authStore rehydrates synchronously on the client), so
  // SSR and the first client render match and the redirect can't fire too early.
  const isClient = useIsClient();
  const { user: me } = useMe();
  const storedRole = useAuthStore((s) => s.role);
  const isStaffSession = useAuthStore((s) => s.isStaffSession);
  const meRole = (() => {
    if (!me) return null;
    try {
      return getPrimaryRole(me.roles);
    } catch {
      return null;
    }
  })();
  const role = !isClient ? null : isStaffSession ? storedRole : meRole ?? storedRole;

  // Staff management calls the OwnerOnly /v1/staff endpoint, so it is restricted
  // to the business owner (Manager role in owner mode). A PIN-selected staff
  // session — even a Manager's — uses a token that can't call OwnerOnly, which
  // is why ProtectedSidebar also hides the Staff link during a staff session.
  const canManageStaff = role === "Manager" && !isStaffSession;

  const { mutate: doToggle, isPending: isToggling } = useMutation({
    mutationFn: ({ id, isActive }: { id: number; isActive: boolean }) =>
      setStaffActive(id, isActive),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.staff.all });
      setDeactivateTarget(null);
    },
  });

  // Redirect non-owners away once the role has resolved. `role === null` means
  // the session is still hydrating — render nothing rather than bounce a manager.
  useEffect(() => {
    if (role !== null && !canManageStaff) {
      router.replace("/dashboard");
    }
  }, [role, canManageStaff, router]);

  if (!canManageStaff) {
    return null;
  }

  const counts: Record<RoleFilter, number> = {
    All: staff.length,
    Manager: staff.filter((m) => m.role === "Manager").length,
    Cashier: staff.filter((m) => m.role === "Cashier").length,
    KitchenStaff: staff.filter((m) => m.role === "KitchenStaff").length,
  };

  const filtered: StaffMember[] =
    filter === "All"
      ? staff
      : staff.filter((m) => (m.role as Role) === filter);

  function handleToggleActive(member: StaffMember) {
    if (member.isActive) {
      setDeactivateTarget(member);
    } else {
      doToggle({ id: member.id, isActive: true });
    }
  }

  function closeFormModal() {
    setAddOpen(false);
    setEditTarget(null);
  }

  const isFormOpen = addOpen || editTarget !== null;
  const formTitle = editTarget ? "Edit Staff Member" : "Add Staff Member";

  return (
    <div className="space-y-6">
      {/* Page header */}
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-[22px] font-semibold tracking-[-0.01em] text-fg">
            Staff
          </h1>
          <p className="text-[13px] text-fg-muted mt-0.5">
            Manage team members, roles, and access.
          </p>
        </div>
        <Button
          onClick={() => setAddOpen(true)}
          leading={
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
              <path d="M12 5v14M5 12h14" />
            </svg>
          }
        >
          Add Staff Member
        </Button>
      </div>

      {/* Error */}
      {isError && (
        <div className="rounded-md border border-status-cancelled-border bg-status-cancelled-bg px-4 py-3">
          <p className="text-[13px] text-status-cancelled-fg">
            Failed to load staff. Please refresh.
          </p>
        </div>
      )}

      {/* Filter strip */}
      {!isLoading && (
        <FilterStrip active={filter} onChange={setFilter} counts={counts} />
      )}

      {/* Table */}
      {isLoading ? (
        <StaffTableSkeleton />
      ) : (
        <StaffTable
          staff={filtered}
          onEdit={setEditTarget}
          onToggleActive={handleToggleActive}
        />
      )}

      {/* Add / Edit staff modal */}
      <Modal isOpen={isFormOpen} onClose={closeFormModal} title={formTitle}>
        <StaffMemberForm
          onClose={closeFormModal}
          defaultValues={
            editTarget
              ? {
                  id: editTarget.id,
                  fullName: editTarget.fullName,
                  email: editTarget.email,
                  role: editTarget.role as "Manager" | "Cashier" | "KitchenStaff",
                }
              : undefined
          }
        />
      </Modal>

      {/* Deactivate confirmation modal */}
      <Modal
        isOpen={deactivateTarget !== null}
        onClose={() => setDeactivateTarget(null)}
        title="Deactivate staff member?"
        footer={
          <>
            <Button
              variant="secondary"
              onClick={() => setDeactivateTarget(null)}
              disabled={isToggling}
            >
              Cancel
            </Button>
            <Button
              variant="danger"
              isLoading={isToggling}
              onClick={() =>
                deactivateTarget &&
                doToggle({ id: deactivateTarget.id, isActive: false })
              }
            >
              Deactivate
            </Button>
          </>
        }
      >
        <p className="text-[13px] text-fg-muted leading-relaxed">
          <span className="font-semibold text-fg">
            {deactivateTarget?.fullName}
          </span>{" "}
          will no longer be able to sign in. You can reactivate them at any
          time.
        </p>
      </Modal>
    </div>
  );
}
