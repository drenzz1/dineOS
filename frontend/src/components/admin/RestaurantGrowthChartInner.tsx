"use client";

import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
} from "recharts";
import type { WeeklyGrowth } from "@/hooks/useAdminAnalytics";

interface RestaurantGrowthChartInnerProps {
  data: WeeklyGrowth[];
}

export default function RestaurantGrowthChartInner({
  data,
}: RestaurantGrowthChartInnerProps) {
  return (
    <ResponsiveContainer width="100%" height={220}>
      <BarChart data={data} margin={{ top: 4, right: 8, left: -16, bottom: 0 }}>
        <CartesianGrid strokeDasharray="3 3" stroke="#f4f4f5" />
        <XAxis
          dataKey="week"
          tick={{ fontSize: 11, fill: "#a1a1aa" }}
          axisLine={false}
          tickLine={false}
        />
        <YAxis
          allowDecimals={false}
          tick={{ fontSize: 11, fill: "#a1a1aa" }}
          axisLine={false}
          tickLine={false}
        />
        <Tooltip
          contentStyle={{
            borderRadius: "6px",
            border: "1px solid #e4e4e7",
            fontSize: "12px",
          }}
          cursor={{ fill: "#f4f4f5" }}
        />
        <Bar
          dataKey="newRestaurants"
          name="New restaurants"
          fill="#6366f1"
          radius={[4, 4, 0, 0]}
        />
      </BarChart>
    </ResponsiveContainer>
  );
}
