interface PaginationProps {
  page: number;
  totalPages: number;
  totalCount: number;
  pageSize: number;
  onPageChange: (page: number) => void;
}

export function Pagination({ page, totalPages, totalCount, pageSize, onPageChange }: PaginationProps) {
  if (totalPages <= 1) return null;

  const from = (page - 1) * pageSize + 1;
  const to = Math.min(page * pageSize, totalCount);

  // Build page numbers to show: always first, last, current ±1, with ellipsis
  const pages: (number | '...')[] = [];
  const add = (n: number) => { if (!pages.includes(n)) pages.push(n); };

  add(1);
  if (page > 3) pages.push('...');
  for (let i = Math.max(2, page - 1); i <= Math.min(totalPages - 1, page + 1); i++) add(i);
  if (page < totalPages - 2) pages.push('...');
  if (totalPages > 1) add(totalPages);

  return (
    <div className="flex flex-col sm:flex-row items-center justify-between gap-3 pt-3 border-t border-gray-200 dark:border-brand-800/40">
      <p className="text-sm text-gray-500 dark:text-gray-400">
        Exibindo <span className="font-medium text-gray-700 dark:text-gray-300">{from}–{to}</span> de{' '}
        <span className="font-medium text-gray-700 dark:text-gray-300">{totalCount}</span> registros
      </p>

      <div className="flex items-center gap-1">
        <button
          onClick={() => onPageChange(page - 1)}
          disabled={page === 1}
          className="px-3 py-1.5 text-sm rounded-md border border-gray-300 dark:border-brand-700/50 text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-brand-900/30 disabled:opacity-40 disabled:cursor-not-allowed"
        >
          ← Anterior
        </button>

        {pages.map((p, i) =>
          p === '...' ? (
            <span key={`ellipsis-${i}`} className="px-2 py-1.5 text-sm text-gray-400 dark:text-gray-500">…</span>
          ) : (
            <button
              key={p}
              onClick={() => onPageChange(p)}
              className={`min-w-[2rem] px-2 py-1.5 text-sm rounded-md border transition-colors ${
                p === page
                  ? 'bg-brand-600 border-brand-600 text-white font-medium'
                  : 'border-gray-300 dark:border-brand-700/50 text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-brand-900/30'
              }`}
            >
              {p}
            </button>
          )
        )}

        <button
          onClick={() => onPageChange(page + 1)}
          disabled={page === totalPages}
          className="px-3 py-1.5 text-sm rounded-md border border-gray-300 dark:border-brand-700/50 text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-brand-900/30 disabled:opacity-40 disabled:cursor-not-allowed"
        >
          Próxima →
        </button>
      </div>
    </div>
  );
}
