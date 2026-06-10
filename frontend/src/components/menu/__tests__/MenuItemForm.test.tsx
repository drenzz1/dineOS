import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderWithProviders } from '@/test-utils/wrapper';
import MenuItemForm from '../MenuItemForm';
import { saveMenuItem } from '@/lib/api/menuApi';

jest.mock('@/lib/api/menuApi', () => ({
  saveMenuItem: jest.fn(),
}));

jest.mock('next/image', () => ({
  __esModule: true,
  // eslint-disable-next-line @next/next/no-img-element -- plain <img> stands in for next/image under jsdom
  default: ({ src, alt }: { src: string; alt: string }) => <img src={src} alt={alt} />,
}));

beforeAll(() => {
  window.URL.createObjectURL = jest.fn(() => 'blob:mock');
  window.URL.revokeObjectURL = jest.fn();
});

beforeEach(() => {
  jest.mocked(saveMenuItem).mockResolvedValue({
    id: 'item-1',
    name: 'Test',
    price: 9.99,
    category: 'MainCourse',
  });
});

describe('MenuItemForm', () => {
  it('required fields show error on empty submit', async () => {
    const user = userEvent.setup();
    renderWithProviders(<MenuItemForm onClose={jest.fn()} />);
    await user.click(screen.getByRole('button', { name: /save/i }));
    await waitFor(() =>
      expect(screen.getByText(/name is required/i)).toBeInTheDocument()
    );
    expect(screen.getByText(/price must be/i)).toBeInTheDocument();
    expect(screen.getByText(/category is required/i)).toBeInTheDocument();
  });

  it('price field rejects zero and negative numbers', async () => {
    const user = userEvent.setup();
    renderWithProviders(<MenuItemForm onClose={jest.fn()} />);
    await user.type(screen.getByLabelText(/item name/i), 'Test Item');
    await user.selectOptions(screen.getByLabelText(/category/i), 'MainCourse');
    await user.type(screen.getByLabelText(/price/i), '0');
    await user.click(screen.getByRole('button', { name: /save/i }));
    await waitFor(() =>
      expect(screen.getByText(/price must be positive/i)).toBeInTheDocument()
    );
  });

  it('file upload over 2 MB shows validation error', async () => {
    const user = userEvent.setup();
    const { container } = renderWithProviders(<MenuItemForm onClose={jest.fn()} />);
    const bigFile = new File([''], 'big.jpg', { type: 'image/jpeg' });
    Object.defineProperty(bigFile, 'size', { value: 3 * 1024 * 1024 });
    const fileInput = container.querySelector('input[type="file"]') as HTMLInputElement;
    await user.upload(fileInput, bigFile);
    await waitFor(() =>
      expect(screen.getByText(/smaller than 2 mb/i)).toBeInTheDocument()
    );
  });
});
