"use client";

import { useState } from "react";
import dynamic from "next/dynamic";
import { Button } from "@/components/ui/Button";

const Modal = dynamic(
  () => import("@/components/ui/Modal").then((m) => m.Modal),
  { ssr: false }
);
import StaffMemberForm from "@/components/staff/StaffMemberForm";

export default function StaffPage() {
  const [open, setOpen] = useState(false);

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold text-zinc-900">Staff</h1>
        <Button onClick={() => setOpen(true)}>Add Staff Member</Button>
      </div>

      <Modal
        isOpen={open}
        onClose={() => setOpen(false)}
        title="Add Staff Member"
      >
        <StaffMemberForm onClose={() => setOpen(false)} />
      </Modal>
    </div>
  );
}
