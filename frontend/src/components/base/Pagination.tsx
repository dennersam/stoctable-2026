import { ChevronLeft, ChevronRight } from 'lucide-react';

const STEP_BUTTON =
  'inline-flex min-h-11 items-center gap-1 rounded-md border border-gray-300 px-3 text-sm text-gray-700 hover:bg-gray-50 ' +
  'disabled:cursor-not-allowed disabled:opacity-40 sm:min-h-0 sm:py-1.5 dark:border-white/15 dark:text-gray-300 dark:hover:bg-white/5';

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
    <nav aria-label="Paginação" className="flex flex-col sm:flex-row items-center justify-between gap-3 pt-3">
      <p className="hidden text-sm text-gray-500 dark:text-gray-400 sm:block">
        Exibindo <span className="font-medium text-gray-700 dark:text-gray-300">{from}–{to}</span> de{' '}
        <span className="font-medium text-gray-700 dark:text-gray-300">{totalCount}</span> registros
      </p>

      <div className="flex w-full items-center justify-between gap-1 sm:w-auto sm:justify-end">
        <button
          onClick={() => onPageChange(page - 1)}
          disabled={page === 1}
          className={STEP_BUTTON}
        >
          <ChevronLeft size={16} />
          Anterior
        </button>

        {/* No celular os números não cabem nem acertam o dedo. */}
        <span className="text-sm tabular-nums text-gray-600 dark:text-gray-300 sm:hidden">
          {page} de {totalPages}
        </span>

        <div className="hidden items-center gap-1 sm:flex">
          {pages.map((p, i) =>
            p === '...' ? (
              <span key={`ellipsis-${i}`} className="px-2 py-1.5 text-sm text-gray-500 dark:text-gray-400">…</span>
            ) : (
              <button
                key={p}
                onClick={() => onPageChange(p)}
                aria-current={p === page ? 'page' : undefined}
                className={`min-w-[2rem] px-2 py-1.5 text-sm tabular-nums rounded-md border transition-colors ${
                  p === page
                    ? 'bg-brand-600 border-brand-600 text-white font-medium'
                    : 'border-gray-300 dark:border-white/15 text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-white/5'
                }`}
              >
                {p}
              </button>
            )
          )}
        </div>

        <button
          onClick={() => onPageChange(page + 1)}
          disabled={page === totalPages}
          className={STEP_BUTTON}
        >
          Próxima
          <ChevronRight size={16} />
        </button>
      </div>
    </nav>
  );
}
