"use client";

import { useState, useEffect } from "react";
import { useRouter } from "next/navigation";
import dynamic from "next/dynamic";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Button } from "@/components/ui/Button";
import { StaffTable, StaffTableSkeleton } from "@/components/staff/StaffTable";
import StaffMemberForm from "@/components/staff/StaffMemberForm";
import { useStaff } from "@/hooks/useStaff";
import { toggleStaffActive } from "@/lib/api/staffApi";
import { queryKeys } from "@/lib/api/queryKeys";
import type { StaffMember, Role } from "@/types/staff";

const Modal = dynamic(
  () => import("@/components/ui/Modal").then((m) => m.Modal),
  { ssr: false }
);

// ─── Role guard ───────────────────────────────────────────────────────────────

// TODO: replace with Zustand auth store / Keycloak session when backend is ready
const MOCK_ROLE = "Manager";

function isManager(role: string): role is "Manager" {
  return role === "Manager";
}

// ─── Filter strip ─────────────────────────────────────────────────────────────

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
      {FILTER_OPTIONS.map(({ label, value }) => (
        <button
          key={value}
          type="button"
          onClick={() => onChange(value)}
          className={`shrink-0 rounded-full px-3 py-1 text-xs font-semibold transition-colors ${
            active === value
              ? "bg-zinc-900 text-white"
              : "bg-zinc-100 text-zinc-600 hover:bg-zinc-200"
          }`}
        >
          {label}
          <span className="ml-1.5 opacity-60">{counts[value]}</span>
        </button>
      ))}
    </div>
  );
}

// ─── Page ─────────────────────────────────────────────────────────────────────

export default function StaffPage() {
  const router = useRouter();
  const queryClient = useQueryClient();

  const [filter, setFilter] = useState<RoleFilter>("All");
  const [addOpen, setAddOpen] = useState(false);
  const [editTarget, setEditTarget] = useState<StaffMember | null>(null);
  const [deactivateTarget, setDeactivateTarget] = useState<StaffMember | null>(
    null
  );

  // Role guard — redirect non-Managers to dashboard
  useEffect(() => {
    if (!isManager(MOCK_ROLE)) {
      router.replace("/dashboard");
    }
  }, [router]);

  if (!isManager(MOCK_ROLE)) return null;

  const { staff, isLoading, isError } = useStaff();

  // Filter counts
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

  // Deactivate / reactivate mutation
  const { mutate: doToggle, isPending: isToggling } = useMutation({
    mutationFn: (id: string) => toggleStaffActive(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.staff.list() });
      setDeactivateTarget(null);
    },
  });

  function handleToggleActive(member: StaffMember) {
    if (member.isActive) {
      // Requires confirmation before deactivating
      setDeactivateTarget(member);
    } else {
      // Immediately reactivate without confirmation
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
          <h1 className="text-2xl font-semibold text-zinc-900">Staff</h1>
          <p className="mt-0.5 text-sm text-zinc-500">
            Manage team members and their roles.
          </p>
        </div>
        <Button onClick={() => setAddOpen(true)}>Add Staff Member</Button>
      </div>

      {/* Error */}
      {isError && (
        <div className="rounded-md bg-red-50 px-4 py-3">
          <p className="text-sm text-red-600">
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
      >
        <div className="space-y-5">
          <p className="text-sm text-zinc-600">
            <span className="font-semibold">{deactivateTarget?.fullName}</span>{" "}
            will no longer be able to sign in. You can reactivate them at any
            time.
          </p>
          <div className="flex justify-end gap-3 border-t border-zinc-200 pt-4">
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
          </div>
        </div>
      </Modal>
    </div>
  );
}
