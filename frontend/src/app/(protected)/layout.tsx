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
    <div className="flex min-h-full">
      <ProtectedSidebar />
      <main id="main-content" className="flex-1 p-6">{children}</main>
    </div>
  );
}
