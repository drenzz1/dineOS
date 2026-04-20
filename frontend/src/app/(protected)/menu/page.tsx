"use client";

import { useState } from "react";
import { Button } from "@/components/ui/Button";
import { Modal } from "@/components/ui/Modal";
import MenuItemForm from "@/components/menu/MenuItemForm";

export default function MenuPage() {
  const [open, setOpen] = useState(false);

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold text-zinc-900">Menu</h1>
        <Button onClick={() => setOpen(true)}>Add Item</Button>
      </div>

      <Modal
        isOpen={open}
        onClose={() => setOpen(false)}
        title="Add Menu Item"
      >
        <MenuItemForm onClose={() => setOpen(false)} />
      </Modal>
    </div>
  );
}
