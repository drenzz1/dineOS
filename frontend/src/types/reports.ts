export type SalesByMethod = {
  method: string;
  total: number;
  count: number;
};

export type SalesReport = {
  from: string;
  to: string;
  orderCount: number;
  totalRevenue: number;
  averageTicket: number;
  byPaymentMethod: SalesByMethod[];
};

export type OrdersByStatus = {
  status: string;
  count: number;
};

export type OrdersByType = {
  orderType: string;
  count: number;
};

export type OrdersReport = {
  from: string;
  to: string;
  totalOrders: number;
  byStatus: OrdersByStatus[];
  byType: OrdersByType[];
};

export type StaffByRole = {
  role: string;
  total: number;
  active: number;
};

export type StaffReport = {
  total: number;
  active: number;
  inactive: number;
  byRole: StaffByRole[];
};
