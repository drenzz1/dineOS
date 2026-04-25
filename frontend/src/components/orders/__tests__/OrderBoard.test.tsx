import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithProviders } from "@/test-utils/wrapper";
import OrderBoard from "../OrderBoard";
import { getOrders, updateOrderStatus } from "@/lib/api/ordersApi";
import { OrderStatus } from "@/types/order";
import type { Order } from "@/types/order";

jest.mock("@/lib/api/ordersApi", () => ({
  getOrders: jest.fn(),
  updateOrderStatus: jest.fn(),
}));

const mockOrder: Order = {
  id: "ord-001",
  orderType: "dine-in",
  tableNumber: 3,
  status: OrderStatus.New,
  items: [{ id: "item-001", name: "Margherita Pizza", quantity: 1, unitPrice: 12.99 }],
  createdAt: new Date().toISOString(),
  updatedAt: new Date().toISOString(),
};

beforeEach(() => {
  jest.mocked(getOrders).mockResolvedValue([mockOrder]);
  jest.mocked(updateOrderStatus).mockImplementation(
    async (orderId: string, status: OrderStatus): Promise<Order> => ({
      ...mockOrder,
      id: orderId,
      status,
      updatedAt: new Date().toISOString(),
    })
  );
});

describe("OrderBoard", () => {
  it("moves an order to the next column on double-click", async () => {
    const user = userEvent.setup();
    renderWithProviders(<OrderBoard />);

    const newColumn = await screen.findByRole("region", { name: /new orders/i });
    const inProgressColumn = screen.getByRole("region", { name: /in progress orders/i });
    const orderCard = within(newColumn).getByTestId("order-card");

    await user.dblClick(orderCard);

    await waitFor(() => {
      expect(jest.mocked(updateOrderStatus)).toHaveBeenCalledWith(
        "ord-001",
        OrderStatus.InProgress
      );
    });

    await waitFor(() => {
      expect(within(newColumn).queryByTestId("order-card")).not.toBeInTheDocument();
      expect(within(inProgressColumn).getByTestId("order-card")).toBeInTheDocument();
    });
  });
});
