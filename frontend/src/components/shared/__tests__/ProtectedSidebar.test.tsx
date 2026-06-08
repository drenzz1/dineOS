import { act, render, screen } from "@testing-library/react";
import ProtectedSidebar from "../ProtectedSidebar";
import { useAuthStore } from "@/stores/authStore";
import type { Role } from "@/types";

jest.mock("next/navigation", () => ({
  usePathname: () => "/orders",
  useRouter: () => ({ push: jest.fn() }),
}));

jest.mock("@/hooks/useMe", () => ({
  useMe: () => ({ user: undefined, isLoading: false, isError: false }),
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

  it("shows only Orders, Payments, Kitchen, and Shifts to cashiers", () => {
    renderForRole("Cashier");

    const allowed = ["Orders", "Payments", "Kitchen", "Shifts"];
    allowed.forEach((label) => {
      expect(screen.getByRole("link", { name: label })).toBeInTheDocument();
    });

    ALL_LINKS.filter((label) => !allowed.includes(label)).forEach((label) => {
      expect(screen.queryByRole("link", { name: label })).not.toBeInTheDocument();
    });
  });

  it("shows only Kitchen and Shifts to kitchen staff", () => {
    renderForRole("KitchenStaff");

    const allowed = ["Kitchen", "Shifts"];
    allowed.forEach((label) => {
      expect(screen.getByRole("link", { name: label })).toBeInTheDocument();
    });

    ALL_LINKS.filter((label) => !allowed.includes(label)).forEach((label) => {
      expect(screen.queryByRole("link", { name: label })).not.toBeInTheDocument();
    });
  });
});
