"use client";

export default function AdminSettingsError() {
  return (
    <div className="rounded-md bg-status-cancelled-bg px-4 py-3">
      <p className="text-sm text-danger">Failed to load settings. Please refresh.</p>
    </div>
  );
}
