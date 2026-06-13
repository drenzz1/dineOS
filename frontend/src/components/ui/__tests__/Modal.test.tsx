import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Modal } from "../Modal";

const DEFAULT_PROPS = {
  title: "Test modal",
  onClose: jest.fn(),
};

beforeEach(() => {
  DEFAULT_PROPS.onClose.mockClear();
});

describe("Modal", () => {
  it("renders dialog and children when open", () => {
    render(
      <Modal {...DEFAULT_PROPS} isOpen>
        Modal body
      </Modal>
    );
    expect(screen.getByRole("dialog")).toBeInTheDocument();
    expect(screen.getByText("Modal body")).toBeInTheDocument();
  });

  it("renders the title", () => {
    render(
      <Modal {...DEFAULT_PROPS} isOpen>
        content
      </Modal>
    );
    expect(screen.getByText("Test modal")).toBeInTheDocument();
  });

  it("keeps long content inside a scrollable dialog body", () => {
    render(
      <Modal {...DEFAULT_PROPS} isOpen>
        content
      </Modal>
    );

    expect(screen.getByRole("dialog")).toHaveClass(
      "max-h-[calc(100dvh-2rem)]"
    );
    expect(screen.getByTestId("modal-body")).toHaveClass("overflow-y-auto");
  });

  it("does not render dialog when closed", () => {
    render(
      <Modal {...DEFAULT_PROPS} isOpen={false}>
        content
      </Modal>
    );
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("calls onClose when Escape key is pressed", async () => {
    const user = userEvent.setup();
    render(
      <Modal {...DEFAULT_PROPS} isOpen>
        content
      </Modal>
    );
    await user.keyboard("{Escape}");
    expect(DEFAULT_PROPS.onClose).toHaveBeenCalledTimes(1);
  });

  it("calls onClose when overlay is clicked", async () => {
    const user = userEvent.setup();
    render(
      <Modal {...DEFAULT_PROPS} isOpen>
        content
      </Modal>
    );
    await user.click(screen.getByTestId("modal-overlay"));
    expect(DEFAULT_PROPS.onClose).toHaveBeenCalledTimes(1);
  });

  it("calls onClose when the close button is clicked", async () => {
    const user = userEvent.setup();
    render(
      <Modal {...DEFAULT_PROPS} isOpen>
        content
      </Modal>
    );
    await user.click(screen.getByRole("button", { name: "Close" }));
    expect(DEFAULT_PROPS.onClose).toHaveBeenCalledTimes(1);
  });
});
