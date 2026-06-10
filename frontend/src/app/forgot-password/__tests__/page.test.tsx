import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithProviders } from "@/test-utils/wrapper";
import ForgotPasswordPage from "../page";
import { requestPasswordReset } from "@/lib/auth/authApi";
import { ApiError } from "@/lib/api/envelope";

const pushMock = jest.fn();

jest.mock("next/navigation", () => ({
  useRouter: () => ({ push: pushMock }),
}));

jest.mock("@/lib/auth/authApi", () => ({
  requestPasswordReset: jest.fn(),
}));

describe("ForgotPasswordPage", () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it("validates the email before calling the API", async () => {
    const user = userEvent.setup();
    renderWithProviders(<ForgotPasswordPage />);

    await user.click(screen.getByRole("button", { name: /send reset code/i }));

    expect(await screen.findByText(/email is required/i)).toBeInTheDocument();
    expect(requestPasswordReset).not.toHaveBeenCalled();
  });

  it("requests a code and moves to the reset page with the email prefilled", async () => {
    jest.mocked(requestPasswordReset).mockResolvedValue(undefined);

    const user = userEvent.setup();
    renderWithProviders(<ForgotPasswordPage />);

    await user.type(screen.getByLabelText(/email/i), "owner@example.com");
    await user.click(screen.getByRole("button", { name: /send reset code/i }));

    await waitFor(() => {
      expect(requestPasswordReset).toHaveBeenCalledWith("owner@example.com");
    });
    expect(pushMock).toHaveBeenCalledWith(
      "/reset-password?email=owner%40example.com"
    );
  });

  it("shows the server error message when the request is rejected", async () => {
    jest
      .mocked(requestPasswordReset)
      .mockRejectedValue(
        new ApiError({ error: "Too many attempts, try again later.", status: 429 })
      );

    const user = userEvent.setup();
    renderWithProviders(<ForgotPasswordPage />);

    await user.type(screen.getByLabelText(/email/i), "owner@example.com");
    await user.click(screen.getByRole("button", { name: /send reset code/i }));

    expect(await screen.findByRole("alert")).toHaveTextContent(/too many attempts/i);
    expect(pushMock).not.toHaveBeenCalled();
  });
});
