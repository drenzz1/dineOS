import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithProviders } from "@/test-utils/wrapper";
import DemoPage from "@/app/(public)/demo/page";
import * as demoApi from "@/lib/api/demoApi";
import { ApiError } from "@/lib/api/envelope";

jest.mock("@/lib/api/demoApi", () => ({
  requestDemoAccess: jest.fn(),
}));

const requestMock = demoApi.requestDemoAccess as jest.MockedFunction<
  typeof demoApi.requestDemoAccess
>;

describe("DemoPage", () => {
  beforeEach(() => {
    requestMock.mockReset();
  });

  async function submit(email: string): Promise<void> {
    const user = userEvent.setup();
    await user.type(screen.getByLabelText(/work email/i), email);
    await user.click(screen.getByLabelText(/i understand demo accounts/i));
    await user.click(screen.getByRole("button", { name: /email me a demo/i }));
  }

  it("blocks submit when email is missing", async () => {
    const user = userEvent.setup();
    renderWithProviders(<DemoPage />);

    await user.click(screen.getByLabelText(/i understand demo accounts/i));
    await user.click(screen.getByRole("button", { name: /email me a demo/i }));

    expect(
      await screen.findByText(/email is required/i)
    ).toBeInTheDocument();
    expect(requestMock).not.toHaveBeenCalled();
  });

  it("blocks submit when terms checkbox is unchecked", async () => {
    const user = userEvent.setup();
    renderWithProviders(<DemoPage />);

    await user.type(
      screen.getByLabelText(/work email/i),
      "visitor@example.com"
    );
    await user.click(screen.getByRole("button", { name: /email me a demo/i }));

    expect(
      await screen.findByText(/must accept the demo terms/i)
    ).toBeInTheDocument();
    expect(requestMock).not.toHaveBeenCalled();
  });

  it("submits and swaps to the inbox panel on success", async () => {
    requestMock.mockResolvedValue({ message: "ok" });

    renderWithProviders(<DemoPage />);
    await submit("visitor@example.com");

    await waitFor(() => {
      expect(requestMock).toHaveBeenCalled();
    });
    expect(requestMock.mock.calls[0][0]).toEqual({
      email: "visitor@example.com",
      acceptedTerms: true,
      companyName: "",
    });

    expect(
      await screen.findByRole("heading", { name: /check your inbox/i })
    ).toBeInTheDocument();
    expect(
      screen.getByText(/visitor@example\.com/i, { exact: false })
    ).toBeInTheDocument();
  });

  it("surfaces a 429 with a clear toast", async () => {
    requestMock.mockRejectedValue(
      new ApiError({ error: "Too many requests.", status: 429 })
    );

    renderWithProviders(<DemoPage />);
    await submit("visitor@example.com");

    expect(
      await screen.findByText(/too many requests/i)
    ).toBeInTheDocument();
  });

  it("surfaces a 404 (feature disabled) with a clear toast", async () => {
    requestMock.mockRejectedValue(
      new ApiError({ error: "Disabled.", status: 404 })
    );

    renderWithProviders(<DemoPage />);
    await submit("visitor@example.com");

    expect(
      await screen.findByText(/demo unavailable/i)
    ).toBeInTheDocument();
  });

  it("hides the honeypot field from the accessibility tree", () => {
    renderWithProviders(<DemoPage />);

    // The honeypot wrapper is aria-hidden and `display:none`. Only the
    // legitimate "Work email" textbox should be exposed to a11y queries.
    const textboxes = screen.getAllByRole("textbox");
    expect(textboxes).toHaveLength(1);
    expect(textboxes[0]).toHaveAccessibleName(/work email/i);
  });
});
