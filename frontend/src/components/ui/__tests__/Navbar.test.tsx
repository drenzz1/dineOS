import { render, screen } from "@testing-library/react";
import { Navbar } from "../Navbar";

jest.mock("next/navigation", () => ({
  usePathname: () => "/dashboard",
}));

const ALL_LINKS = [
  "Dashboard",
  "Orders",
  "Kitchen",
  "Menu",
  "Reports",
  "Shifts",
  "Staff",
];

const CASHIER_LINKS = ["Orders", "Kitchen"];
const KITCHEN_STAFF_LINKS = ["Kitchen"];

describe("Navbar — Manager", () => {
  beforeEach(() => {
    render(<Navbar role="Manager" />);
  });

  it("sees all navigation links", () => {
    ALL_LINKS.forEach((label) => {
      expect(screen.getByRole("link", { name: label })).toBeInTheDocument();
    });
  });
});

describe("Navbar — Cashier", () => {
  beforeEach(() => {
    render(<Navbar role="Cashier" />);
  });

  it("sees Orders and Kitchen links", () => {
    CASHIER_LINKS.forEach((label) => {
      expect(screen.getByRole("link", { name: label })).toBeInTheDocument();
    });
  });

  it("does not see Manager-only links", () => {
    const restricted = ALL_LINKS.filter((l) => !CASHIER_LINKS.includes(l));
    restricted.forEach((label) => {
      expect(
        screen.queryByRole("link", { name: label })
      ).not.toBeInTheDocument();
    });
  });
});

describe("Navbar — KitchenStaff", () => {
  beforeEach(() => {
    render(<Navbar role="KitchenStaff" />);
  });

  it("sees only the Kitchen link", () => {
    KITCHEN_STAFF_LINKS.forEach((label) => {
      expect(screen.getByRole("link", { name: label })).toBeInTheDocument();
    });
  });

  it("does not see any other links", () => {
    const restricted = ALL_LINKS.filter(
      (l) => !KITCHEN_STAFF_LINKS.includes(l)
    );
    restricted.forEach((label) => {
      expect(
        screen.queryByRole("link", { name: label })
      ).not.toBeInTheDocument();
    });
  });
});
