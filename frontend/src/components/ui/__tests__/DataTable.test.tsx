import { render, screen } from "@testing-library/react";
import { DataTable } from "../DataTable";

type StaffRow = {
  id: string;
  name: string;
  role: string;
};

const columns = [
  { key: "id" as const, header: "ID" },
  { key: "name" as const, header: "Name" },
  { key: "role" as const, header: "Role" },
];

const data: StaffRow[] = [
  { id: "1", name: "Alice", role: "Manager" },
  { id: "2", name: "Bob", role: "Cashier" },
  { id: "3", name: "Carol", role: "KitchenStaff" },
];

describe("DataTable", () => {
  it("renders the correct number of rows (header + data rows)", () => {
    render(<DataTable columns={columns} data={data} />);
    // getAllByRole('row') includes the header row
    expect(screen.getAllByRole("row")).toHaveLength(data.length + 1);
  });

  it("renders column headers", () => {
    render(<DataTable columns={columns} data={data} />);
    expect(screen.getByText("ID")).toBeInTheDocument();
    expect(screen.getByText("Name")).toBeInTheDocument();
    expect(screen.getByText("Role")).toBeInTheDocument();
  });

  it("renders cell values for each row", () => {
    render(<DataTable columns={columns} data={data} />);
    expect(screen.getByText("Alice")).toBeInTheDocument();
    expect(screen.getByText("Bob")).toBeInTheDocument();
    expect(screen.getByText("Carol")).toBeInTheDocument();
  });

  it("shows default empty state when data is empty", () => {
    render(<DataTable columns={columns} data={[]} />);
    expect(screen.getByText("No data available.")).toBeInTheDocument();
  });

  it("shows custom empty message when data is empty", () => {
    render(
      <DataTable columns={columns} data={[]} emptyMessage="No staff found." />
    );
    expect(screen.getByText("No staff found.")).toBeInTheDocument();
  });

  it("does not render a table when data is empty", () => {
    render(<DataTable columns={columns} data={[]} />);
    expect(screen.queryByRole("table")).not.toBeInTheDocument();
  });
});
