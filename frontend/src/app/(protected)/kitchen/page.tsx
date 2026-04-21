"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import KitchenBoard from "@/components/kitchen/KitchenBoard";

type KitchenRole = "Manager" | "KitchenStaff";

// TODO: replace with Zustand auth store / Keycloak session when backend is ready
const MOCK_ROLE = "KitchenStaff";

function isKitchenRole(role: string): role is KitchenRole {
  return role === "Manager" || role === "KitchenStaff";
}

export default function KitchenPage() {
  const router = useRouter();

  useEffect(() => {
    if (!isKitchenRole(MOCK_ROLE)) {
      router.replace("/login");
    }
  }, [router]);

  // Render nothing until the role check resolves to prevent flash
  if (!isKitchenRole(MOCK_ROLE)) return null;

  return <KitchenBoard />;
}
