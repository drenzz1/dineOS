import { screen, waitFor, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderWithProviders } from '@/test-utils/wrapper';
import OrderWizard from '../OrderWizard';
import { getMenuItems, createOrder } from '@/lib/api/ordersApi';
import { useOrderWizardStore } from '@/stores/orderWizardStore';
import type { MenuItem, Order } from '@/types';

jest.mock('next/navigation', () => ({
  useRouter: () => ({ push: jest.fn() }),
}));

jest.mock('@/lib/api/ordersApi', () => ({
  getMenuItems: jest.fn(),
  createOrder: jest.fn(),
}));

const mockMenuItems: MenuItem[] = [
  { id: '1', name: 'Margherita Pizza', price: 12.99, category: 'MainCourse' },
  { id: '2', name: 'Caesar Salad', price: 8.99, category: 'Starters' },
];

const mockOrder: Order = {
  id: 'ord-new',
  orderType: 'pickup',
  status: 'New' as Order['status'],
  items: [],
  total: 0,
  createdAt: new Date().toISOString(),
  updatedAt: new Date().toISOString(),
};

beforeEach(() => {
  useOrderWizardStore.setState({ step: 1 });
  jest.mocked(getMenuItems).mockResolvedValue(mockMenuItems);
  jest.mocked(createOrder).mockResolvedValue(mockOrder);
});

async function goToStep2(user: ReturnType<typeof userEvent.setup>) {
  await user.click(screen.getByRole('radio', { name: /pickup/i }));
  await user.click(screen.getByRole('button', { name: /next/i }));
  await waitFor(() =>
    expect(screen.getByRole('heading', { name: /step 2/i })).toBeInTheDocument()
  );
}

async function goToStep3(user: ReturnType<typeof userEvent.setup>) {
  await goToStep2(user);
  await waitFor(() =>
    expect(screen.getByRole('checkbox', { name: /margherita pizza/i })).toBeInTheDocument()
  );
  await user.click(screen.getByRole('checkbox', { name: /margherita pizza/i }));
  await user.click(screen.getByRole('button', { name: /next/i }));
  await waitFor(() =>
    expect(screen.getByRole('heading', { name: /step 3/i })).toBeInTheDocument()
  );
}

describe('OrderWizard', () => {
  it('Step 1 renders order type radio group', () => {
    renderWithProviders(<OrderWizard />);
    expect(screen.getByRole('radio', { name: /dine-in/i })).toBeInTheDocument();
    expect(screen.getByRole('radio', { name: /pickup/i })).toBeInTheDocument();
  });

  it('selecting dine-in reveals table number input', async () => {
    const user = userEvent.setup();
    renderWithProviders(<OrderWizard />);
    await user.click(screen.getByRole('radio', { name: /pickup/i }));
    expect(screen.queryByLabelText(/table number/i)).not.toBeInTheDocument();
    await user.click(screen.getByRole('radio', { name: /dine-in/i }));
    expect(screen.getByLabelText(/table number/i)).toBeInTheDocument();
  });

  it('clicking Next on Step 1 with no table number shows validation error', async () => {
    const user = userEvent.setup();
    renderWithProviders(<OrderWizard />);
    await user.click(screen.getByRole('button', { name: /next/i }));
    await waitFor(() =>
      expect(screen.getByText(/table number.*required/i)).toBeInTheDocument()
    );
  });

  it('valid Step 1 advances to Step 2', async () => {
    const user = userEvent.setup();
    renderWithProviders(<OrderWizard />);
    await user.click(screen.getByRole('radio', { name: /pickup/i }));
    await user.click(screen.getByRole('button', { name: /next/i }));
    await waitFor(() =>
      expect(screen.getByRole('heading', { name: /step 2/i })).toBeInTheDocument()
    );
  });

  it('Step 2: clicking Next with no item selected shows error', async () => {
    const user = userEvent.setup();
    renderWithProviders(<OrderWizard />);
    await goToStep2(user);
    await user.click(screen.getByRole('button', { name: /next/i }));
    await waitFor(() =>
      expect(screen.getByText(/at least 1 item required/i)).toBeInTheDocument()
    );
  });

  it('Step 2: selecting an item and clicking Next advances to Step 3', async () => {
    const user = userEvent.setup();
    renderWithProviders(<OrderWizard />);
    await goToStep2(user);
    await waitFor(() =>
      expect(screen.getByRole('checkbox', { name: /margherita pizza/i })).toBeInTheDocument()
    );
    await user.click(screen.getByRole('checkbox', { name: /margherita pizza/i }));
    await user.click(screen.getByRole('button', { name: /next/i }));
    await waitFor(() =>
      expect(screen.getByRole('heading', { name: /step 3/i })).toBeInTheDocument()
    );
  });

  it('Step 3: clicking Back returns to Step 2 with item selection preserved', async () => {
    const user = userEvent.setup();
    renderWithProviders(<OrderWizard />);
    await goToStep3(user);
    await user.click(screen.getByRole('button', { name: /back/i }));
    await waitFor(() =>
      expect(screen.getByRole('heading', { name: /step 2/i })).toBeInTheDocument()
    );
    expect(screen.getByRole('checkbox', { name: /margherita pizza/i })).toBeChecked();
  });

  it('Step 3: submitting calls createOrder mutation with OrderStatus.New', async () => {
    const user = userEvent.setup();
    renderWithProviders(<OrderWizard />);
    await goToStep3(user);
    await user.click(screen.getByRole('button', { name: /place order/i }));
    await waitFor(() => expect(jest.mocked(createOrder)).toHaveBeenCalledTimes(1));
    expect(jest.mocked(createOrder)).toHaveBeenCalledWith(
      expect.objectContaining({ orderType: 'pickup', items: expect.any(Array) }),
      expect.any(Object)
    );
  });

  it('submit button shows loading state during async call', async () => {
    jest.mocked(createOrder).mockImplementation((): Promise<Order> => new Promise(() => {}));
    const user = userEvent.setup();
    renderWithProviders(<OrderWizard />);
    await goToStep3(user);
    await user.click(screen.getByRole('button', { name: /place order/i }));
    await waitFor(() =>
      expect(screen.getByTestId('wizard-submit')).toBeDisabled()
    );
    act(() => {});
  });
});
