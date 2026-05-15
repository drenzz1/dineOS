import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithProviders } from "@/test-utils/wrapper";
import RestaurantTablesPage from "../page";
import {
  createRestaurantTable,
  listRestaurantTables,
  updateRestaurantTable,
} from "@/lib/api/restaurantTablesApi";
import type { RestaurantTable } from "@/types/restaurantTable";

jest.mock("@/lib/api/restaurantTablesApi", () => ({
  listRestaurantTables: jest.fn(),
  createRestaurantTable: jest.fn(),
  updateRestaurantTable: jest.fn(),
}));

jest.mock("@/hooks/useTenant", () => ({
  useTenant: () => ({ tenantId: "tenant-xyz", restaurantName: "Olio & Sale" }),
}));

const initialTables: RestaurantTable[] = [
  { id: 1, number: 1, capacity: 2, location: "Window", isActive: true, tenantId: 10 },
  { id: 2, number: 2, capacity: 4, location: null, isActive: false, tenantId: 10 },
];

beforeEach(() => {
  jest.mocked(listRestaurantTables).mockResolvedValue(initialTables);
  jest.mocked(createRestaurantTable).mockReset();
  jest.mocked(updateRestaurantTable).mockReset();
});

describe("RestaurantTablesPage", () => {
  it("renders the existing tables", async () => {
    renderWithProviders(<RestaurantTablesPage />);

    expect(await screen.findByText("#1")).toBeInTheDocument();
    expect(screen.getByText("#2")).toBeInTheDocument();
    expect(screen.getByText("Window")).toBeInTheDocument();
  });

  it("creates a new table via POST /v1/restaurant/tables", async () => {
    jest.mocked(createRestaurantTable).mockResolvedValue({
      id: 3,
      number: 3,
      capacity: 6,
      location: "Patio",
      isActive: true,
      tenantId: 10,
    });

    const user = userEvent.setup();
    renderWithProviders(<RestaurantTablesPage />);

    await screen.findByText("#1");

    const numberInput = screen.getByLabelText(/^number$/i);
    const capacityInput = screen.getByLabelText(/capacity/i);
    const locationInput = screen.getByLabelText(/location/i);

    await user.clear(numberInput);
    await user.type(numberInput, "3");
    await user.clear(capacityInput);
    await user.type(capacityInput, "6");
    await user.type(locationInput, "Patio");

    await user.click(screen.getByRole("button", { name: /add table/i }));

    await waitFor(() => {
      expect(jest.mocked(createRestaurantTable).mock.calls[0]?.[0]).toEqual({
        number: 3,
        capacity: 6,
        location: "Patio",
      });
    });
  });

  it("toggles an inactive table to active via PUT /v1/restaurant/tables/:id", async () => {
    jest.mocked(updateRestaurantTable).mockResolvedValue({
      ...initialTables[1]!,
      isActive: true,
    });

    const user = userEvent.setup();
    renderWithProviders(<RestaurantTablesPage />);

    await screen.findByText("#2");

    const activateButton = screen.getByRole("button", { name: /^activate$/i });
    await user.click(activateButton);

    await waitFor(() => {
      const call = jest.mocked(updateRestaurantTable).mock.calls[0];
      expect(call?.[0]).toBe(2);
      expect(call?.[1]).toEqual({ isActive: true });
    });
  });
});
