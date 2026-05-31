import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithProviders } from "@/test-utils/wrapper";
import LoginPage from "../page";
import { useAuthStore } from "@/stores/authStore";
import { ApiError } from "@/lib/api/envelope";

const pushMock = jest.fn();

jest.mock("next/navigation", () => ({
  useRouter: () => ({ push: pushMock }),
  useSearchParams: () => ({ get: () => null }),
}));

describe("LoginPage", () => {
  beforeEach(() => {
    pushMock.mockReset();
  });

  it("validates required fields before calling the auth store", async () => {
    const loginSpy = jest.fn();
    useAuthStore.setState({ login: loginSpy } as unknown as Partial<ReturnType<typeof useAuthStore.getState>>);

    const user = userEvent.setup();
    renderWithProviders(<LoginPage />);

    await user.click(screen.getByRole("button", { name: /sign in/i }));

    expect(await screen.findByText(/username is required/i)).toBeInTheDocument();
    expect(screen.getByText(/password is required/i)).toBeInTheDocument();
    expect(loginSpy).not.toHaveBeenCalled();
  });

  it("calls authStore.login and redirects to the staff roster on success", async () => {
    const loginSpy = jest
      .fn()
      .mockResolvedValue({ destination: "/dashboard" });
    useAuthStore.setState({ login: loginSpy } as unknown as Partial<ReturnType<typeof useAuthStore.getState>>);

    const user = userEvent.setup();
    renderWithProviders(<LoginPage />);

    await user.type(screen.getByLabelText(/username/i), "alice");
    await user.type(screen.getByLabelText(/password/i), "s3cr3t");
    await user.click(screen.getByRole("button", { name: /sign in/i }));

    await waitFor(() => {
      expect(loginSpy).toHaveBeenCalledWith("alice", "s3cr3t", null);
    });
    // Post-login goes to the staff roster, not straight to a role dashboard
    // (#staff-pin-auth Phase 3) — the operational role is chosen there via PIN.
    expect(pushMock).toHaveBeenCalledWith("/select-staff");
  });

  it("shows the server error message when the API returns 401", async () => {
    const loginSpy = jest
      .fn()
      .mockRejectedValue(new ApiError({ error: "Invalid credentials.", status: 401 }));
    useAuthStore.setState({ login: loginSpy } as unknown as Partial<ReturnType<typeof useAuthStore.getState>>);

    const user = userEvent.setup();
    renderWithProviders(<LoginPage />);

    await user.type(screen.getByLabelText(/username/i), "alice");
    await user.type(screen.getByLabelText(/password/i), "wrong");
    await user.click(screen.getByRole("button", { name: /sign in/i }));

    expect(await screen.findByRole("alert")).toHaveTextContent(/invalid credentials/i);
    expect(pushMock).not.toHaveBeenCalled();
  });
});
