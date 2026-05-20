import { act, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithProviders } from "@/test-utils/wrapper";
import SignupSuccessPage from "@/app/(public)/signup/success/page";
import * as signupApi from "@/lib/api/signupApi";

jest.mock("@/lib/api/signupApi", () => ({
  startSignup: jest.fn(),
  getSignupStatus: jest.fn(),
}));

const getSignupStatusMock = signupApi.getSignupStatus as jest.MockedFunction<
  typeof signupApi.getSignupStatus
>;

let searchParamValue: string | null = null;

jest.mock("next/navigation", () => ({
  useSearchParams: () => ({
    get: (key: string) => (key === "session_id" ? searchParamValue : null),
  }),
}));

describe("SignupSuccessPage", () => {
  beforeEach(() => {
    getSignupStatusMock.mockReset();
    sessionStorage.clear();
    searchParamValue = null;
    jest.useRealTimers();
  });

  it("renders the loading panel while status is PendingPayment", async () => {
    searchParamValue = "cs_test_pending";
    getSignupStatusMock.mockResolvedValue({ status: "PendingPayment" });

    renderWithProviders(<SignupSuccessPage />);

    expect(
      await screen.findByText(/setting up your restaurant/i)
    ).toBeInTheDocument();
    await waitFor(() => {
      expect(getSignupStatusMock).toHaveBeenCalledWith("cs_test_pending");
    });
  });

  it("renders the success panel + sign-in link when status is Active", async () => {
    searchParamValue = "cs_test_active";
    getSignupStatusMock.mockResolvedValue({ status: "Active" });
    sessionStorage.setItem("dineos.signup.lastSessionId", "cs_test_active");

    renderWithProviders(<SignupSuccessPage />);

    expect(await screen.findByText(/you're in\./i)).toBeInTheDocument();
    expect(
      screen.getByRole("link", { name: /go to sign in/i })
    ).toHaveAttribute("href", "/login");

    await waitFor(() => {
      expect(sessionStorage.getItem("dineos.signup.lastSessionId")).toBeNull();
    });
  });

  it("renders the failure panel + retry link when status is Failed", async () => {
    searchParamValue = "cs_test_failed";
    getSignupStatusMock.mockResolvedValue({ status: "Failed" });

    renderWithProviders(<SignupSuccessPage />);

    expect(
      await screen.findByText(/payment didn't complete/i)
    ).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /try again/i })).toHaveAttribute(
      "href",
      "/signup"
    );
  });

  it("falls back to sessionStorage when session_id is missing from the URL", async () => {
    searchParamValue = null;
    sessionStorage.setItem("dineos.signup.lastSessionId", "cs_test_storage");
    getSignupStatusMock.mockResolvedValue({ status: "PendingPayment" });

    renderWithProviders(<SignupSuccessPage />);

    await waitFor(() => {
      expect(getSignupStatusMock).toHaveBeenCalledWith("cs_test_storage");
    });
  });

  it("shows the missing-session panel when no session id is available", async () => {
    searchParamValue = null;

    renderWithProviders(<SignupSuccessPage />);

    expect(await screen.findByText(/missing session/i)).toBeInTheDocument();
    expect(getSignupStatusMock).not.toHaveBeenCalled();
  });

  it("renders 'Still processing' after the soft cap and re-enables polling on Check again", async () => {
    jest.useFakeTimers();
    searchParamValue = "cs_test_slow";
    getSignupStatusMock.mockResolvedValue({ status: "PendingPayment" });

    renderWithProviders(<SignupSuccessPage />);

    await waitFor(() => {
      expect(getSignupStatusMock).toHaveBeenCalled();
    });

    act(() => {
      jest.advanceTimersByTime(30_001);
    });

    expect(
      await screen.findByText(/still processing/i)
    ).toBeInTheDocument();

    getSignupStatusMock.mockResolvedValueOnce({ status: "Active" });
    const user = userEvent.setup({ advanceTimers: jest.advanceTimersByTime });
    await user.click(screen.getByRole("button", { name: /check again/i }));

    await waitFor(() => {
      expect(screen.getByText(/you're in\./i)).toBeInTheDocument();
    });
    jest.useRealTimers();
  });
});
