import { render, screen } from "@testing-library/react";
import { StatusBadge } from "../StatusBadge";
import { OrderStatus } from "@/types/order";

describe("StatusBadge", () => {
  const cases: Array<[OrderStatus, string]> = [
    [OrderStatus.New, "bg-blue-100"],
    [OrderStatus.InProgress, "bg-yellow-100"],
    [OrderStatus.Ready, "bg-green-100"],
    [OrderStatus.Delivered, "bg-gray-100"],
    [OrderStatus.Cancelled, "bg-red-100"],
  ];

  it.each(cases)(
    "renders %s with correct background class",
    (status, expectedClass) => {
      render(<StatusBadge status={status} />);
      expect(screen.getByText(status)).toHaveClass(expectedClass);
    }
  );

  it("renders the status label as text", () => {
    render(<StatusBadge status={OrderStatus.Ready} />);
    expect(screen.getByText("Ready")).toBeInTheDocument();
  });

  it("merges extra className with badge classes", () => {
    render(<StatusBadge status={OrderStatus.New} className="extra-class" />);
    expect(screen.getByText("New")).toHaveClass("extra-class");
  });
});
