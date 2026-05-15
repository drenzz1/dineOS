import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithProviders } from "@/test-utils/wrapper";
import RestaurantProfilePage from "../page";
import {
  getRestaurantProfile,
  updateRestaurantProfile,
} from "@/lib/api/restaurantProfileApi";
import type { RestaurantProfile } from "@/types/restaurantProfile";

jest.mock("@/lib/api/restaurantProfileApi", () => ({
  getRestaurantProfile: jest.fn(),
  updateRestaurantProfile: jest.fn(),
}));

jest.mock("@/hooks/useTenant", () => ({
  useTenant: () => ({ tenantId: "tenant-xyz", restaurantName: "Olio & Sale" }),
}));

const fixture: RestaurantProfile = {
  id: 1,
  name: "Olio & Sale",
  slug: "olio-sale",
  ownerName: "Drini Halili",
  ownerEmail: "drini@example.com",
  phone: "+38344123456",
  city: "Prishtina",
  plan: "Pro",
  status: "Active",
  createdAt: new Date().toISOString(),
};

beforeEach(() => {
  jest.mocked(getRestaurantProfile).mockResolvedValue(fixture);
  jest.mocked(updateRestaurantProfile).mockReset();
});

describe("RestaurantProfilePage", () => {
  it("prefills the form from GET /v1/restaurant", async () => {
    renderWithProviders(<RestaurantProfilePage />);

    await waitFor(() => {
      expect(screen.getByLabelText(/restaurant name/i)).toHaveValue("Olio & Sale");
    });
    expect(screen.getByLabelText(/owner name/i)).toHaveValue("Drini Halili");
    expect(screen.getByLabelText(/phone/i)).toHaveValue("+38344123456");
    expect(screen.getByLabelText(/city/i)).toHaveValue("Prishtina");
  });

  it("submits an updated profile via PUT /v1/restaurant", async () => {
    jest.mocked(updateRestaurantProfile).mockResolvedValue({
      ...fixture,
      city: "Pejë",
    });

    const user = userEvent.setup();
    renderWithProviders(<RestaurantProfilePage />);

    const cityInput = await screen.findByLabelText(/city/i);
    await user.clear(cityInput);
    await user.type(cityInput, "Pejë");
    await user.click(screen.getByRole("button", { name: /save changes/i }));

    await waitFor(() => {
      expect(jest.mocked(updateRestaurantProfile).mock.calls[0]?.[0]).toEqual({
        name: "Olio & Sale",
        ownerName: "Drini Halili",
        phone: "+38344123456",
        city: "Pejë",
      });
    });

    expect(await screen.findByTestId("profile-toast-success")).toBeInTheDocument();
  });
});
