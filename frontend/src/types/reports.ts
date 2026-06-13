export type SalesByMethod = {
  method: string;
  total: number;
  count: number;
};

export type RevenueByDay = {
  date: string;
  revenue: number;
  orderCount: number;
};

export type SalesReport = {
  from: string;
  to: string;
  orderCount: number;
  totalRevenue: number;
  averageTicket: number;
  byPaymentMethod: SalesByMethod[];
  revenueByDay: RevenueByDay[];
};

export type OrdersByStatus = {
  status: string;
  count: number;
};

export type OrdersByType = {
  orderType: string;
  count: number;
};

export type OrdersByHour = {
  hour: number;
  count: number;
};

export type OrdersReport = {
  from: string;
  to: string;
  totalOrders: number;
  byStatus: OrdersByStatus[];
  byType: OrdersByType[];
  byHour: OrdersByHour[];
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

export type TopItem = {
  name: string;
  quantity: number;
  revenue: number;
};

export type ItemsReport = {
  from: string;
  to: string;
  topItems: TopItem[];
};

export type OrderHistoryItem = {
  id: number;
  createdAt: string;
  tableNumber: number | null;
  orderType: string;
  status: string;
  itemCount: number;
  total: number;
  paymentMethod: string | null;
};

export type OrderHistoryReport = {
  from: string;
  to: string;
  page: number;
  pageSize: number;
  totalCount: number;
  orders: OrderHistoryItem[];
};
