export interface RestaurantTable {
  id: number;
  number: number;
  capacity: number;
  location: string | null;
  isActive: boolean;
  tenantId: number;
}
