import { render, screen } from "@testing-library/react";
import { StatusBadge } from "../StatusBadge";
import { OrderStatus } from "@/types/order";

describe("StatusBadge", () => {
  const cases: Array<[OrderStatus, string, string]> = [
    [OrderStatus.New, "New", "bg-status-new-bg"],
    [OrderStatus.InProgress, "In progress", "bg-status-progress-bg"],
    [OrderStatus.Ready, "Ready", "bg-status-ready-bg"],
    [OrderStatus.Delivered, "Delivered", "bg-status-delivered-bg"],
    [OrderStatus.Cancelled, "Cancelled", "bg-status-cancelled-bg"],
  ];

  it.each(cases)(
    "renders %s with its token-backed background class",
    (status, label, expectedClass) => {
      render(<StatusBadge status={status} />);
      expect(screen.getByText(label)).toHaveClass(expectedClass);
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

  it("renders the solid variant with the status solid background", () => {
    render(<StatusBadge status={OrderStatus.Ready} solid />);
    expect(screen.getByText("Ready")).toHaveClass("bg-status-ready-solid");
  });
});
