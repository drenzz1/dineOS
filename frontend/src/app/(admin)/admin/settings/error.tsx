"use client";

export default function AdminSettingsError() {
  return (
    <div className="rounded-md bg-red-50 px-4 py-3">
      <p className="text-sm text-red-600">Failed to load settings. Please refresh.</p>
    </div>
  );
}
