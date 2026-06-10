"use client";

import { Suspense, useState } from "react";
import { useSearchParams } from "next/navigation";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { Card } from "@/components/ui/Card";
import RoleBadge from "@/components/staff/RoleBadge";
import { useStaff } from "@/hooks/useStaff";
import { useAuthStore } from "@/stores/authStore";
import { getDestination } from "@/lib/auth/keycloak";
import { ApiError } from "@/lib/api/envelope";
import type { StaffMember } from "@/types";

function SelectStaffInner() {
  const searchParams = useSearchParams();
  const from = searchParams.get("from");

  const role = useAuthStore((s) => s.role);
  const logout = useAuthStore((s) => s.logout);
  const startStaffSession = useAuthStore((s) => s.startStaffSession);

  async function handleSignOut() {
    await logout();
    window.location.assign("/login");
  }

  const { staff, isLoading, isError } = useStaff();
  const activeStaff = staff.filter((s) => s.isActive);

  const [selected, setSelected] = useState<StaffMember | null>(null);
  const [pin, setPin] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  function pick(member: StaffMember) {
    setSelected(member);
    setPin("");
    setError(null);
  }

  function continueAsOwner() {
    const ownerRole = role && role !== "SuperAdmin" ? role : "Manager";
    // Full document request — same reason as the staff session path below.
    window.location.assign(getDestination(ownerRole, from));
  }

  async function submitPin(e: React.FormEvent) {
    e.preventDefault();
    if (!selected) return;
    setSubmitting(true);
    setError(null);
    try {
      const { role: staffRole } = await startStaffSession(selected.id, pin);
      // Starting a staff session replaces the active access-token and role
      // cookies. Use a fresh document request so Next's client-router cache
      // cannot reuse a redirect fetched before those cookies were replaced.
      window.location.assign(getDestination(staffRole, from));
    } catch (err) {
      setError(
        err instanceof ApiError ? err.error : "Could not start session. Check the PIN and try again."
      );
      setSubmitting(false);
    }
  }

  return (
    <main id="main-content" className="mx-auto min-h-screen max-w-2xl px-4 py-12">
      <div className="mb-8 flex items-start justify-between">
        <header className="text-center flex-1">
          <h1 className="text-2xl font-semibold text-fg">Who&apos;s working?</h1>
          <p className="mt-1 text-sm text-fg-muted">
            Choose your profile and enter your PIN to start your shift.
          </p>
        </header>
        <button
          type="button"
          onClick={handleSignOut}
          className="shrink-0 text-[13px] text-fg-muted hover:text-fg transition-colors"
        >
          Sign out
        </button>
      </div>

      {isLoading && <p className="text-center text-sm text-fg-muted">Loading staff…</p>}
      {isError && (
        <p role="alert" className="text-center text-sm text-red-700">
          Couldn&apos;t load the staff list. Try again, or continue as the owner below.
        </p>
      )}

      {!isLoading && !isError && activeStaff.length === 0 && (
        <p className="text-center text-sm text-fg-muted">
          No staff members yet. Continue as the owner to add them in Staff settings.
        </p>
      )}

      <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
        {activeStaff.map((member) => (
          <button
            key={member.id}
            type="button"
            onClick={() => pick(member)}
            aria-pressed={selected?.id === member.id}
            className={`rounded-lg border p-4 text-left transition ${
              selected?.id === member.id
                ? "border-accent ring-2 ring-accent/40"
                : "border-zinc-200 hover:border-zinc-300"
            }`}
          >
            <span className="block truncate text-sm font-medium text-fg">{member.fullName}</span>
            <RoleBadge role={member.role} className="mt-2" />
          </button>
        ))}
      </div>

      {selected && (
        <Card className="mt-6 p-5">
          <form onSubmit={submitPin} className="space-y-4">
            <Input
              id="staff-pin"
              label={`Enter ${selected.fullName}'s 4-digit PIN`}
              type="password"
              inputMode="numeric"
              autoComplete="off"
              maxLength={4}
              value={pin}
              error={error ?? undefined}
              onChange={(e) => setPin(e.target.value.replace(/\D/g, "").slice(0, 4))}
            />
            <div className="flex gap-2">
              <Button type="submit" block isLoading={submitting} disabled={pin.length !== 4}>
                Start shift
              </Button>
              <Button type="button" variant="secondary" onClick={() => setSelected(null)}>
                Cancel
              </Button>
            </div>
          </form>
        </Card>
      )}

      <div className="mt-8 border-t border-zinc-200 pt-6 text-center">
        <Button type="button" variant="ghost" onClick={continueAsOwner}>
          Continue as the owner (full access)
        </Button>
      </div>
    </main>
  );
}

export default function SelectStaffPage() {
  return (
    <Suspense fallback={<div className="p-12 text-center text-sm text-fg-muted">Loading…</div>}>
      <SelectStaffInner />
    </Suspense>
  );
}
