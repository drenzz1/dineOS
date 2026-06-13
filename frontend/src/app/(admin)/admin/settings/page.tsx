"use client";

import AiProviderSettingsCard from "@/components/admin/AiProviderSettingsCard";

export default function AdminSettingsPage() {
  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold text-fg">Settings</h1>
        <p className="mt-0.5 text-sm text-fg-subtle">
          Platform-level configuration for dineOS.
        </p>
      </div>

      <AiProviderSettingsCard />
    </div>
  );
}
