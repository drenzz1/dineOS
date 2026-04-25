import { render, screen } from "@testing-library/react";
import { Card } from "../Card";

describe("Card", () => {
  it("renders children", () => {
    render(<Card>Card content</Card>);
    expect(screen.getByText("Card content")).toBeInTheDocument();
  });

  it("applies base card classes by default", () => {
    const { container } = render(<Card>Content</Card>);
    const el = container.firstChild as HTMLElement;
    expect(el).toHaveClass("rounded-md");
    expect(el).toHaveClass("bg-surface");
    expect(el).toHaveClass("shadow-sm");
  });

  it("merges extra className with base classes", () => {
    const { container } = render(
      <Card className="border-2">Content</Card>
    );
    const el = container.firstChild as HTMLElement;
    expect(el).toHaveClass("rounded-md");
    expect(el).toHaveClass("border-2");
  });

  it("forwards additional HTML attributes to the div", () => {
    render(<Card data-testid="my-card">Content</Card>);
    expect(screen.getByTestId("my-card")).toBeInTheDocument();
  });
});
