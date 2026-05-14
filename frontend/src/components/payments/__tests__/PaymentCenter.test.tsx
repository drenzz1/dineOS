import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithProviders } from "@/test-utils/wrapper";
import PaymentCenter from "../PaymentCenter";
import { getOpenOrders, processPayment } from "@/lib/api/paymentsApi";
import { ApiError } from "@/lib/api/envelope";
import { OrderStatus } from "@/types/order";
import type { Order } from "@/types/order";

jest.mock("next/navigation", () => ({
  useRouter: () => ({ push: jest.fn() }),
}));

jest.mock("@/lib/api/paymentsApi", () => ({
  getOpenOrders: jest.fn(),
  processPayment: jest.fn(),
}));

const mockOpenOrder: Order = {
  id: "42",
  orderType: "dine-in",
  tableNumber: 7,
  status: OrderStatus.Ready,
  items: [{ id: "i1", name: "Margherita Pizza", quantity: 1, unitPrice: 12.99 }],
  total: 12.99,
  createdAt: new Date().toISOString(),
  updatedAt: new Date().toISOString(),
};

beforeEach(() => {
  jest.mocked(getOpenOrders).mockResolvedValue([mockOpenOrder]);
  jest.mocked(processPayment).mockReset();
});

describe("PaymentCenter — ticket #164 Definition of Done", () => {
  it("settles an order end-to-end and shows a success toast", async () => {
    jest.mocked(processPayment).mockResolvedValue({
      id: "p-1",
      orderId: "42",
      amount: 12.99,
      method: "Card",
      status: "Completed",
      createdAt: new Date().toISOString(),
    });

    const user = userEvent.setup();
    renderWithProviders(<PaymentCenter />);

    const payButton = await screen.findByRole("button", { name: /mark paid/i });
    await user.click(payButton);

    await waitFor(() => {
      expect(jest.mocked(processPayment).mock.calls[0]?.[0]).toEqual({
        orderId: "42",
        amount: 12.99,
        method: "Card",
      });
    });

    expect(await screen.findByText(/Order #42 settled/i)).toBeInTheDocument();
  });

  it("shows an 'Order already settled' toast on replay (422)", async () => {
    jest.mocked(processPayment).mockRejectedValue(
      new ApiError({
        error: "Order 42 is already delivered and cannot be paid.",
        errors: ["Order 42 is already delivered and cannot be paid."],
        status: 422,
      })
    );

    const user = userEvent.setup();
    renderWithProviders(<PaymentCenter />);

    const payButton = await screen.findByRole("button", { name: /mark paid/i });
    await user.click(payButton);

    expect(await screen.findByText(/Order already settled/i)).toBeInTheDocument();
  });

  it("shows an 'Amount mismatch' toast on amount disagreement (422)", async () => {
    jest.mocked(processPayment).mockRejectedValue(
      new ApiError({
        error: "Payment amount 10.00 does not match order total 12.99.",
        errors: ["Payment amount 10.00 does not match order total 12.99."],
        status: 422,
      })
    );

    const user = userEvent.setup();
    renderWithProviders(<PaymentCenter />);

    const payButton = await screen.findByRole("button", { name: /mark paid/i });
    await user.click(payButton);

    expect(await screen.findByText(/Amount mismatch/i)).toBeInTheDocument();
  });

  it("shows an 'Order no longer available' toast on 404", async () => {
    jest.mocked(processPayment).mockRejectedValue(
      new ApiError({
        error: "Order 42 not found.",
        errors: ["Order 42 not found."],
        status: 404,
      })
    );

    const user = userEvent.setup();
    renderWithProviders(<PaymentCenter />);

    const payButton = await screen.findByRole("button", { name: /mark paid/i });
    await user.click(payButton);

    expect(await screen.findByText(/Order no longer available/i)).toBeInTheDocument();
  });
});
