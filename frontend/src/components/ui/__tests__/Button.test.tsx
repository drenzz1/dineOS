import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Button } from "../Button";

describe("Button", () => {
  it("renders label correctly", () => {
    render(<Button>Save order</Button>);
    expect(
      screen.getByRole("button", { name: "Save order" })
    ).toBeInTheDocument();
  });

  it("calls onClick when clicked", async () => {
    const user = userEvent.setup();
    const onClick = jest.fn();
    render(<Button onClick={onClick}>Click me</Button>);
    await user.click(screen.getByRole("button"));
    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it("disabled state prevents click", async () => {
    const user = userEvent.setup();
    const onClick = jest.fn();
    render(
      <Button onClick={onClick} disabled>
        Click me
      </Button>
    );
    await user.click(screen.getByRole("button"));
    expect(onClick).not.toHaveBeenCalled();
  });

  it("shows loading spinner and suppresses click while loading", async () => {
    const user = userEvent.setup();
    const onClick = jest.fn();
    render(
      <Button onClick={onClick} isLoading>
        Save
      </Button>
    );
    await user.click(screen.getByRole("button"));
    expect(onClick).not.toHaveBeenCalled();
    expect(screen.getByText("Loading...")).toBeInTheDocument();
  });
});
