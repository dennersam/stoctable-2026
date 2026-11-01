import { cn } from '@/lib/utils';

export interface Column<T> {
  key: string;
  header: string;
  cell: (row: T) => React.ReactNode;
  align?: 'left' | 'right' | 'center';
  className?: string;
}

interface DataTableProps<T> {
  rows: T[];
  columns: Column<T>[];
  rowKey: (row: T) => string;
  /** Como a linha aparece abaixo de lg, onde uma tabela não cabe. */
  card: (row: T) => React.ReactNode;
  empty: React.ReactNode;
  /** Só some com os dados na primeira carga; depois os mantém esmaecidos. */
  loading?: boolean;
  caption: string;
}

const ALIGN = { left: 'text-left', right: 'text-right', center: 'text-center' };

export function DataTable<T>({ rows, columns, rowKey, card, empty, loading = false, caption }: DataTableProps<T>) {
  if (loading && rows.length === 0) {
    return (
      <div role="status" aria-label="Carregando" className="space-y-2">
        {Array.from({ length: 6 }, (_, i) => (
          <div key={i} className="h-16 animate-pulse rounded-lg bg-gray-200/70 dark:bg-white/5" />
        ))}
      </div>
    );
  }

  if (rows.length === 0) {
    return (
      <div className="rounded-lg border border-dashed border-gray-300 px-4 py-12 text-center text-sm text-gray-500 dark:border-white/15 dark:text-gray-400">
        {empty}
      </div>
    );
  }

  return (
    <div aria-busy={loading} className={cn('transition-opacity', loading && 'opacity-60')}>
      <ul className="grid gap-2 sm:grid-cols-2 lg:hidden">
        {rows.map((row) => (
          <li key={rowKey(row)} className="rounded-lg border border-line bg-surface p-3">
            {card(row)}
          </li>
        ))}
      </ul>

      <div className="hidden overflow-x-auto rounded-lg border border-line bg-surface lg:block">
        <table className="min-w-full divide-y divide-gray-200 text-sm dark:divide-white/10">
          <caption className="sr-only">{caption}</caption>
          <thead className="bg-gray-50 dark:bg-white/5">
            <tr>
              {columns.map((c) => (
                <th
                  key={c.key}
                  scope="col"
                  className={cn('px-4 py-3 text-xs font-medium text-gray-500 dark:text-gray-400', ALIGN[c.align ?? 'left'])}
                >
                  {c.header}
                </th>
              ))}
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100 dark:divide-white/10">
            {rows.map((row) => (
              <tr key={rowKey(row)} className="transition-colors hover:bg-gray-50 dark:hover:bg-white/5">
                {columns.map((c) => (
                  <td
                    key={c.key}
                    className={cn(
                      'px-4 py-3 text-gray-700 dark:text-gray-300',
                      ALIGN[c.align ?? 'left'],
                      c.align === 'right' && 'tabular-nums',
                      c.className
                    )}
                  >
                    {c.cell(row)}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
