"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import dynamic from "next/dynamic";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Button } from "@/components/ui/Button";
import { StaffTable, StaffTableSkeleton } from "@/components/staff/StaffTable";
import StaffMemberForm from "@/components/staff/StaffMemberForm";
import { useStaff } from "@/hooks/useStaff";
import { useTenant } from "@/hooks/useTenant";
import { toggleStaffActive } from "@/lib/api/staffApi";
import { queryKeys } from "@/lib/api/queryKeys";
import type { StaffMember, Role } from "@/types/staff";

const Modal = dynamic(
  () => import("@/components/ui/Modal").then((m) => m.Modal),
  { ssr: false }
);

// TODO: replace with Zustand auth store / Keycloak session when backend is ready
const MOCK_ROLE = "Manager";

function isManager(role: string): role is "Manager" {
  return role === "Manager";
}

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
  const { tenantId } = useTenant();

  const [filter, setFilter] = useState<RoleFilter>("All");
  const [addOpen, setAddOpen] = useState(false);
  const [editTarget, setEditTarget] = useState<StaffMember | null>(null);
  const [deactivateTarget, setDeactivateTarget] = useState<StaffMember | null>(
    null
  );

  const { staff, isLoading, isError } = useStaff();

  const { mutate: doToggle, isPending: isToggling } = useMutation({
    mutationFn: (id: string) => toggleStaffActive(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.staff.list(tenantId) });
      setDeactivateTarget(null);
    },
  });

  if (!isManager(MOCK_ROLE)) {
    router.replace("/dashboard");
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
      doToggle(member.id);
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
                  pin: editTarget.pin,
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
                deactivateTarget && doToggle(deactivateTarget.id)
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
