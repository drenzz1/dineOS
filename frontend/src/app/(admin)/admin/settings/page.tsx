"use client";

import AiProviderSettingsCard from "@/components/admin/AiProviderSettingsCard";

export default function AdminSettingsPage() {
  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold text-zinc-900">Settings</h1>
        <p className="mt-0.5 text-sm text-zinc-500">
          Platform-level configuration for dineOS.
        </p>
      </div>

      <AiProviderSettingsCard />
    </div>
  );
}
