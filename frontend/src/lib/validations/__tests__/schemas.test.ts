import { orderSchema } from '../order';
import { menuItemSchema } from '../menuItem';
import { staffMemberSchema } from '../staffMember';
import { shiftNoteSchema } from '../shiftNote';

const validItem = { menuItemId: '1', name: 'Pizza', quantity: 1, unitPrice: 10 };

describe('orderSchema', () => {
  it('rejects missing order type', () => {
    const result = orderSchema.safeParse({ items: [validItem] });
    expect(result.success).toBe(false);
  });

  it('rejects dine-in without table number', () => {
    const result = orderSchema.safeParse({ orderType: 'dine-in', items: [validItem] });
    expect(result.success).toBe(false);
    if (!result.success) {
      const paths = result.error.issues.map((i) => i.path.join('.'));
      expect(paths).toContain('tableNumber');
    }
  });

  it('accepts valid pickup order', () => {
    const result = orderSchema.safeParse({ orderType: 'pickup', items: [validItem] });
    expect(result.success).toBe(true);
  });

  it('accepts any configured positive table number', () => {
    const result = orderSchema.safeParse({
      orderType: 'dine-in',
      tableNumber: 75,
      items: [validItem],
    });
    expect(result.success).toBe(true);
  });
});

describe('menuItemSchema', () => {
  it('rejects price ≤ 0', () => {
    const result = menuItemSchema.safeParse({ name: 'Pizza', price: 0, category: 'MainCourse' });
    expect(result.success).toBe(false);
  });

  it('rejects negative price', () => {
    const result = menuItemSchema.safeParse({ name: 'Pizza', price: -5, category: 'MainCourse' });
    expect(result.success).toBe(false);
  });

  it('accepts valid menu item', () => {
    const result = menuItemSchema.safeParse({ name: 'Pizza', price: 9.99, category: 'MainCourse' });
    expect(result.success).toBe(true);
  });
});

describe('staffMemberSchema', () => {
  const base = { fullName: 'John Doe', pin: '1234' };

  it('rejects non-email string', () => {
    const result = staffMemberSchema.safeParse({ ...base, email: 'not-an-email', role: 'Manager' });
    expect(result.success).toBe(false);
  });

  it('rejects role outside Manager | Cashier | KitchenStaff', () => {
    const result = staffMemberSchema.safeParse({ ...base, email: 'john@example.com', role: 'Admin' });
    expect(result.success).toBe(false);
  });

  it('accepts valid staff member', () => {
    const result = staffMemberSchema.safeParse({ ...base, email: 'john@example.com', role: 'KitchenStaff' });
    expect(result.success).toBe(true);
  });
});

describe('shiftNoteSchema', () => {
  it('rejects body over 1000 chars', () => {
    const result = shiftNoteSchema.safeParse({ title: 'Note', body: 'a'.repeat(1001) });
    expect(result.success).toBe(false);
  });

  it('accepts body of exactly 1000 chars', () => {
    const result = shiftNoteSchema.safeParse({ title: 'Note', body: 'a'.repeat(1000) });
    expect(result.success).toBe(true);
  });
});
