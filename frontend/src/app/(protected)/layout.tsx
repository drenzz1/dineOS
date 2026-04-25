import type { Metadata } from "next";
import ProtectedSidebar from "@/components/shared/ProtectedSidebar";

export const metadata: Metadata = {
  title: { template: "%s | dineOS", default: "dineOS" },
};

export default function ProtectedLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <div className="flex min-h-full bg-bg text-fg">
      <ProtectedSidebar />
      <main
        id="main-content"
        className="flex-1 min-w-0 p-6 animate-fade-up"
      >
        {children}
      </main>
    </div>
  );
}
