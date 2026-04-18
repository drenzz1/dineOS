import ProtectedSidebar from "@/components/shared/ProtectedSidebar";

export default function ProtectedLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <div className="flex min-h-full">
      <ProtectedSidebar />
      <main className="flex-1 p-6">{children}</main>
    </div>
  );
}
