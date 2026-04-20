"use client";

import { useState } from "react";
import { Button } from "@/components/ui/Button";
import { Modal } from "@/components/ui/Modal";
import ShiftNoteForm from "@/components/shifts/ShiftNoteForm";

export default function ShiftsPage() {
  const [open, setOpen] = useState(false);

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold text-zinc-900">Shifts</h1>
        <Button onClick={() => setOpen(true)}>Add Shift Note</Button>
      </div>

      <Modal
        isOpen={open}
        onClose={() => setOpen(false)}
        title="Add Shift Note"
      >
        <ShiftNoteForm onClose={() => setOpen(false)} />
      </Modal>
    </div>
  );
}
