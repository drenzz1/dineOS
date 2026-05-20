import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithProviders } from "@/test-utils/wrapper";
import SignupPage from "@/app/(public)/signup/page";
import * as signupApi from "@/lib/api/signupApi";
import { ApiError } from "@/lib/api/envelope";

jest.mock("@/lib/api/signupApi", () => ({
  startSignup: jest.fn(),
  getSignupStatus: jest.fn(),
}));

const startSignupMock = signupApi.startSignup as jest.MockedFunction<
  typeof signupApi.startSignup
>;

describe("SignupPage", () => {
  let consoleErrorSpy: jest.SpyInstance;

  beforeEach(() => {
    startSignupMock.mockReset();
    // jsdom's Location.assign is non-configurable; it logs "Not implemented:
    // navigation" to console.error when invoked. Suppress + observe that signal.
    consoleErrorSpy = jest
      .spyOn(console, "error")
      .mockImplementation(() => {});
    sessionStorage.clear();
  });

  afterEach(() => {
    consoleErrorSpy.mockRestore();
  });

  async function fillForm(): Promise<void> {
    const user = userEvent.setup();
    await user.type(screen.getByLabelText(/restaurant name/i), "Da Mario");
    await user.type(screen.getByLabelText(/owner name/i), "Mario Rossi");
    await user.type(
      screen.getByLabelText(/owner email/i),
      "mario@damario.test"
    );
    await user.type(screen.getByLabelText(/phone/i), "+39 333 1234567");
    await user.type(screen.getByLabelText(/city/i), "Rome");
    await user.click(
      screen.getByRole("button", { name: /continue to payment/i })
    );
  }

  it("blocks submit when required fields are empty", async () => {
    const user = userEvent.setup();
    renderWithProviders(<SignupPage />);

    await user.click(
      screen.getByRole("button", { name: /continue to payment/i })
    );

    expect(
      await screen.findByText(/restaurant name is required/i)
    ).toBeInTheDocument();
    expect(screen.getByText(/owner name is required/i)).toBeInTheDocument();
    expect(screen.getByText(/email is required/i)).toBeInTheDocument();
    expect(screen.getByText(/phone is required/i)).toBeInTheDocument();
    expect(screen.getByText(/city is required/i)).toBeInTheDocument();
    expect(startSignupMock).not.toHaveBeenCalled();
  });

  it("invokes startSignup with form values and triggers a Stripe redirect + stashes sessionId on success", async () => {
    startSignupMock.mockResolvedValue({
      checkoutUrl: "https://checkout.stripe.com/c/pay/cs_test_123",
      sessionId: "cs_test_123",
      tenantId: 42,
    });

    renderWithProviders(<SignupPage />);
    await fillForm();

    await waitFor(() => {
      expect(startSignupMock).toHaveBeenCalled();
    });
    // TanStack Query v5 passes (variables, context) — assert on the first arg.
    expect(startSignupMock.mock.calls[0][0]).toEqual({
      restaurantName: "Da Mario",
      ownerName: "Mario Rossi",
      ownerEmail: "mario@damario.test",
      phone: "+39 333 1234567",
      city: "Rome",
    });

    // sessionStorage must be populated BEFORE the cross-origin redirect.
    await waitFor(() => {
      expect(sessionStorage.getItem("dineos.signup.lastSessionId")).toBe(
        "cs_test_123"
      );
    });

    // jsdom can't follow window.location.assign(); the only observable signal
    // it leaves is a "Not implemented: navigation" console.error.
    await waitFor(() => {
      expect(consoleErrorSpy).toHaveBeenCalledWith(
        expect.objectContaining({
          message: expect.stringContaining("Not implemented: navigation"),
        })
      );
    });
  });

  it("surfaces a clear toast on 503", async () => {
    startSignupMock.mockRejectedValue(
      new ApiError({ error: "Billing down.", status: 503 })
    );

    renderWithProviders(<SignupPage />);
    await fillForm();

    expect(
      await screen.findByText(/billing temporarily unavailable/i)
    ).toBeInTheDocument();
  });
});
