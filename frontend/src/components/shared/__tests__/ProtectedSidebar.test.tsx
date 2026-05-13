import { act, render, screen } from "@testing-library/react";
import ProtectedSidebar from "../ProtectedSidebar";
import { useAuthStore } from "@/stores/authStore";
import type { Role } from "@/types";

jest.mock("next/navigation", () => ({
  usePathname: () => "/orders",
  useRouter: () => ({ push: jest.fn() }),
}));

const ALL_LINKS = [
  "Dashboard",
  "Orders",
  "Payments",
  "Kitchen",
  "Menu",
  "Reports",
  "Shifts",
  "Staff",
];

function renderForRole(role: Role) {
  act(() => {
    useAuthStore.setState({
      userId: "test-user",
      role,
      tenantId: "demo-tenant",
      restaurantName: "Olio & Sale",
    });
  });

  render(<ProtectedSidebar />);
}

describe("ProtectedSidebar", () => {
  afterEach(() => {
    act(() => {
      useAuthStore.setState({
        userId: null,
        role: null,
        tenantId: null,
        restaurantName: null,
      });
    });
  });

  it("shows every tenant route to managers", () => {
    renderForRole("Manager");

    ALL_LINKS.forEach((label) => {
      expect(screen.getByRole("link", { name: label })).toBeInTheDocument();
    });
  });

  it("shows only Orders, Payments, and Kitchen to cashiers", () => {
    renderForRole("Cashier");

    expect(screen.getByRole("link", { name: "Orders" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Payments" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Kitchen" })).toBeInTheDocument();

    ALL_LINKS.filter(
      (label) => !["Orders", "Payments", "Kitchen"].includes(label)
    ).forEach((label) => {
      expect(screen.queryByRole("link", { name: label })).not.toBeInTheDocument();
    });
  });

  it("shows only Kitchen to kitchen staff", () => {
    renderForRole("KitchenStaff");

    expect(screen.getByRole("link", { name: "Kitchen" })).toBeInTheDocument();

    ALL_LINKS.filter((label) => label !== "Kitchen").forEach((label) => {
      expect(screen.queryByRole("link", { name: label })).not.toBeInTheDocument();
    });
  });
});
