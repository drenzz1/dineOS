interface Column<T> {
  key: keyof T & string;
  header: string;
}

interface DataTableProps<T extends Record<string, unknown>> {
  columns: Array<Column<T>>;
  data: T[];
  emptyMessage?: string;
}

export function DataTable<T extends Record<string, unknown>>({
  columns,
  data,
  emptyMessage = "No data available.",
}: DataTableProps<T>) {
  if (data.length === 0) {
    return (
      <div className="rounded-md border border-dashed border-border-strong bg-surface p-8 text-center">
        <p className="text-sm text-fg-subtle">{emptyMessage}</p>
      </div>
    );
  }

  return (
    <div className="overflow-x-auto rounded-md border border-border bg-surface shadow-sm">
      <table className="w-full text-sm text-fg">
        <thead className="border-b border-border bg-surface-2">
          <tr>
            {columns.map((col) => (
              <th
                key={col.key}
                className="px-4 py-3 text-left text-[11px] font-semibold uppercase tracking-[0.04em] text-fg-subtle"
              >
                {col.header}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-border">
          {data.map((row, rowIndex) => (
            <tr
              key={rowIndex}
              className="transition-colors duration-150 hover:bg-surface-2"
            >
              {columns.map((col) => (
                <td key={col.key} className="px-4 py-3 text-[13px]">
                  {String(row[col.key] ?? "")}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
