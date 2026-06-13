import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithProviders } from "@/test-utils/wrapper";
import OrderQuickCreate from "../OrderQuickCreate";
import { createOrder, getMenuItems } from "@/lib/api/ordersApi";
import { listRestaurantTables } from "@/lib/api/restaurantTablesApi";
import type { MenuItem, Order } from "@/types";
import { OrderStatus } from "@/types/order";

const push = jest.fn();

jest.mock("next/navigation", () => ({
  useRouter: () => ({ push }),
}));

jest.mock("@/lib/api/ordersApi", () => ({
  getMenuItems: jest.fn(),
  createOrder: jest.fn(),
}));

jest.mock("@/lib/api/restaurantTablesApi", () => ({
  listRestaurantTables: jest.fn(),
}));

const menuItems: MenuItem[] = [
  {
    id: "1",
    name: "Margherita Pizza",
    price: 12.99,
    category: "MainCourse",
  },
];

const createdOrder: Order = {
  id: "100",
  orderType: "dine-in",
  tableNumber: 4,
  status: OrderStatus.New,
  items: [],
  total: 12.99,
  createdAt: new Date().toISOString(),
  updatedAt: new Date().toISOString(),
};

beforeEach(() => {
  push.mockClear();
  jest.mocked(getMenuItems).mockResolvedValue(menuItems);
  jest.mocked(createOrder).mockResolvedValue(createdOrder);
  jest.mocked(listRestaurantTables).mockResolvedValue([
    {
      id: 1,
      number: 4,
      capacity: 4,
      location: "Patio",
      isActive: true,
      tenantId: 1,
    },
    {
      id: 2,
      number: 9,
      capacity: 2,
      location: null,
      isActive: false,
      tenantId: 1,
    },
  ]);
});

describe("OrderQuickCreate", () => {
  it("submits a configured active table instead of accepting a free-form number", async () => {
    const user = userEvent.setup();
    renderWithProviders(<OrderQuickCreate />);

    const tableSelect = await screen.findByRole("combobox", { name: "Table" });
    expect(
      await screen.findByRole("option", { name: /table 9.*inactive/i })
    ).toBeDisabled();

    await user.selectOptions(tableSelect, "4");
    await user.click(
      await screen.findByRole("button", { name: /margherita pizza/i })
    );
    await user.click(screen.getByRole("button", { name: /send order/i }));

    await waitFor(() => expect(createOrder).toHaveBeenCalledTimes(1));
    expect(createOrder).toHaveBeenCalledWith(
      expect.objectContaining({
        orderType: "dine-in",
        tableNumber: 4,
      }),
      expect.any(Object)
    );
  });
});
