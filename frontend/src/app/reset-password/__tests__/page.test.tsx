import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithProviders } from "@/test-utils/wrapper";
import ResetPasswordPage from "../page";
import { resetForgottenPassword } from "@/lib/auth/authApi";
import { ApiError } from "@/lib/api/envelope";

jest.mock("next/navigation", () => ({
  useSearchParams: () => ({
    get: (key: string) => (key === "email" ? "owner@example.com" : null),
  }),
}));

jest.mock("@/lib/auth/authApi", () => ({
  resetForgottenPassword: jest.fn(),
}));

describe("ResetPasswordPage", () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it("prefills the email from the query string", () => {
    renderWithProviders(<ResetPasswordPage />);

    expect(screen.getByLabelText(/^email$/i)).toHaveValue("owner@example.com");
  });

  it("rejects a malformed code before calling the API", async () => {
    const user = userEvent.setup();
    renderWithProviders(<ResetPasswordPage />);

    await user.type(screen.getByLabelText(/reset code/i), "12");
    await user.type(screen.getByLabelText(/^new password$/i), "BrandNewPass-456");
    await user.type(screen.getByLabelText(/confirm new password/i), "BrandNewPass-456");
    await user.click(screen.getByRole("button", { name: /reset password/i }));

    expect(
      await screen.findByText("Enter the 6-digit code from the email")
    ).toBeInTheDocument();
    expect(resetForgottenPassword).not.toHaveBeenCalled();
  });

  it("resets the password and shows the success panel", async () => {
    jest.mocked(resetForgottenPassword).mockResolvedValue(undefined);

    const user = userEvent.setup();
    renderWithProviders(<ResetPasswordPage />);

    await user.type(screen.getByLabelText(/reset code/i), "123456");
    await user.type(screen.getByLabelText(/^new password$/i), "BrandNewPass-456");
    await user.type(screen.getByLabelText(/confirm new password/i), "BrandNewPass-456");
    await user.click(screen.getByRole("button", { name: /reset password/i }));

    await waitFor(() => {
      expect(resetForgottenPassword).toHaveBeenCalledWith(
        "owner@example.com",
        "123456",
        "BrandNewPass-456"
      );
    });
    expect(await screen.findByText(/password updated/i)).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /go to sign in/i })).toHaveAttribute(
      "href",
      "/login"
    );
  });

  it("shows the server error message when the code is rejected", async () => {
    jest.mocked(resetForgottenPassword).mockRejectedValue(
      new ApiError({
        error: "Reset code is invalid or expired. Request a new code and try again.",
        status: 400,
      })
    );

    const user = userEvent.setup();
    renderWithProviders(<ResetPasswordPage />);

    await user.type(screen.getByLabelText(/reset code/i), "123456");
    await user.type(screen.getByLabelText(/^new password$/i), "BrandNewPass-456");
    await user.type(screen.getByLabelText(/confirm new password/i), "BrandNewPass-456");
    await user.click(screen.getByRole("button", { name: /reset password/i }));

    expect(await screen.findByRole("alert")).toHaveTextContent(
      /reset code is invalid or expired/i
    );
  });
});
