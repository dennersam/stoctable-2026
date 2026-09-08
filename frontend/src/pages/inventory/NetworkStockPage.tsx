import { useState } from 'react';
import toast from 'react-hot-toast';
import { Search, Store } from 'lucide-react';
import { productService } from '@/services/productService';
import { inventoryService } from '@/services/inventoryService';
import { useBranchStore } from '@/store/branchStore';
import type { Product } from '@/types/product';
import type { BranchStock } from '@/types/stockTransfer';

const inputCls =
  'w-full rounded-md border border-gray-300 dark:border-white/15 bg-white dark:bg-white/5 text-gray-900 dark:text-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500';

/**
 * "Onde tem esta peça?" — a pergunta que motiva uma transferência.
 *
 * Só leitura: consultar o saldo das outras lojas é legítimo, escrever nele não.
 * As lojas listadas são as que a conta tem acesso, o que o backend confere
 * contra as claims assinadas.
 */
export function NetworkStockPage() {
  const [search, setSearch] = useState('');
  const [results, setResults] = useState<Product[]>([]);
  const [selected, setSelected] = useState<Product | null>(null);
  const [rows, setRows] = useState<BranchStock[]>([]);
  const [loading, setLoading] = useState(false);

  const { branches, activeBranchId } = useBranchStore();
  const branchName = (id: string) => branches.find((b) => b.id === id)?.name ?? 'Loja';

  async function handleSearch() {
    if (search.trim().length < 2) return;
    try {
      setResults(await productService.search(search.trim()));
    } catch {
      toast.error('Não foi possível buscar produtos.');
    }
  }

  async function select(product: Product) {
    setSelected(product);
    setResults([]);
    setSearch('');
    setLoading(true);
    try {
      setRows(await inventoryService.getNetworkStock(product.id));
    } catch {
      toast.error('Não foi possível carregar o estoque da rede.');
    } finally {
      setLoading(false);
    }
  }

  const totals = rows.reduce(
    (acc, r) => ({
      quantity: acc.quantity + r.quantity,
      available: acc.available + r.available,
      inTransit: acc.inTransit + r.inTransit,
    }),
    { quantity: 0, available: 0, inTransit: 0 }
  );

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold text-gray-900 dark:text-white">Estoque na rede</h1>
        <p className="text-sm text-gray-500 dark:text-gray-400">
          Saldo de um produto em todas as lojas. Consulta apenas.
        </p>
      </div>

      <div className="flex gap-2">
        <input
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          onKeyDown={(e) => e.key === 'Enter' && handleSearch()}
          placeholder="Buscar por nome, SKU ou código de barras…"
          className={inputCls}
        />
        <button
          type="button"
          onClick={handleSearch}
          className="inline-flex shrink-0 items-center gap-2 rounded-md border border-gray-300 px-4 text-sm text-gray-700 hover:bg-gray-50 dark:border-white/15 dark:text-gray-300 dark:hover:bg-white/5"
        >
          <Search size={16} />
          Buscar
        </button>
      </div>

      {results.length > 0 && (
        <ul className="max-h-60 divide-y divide-gray-100 overflow-y-auto rounded-md border border-gray-200 dark:divide-white/10 dark:border-white/10">
          {results.map((p) => (
            <li key={p.id}>
              <button
                onClick={() => select(p)}
                className="flex w-full items-center justify-between px-3 py-2 text-left text-sm hover:bg-gray-50 dark:hover:bg-white/5"
              >
                <span className="truncate text-gray-900 dark:text-white">{p.name}</span>
                <span className="ml-3 shrink-0 text-xs text-gray-400">{p.sku}</span>
              </button>
            </li>
          ))}
        </ul>
      )}

      {selected && (
        <div className="space-y-4">
          <div className="rounded-lg border border-gray-200 p-4 dark:border-white/10">
            <p className="font-medium text-gray-900 dark:text-white">{selected.name}</p>
            <p className="text-xs text-gray-400">{selected.sku}</p>
          </div>

          {loading ? (
            <p className="py-8 text-center text-sm text-gray-500 dark:text-gray-400">Carregando…</p>
          ) : (
            <div className="overflow-x-auto rounded-lg border border-gray-200 dark:border-white/10">
              <table className="w-full text-sm">
                <thead className="bg-gray-50 text-left text-xs uppercase tracking-wide text-gray-500 dark:bg-white/5 dark:text-gray-400">
                  <tr>
                    <th className="px-4 py-3">Loja</th>
                    <th className="px-4 py-3 text-right">Total</th>
                    <th className="px-4 py-3 text-right">Reservado</th>
                    <th className="px-4 py-3 text-right">Disponível</th>
                    <th className="px-4 py-3 text-right">Em trânsito</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100 dark:divide-white/10">
                  {rows.map((r) => (
                    <tr
                      key={r.branchId}
                      className={r.branchId === activeBranchId ? 'bg-brand-50/60 dark:bg-brand-900/10' : ''}
                    >
                      <td className="px-4 py-3">
                        <span className="inline-flex items-center gap-2 text-gray-900 dark:text-white">
                          <Store size={14} className="text-gray-400" />
                          {branchName(r.branchId)}
                          {r.branchId === activeBranchId && (
                            <span className="rounded-full bg-brand-100 px-1.5 py-0.5 text-[10px] text-brand-700 dark:bg-brand-900/40 dark:text-brand-300">
                              esta loja
                            </span>
                          )}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-right tabular-nums text-gray-600 dark:text-gray-300">{r.quantity}</td>
                      <td className="px-4 py-3 text-right tabular-nums text-gray-500 dark:text-gray-400">{r.reserved}</td>
                      <td className="px-4 py-3 text-right font-medium tabular-nums text-gray-900 dark:text-white">
                        {r.available}
                      </td>
                      <td className="px-4 py-3 text-right tabular-nums text-gray-500 dark:text-gray-400">
                        {r.inTransit > 0 ? r.inTransit : '—'}
                      </td>
                    </tr>
                  ))}
                </tbody>
                <tfoot className="border-t border-gray-200 bg-gray-50 text-sm font-medium dark:border-white/10 dark:bg-white/5">
                  <tr>
                    <td className="px-4 py-3 text-gray-700 dark:text-gray-200">Total da rede</td>
                    <td className="px-4 py-3 text-right tabular-nums text-gray-700 dark:text-gray-200">{totals.quantity}</td>
                    <td className="px-4 py-3" />
                    <td className="px-4 py-3 text-right tabular-nums text-gray-900 dark:text-white">{totals.available}</td>
                    <td className="px-4 py-3 text-right tabular-nums text-gray-700 dark:text-gray-200">
                      {totals.inTransit > 0 ? totals.inTransit : '—'}
                    </td>
                  </tr>
                </tfoot>
              </table>
            </div>
          )}

          {totals.inTransit > 0 && (
            <p className="text-xs text-gray-500 dark:text-gray-400">
              O que está em trânsito já saiu da loja de origem e ainda não foi conferido no destino,
              por isso não aparece no saldo de nenhuma das duas.
            </p>
          )}
        </div>
      )}
    </div>
  );
}
