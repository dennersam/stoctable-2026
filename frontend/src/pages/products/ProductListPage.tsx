import { useState, useEffect, useRef } from 'react';
import { Link } from 'react-router-dom';
import toast from 'react-hot-toast';
import { Pencil, PowerOff, Trash2 } from 'lucide-react';
import { productService } from '@/services/productService';
import type { Product } from '@/types/product';
import { useAuthStore } from '@/store/authStore';
import { Pagination } from '@/components/base/Pagination';

const PAGE_SIZE = 20;

export function ProductListPage() {
  const { user } = useAuthStore();
  const isAdmin = user?.role === 'admin';

  const [products, setProducts] = useState<Product[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(1);

  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const loadProducts = async (currentPage: number, currentSearch: string) => {
    try {
      setLoading(true);
      const data = await productService.getAll({
        page: currentPage,
        pageSize: PAGE_SIZE,
        search: currentSearch || undefined,
      });
      setProducts(data.items);
      setTotalCount(data.totalCount);
      setTotalPages(data.totalPages);
    } catch {
      toast.error('Erro ao carregar produtos.');
    } finally {
      setLoading(false);
    }
  };

  // Load on page change
  useEffect(() => {
    loadProducts(page, search);
  }, [page]); // eslint-disable-line react-hooks/exhaustive-deps

  // Debounce search: reset to page 1
  useEffect(() => {
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => {
      setPage(1);
      loadProducts(1, search);
    }, 400);
    return () => { if (debounceRef.current) clearTimeout(debounceRef.current); };
  }, [search]); // eslint-disable-line react-hooks/exhaustive-deps

  const handleDeactivate = async (id: string, name: string) => {
    if (!confirm(`Desativar produto "${name}"?`)) return;
    try {
      await productService.delete(id);
      toast.success('Produto desativado.');
      loadProducts(page, search);
    } catch {
      toast.error('Erro ao desativar produto.');
    }
  };

  const handleHardDelete = async (id: string, name: string) => {
    if (!confirm(`Excluir permanentemente o produto "${name}"?\n\nEsta ação não pode ser desfeita.`)) return;
    try {
      await productService.hardDelete(id);
      toast.success('Produto excluído.');
      loadProducts(page, search);
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { detail?: string } } })?.response?.data?.detail;
      toast.error(msg ?? 'Não é possível excluir este produto.');
    }
  };

  const stockStatus = (p: Product) => {
    const available = p.stockQuantity - p.stockReserved;
    if (available <= 0) return 'text-red-600 dark:text-red-400 font-semibold';
    if (available <= p.stockMinimum) return 'text-yellow-600 dark:text-yellow-400 font-semibold';
    return 'text-green-700 dark:text-green-400';
  };

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold text-gray-900 dark:text-white">Produtos</h1>
        {isAdmin && (
          <Link
            to="/products/new"
            className="rounded-md bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700"
          >
            Novo produto
          </Link>
        )}
      </div>

      <input
        type="text"
        value={search}
        onChange={e => setSearch(e.target.value)}
        placeholder="Buscar por SKU, nome, código de barras, fabricante..."
        className="w-full rounded-md border border-gray-300 dark:border-brand-700/50 bg-white dark:bg-brand-900/30 text-gray-900 dark:text-white placeholder-gray-400 dark:placeholder-brand-300/40 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500"
      />

      {loading ? (
        <div className="py-12 text-center text-gray-500 dark:text-gray-400">Carregando...</div>
      ) : (
        <div className="space-y-3">
          <div className="overflow-hidden rounded-lg border border-gray-200 dark:border-brand-800/50">
            <table className="min-w-full divide-y divide-gray-200 dark:divide-brand-800/40">
              <thead className="bg-gray-50 dark:bg-brand-900/40">
                <tr>
                  {['SKU', 'Nome', 'Preço', 'Estoque disp.', 'Status'].map(h => (
                    <th key={h} className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500 dark:text-brand-300/70">
                      {h}
                    </th>
                  ))}
                  {isAdmin && <th className="px-4 py-3" />}
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100 dark:divide-brand-800/40 bg-white dark:bg-brand-950">
                {products.length === 0 ? (
                  <tr>
                    <td colSpan={isAdmin ? 6 : 5} className="py-8 text-center text-gray-400 dark:text-gray-500">
                      Nenhum produto encontrado.
                    </td>
                  </tr>
                ) : (
                  products.map(p => (
                    <tr key={p.id} className="hover:bg-gray-50 dark:hover:bg-brand-800/20">
                      <td className="px-4 py-3 font-mono text-sm text-gray-700 dark:text-gray-300">{p.sku}</td>
                      <td className="px-4 py-3">
                        <div className="font-medium text-gray-900 dark:text-white">{p.name}</div>
                        {p.manufacturerName && <div className="text-xs text-gray-400 dark:text-gray-500">{p.manufacturerName}</div>}
                      </td>
                      <td className="px-4 py-3 text-sm text-gray-700 dark:text-gray-300">
                        R$ {p.salePrice.toFixed(2)}
                      </td>
                      <td className={`px-4 py-3 text-sm ${stockStatus(p)}`}>
                        {(p.stockQuantity - p.stockReserved).toFixed(0)} {p.unit}
                        {p.stockReserved > 0 && (
                          <span className="ml-1 text-xs text-gray-400 dark:text-gray-500">({p.stockReserved} res.)</span>
                        )}
                      </td>
                      <td className="px-4 py-3">
                        <span className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium ${
                          p.isActive
                            ? 'bg-green-100 dark:bg-green-900/40 text-green-700 dark:text-green-400'
                            : 'bg-gray-100 dark:bg-gray-700 text-gray-500 dark:text-gray-400'
                        }`}>
                          {p.isActive ? 'Ativo' : 'Inativo'}
                        </span>
                      </td>
                      {isAdmin && (
                        <td className="px-4 py-3 text-right whitespace-nowrap">
                          <div className="inline-flex items-center gap-1">
                            <Link
                              to={`/products/${p.id}/edit`}
                              title="Editar"
                              className="rounded p-1.5 text-gray-400 hover:bg-brand-50 hover:text-brand-600 dark:hover:bg-brand-900/30 dark:hover:text-brand-400 transition-colors"
                            >
                              <Pencil size={15} />
                            </Link>
                            {p.isActive && (
                              <button
                                onClick={() => handleDeactivate(p.id, p.name)}
                                title="Desativar"
                                className="rounded p-1.5 text-gray-400 hover:bg-amber-50 hover:text-amber-600 dark:hover:bg-amber-900/30 dark:hover:text-amber-400 transition-colors"
                              >
                                <PowerOff size={15} />
                              </button>
                            )}
                            <button
                              onClick={() => handleHardDelete(p.id, p.name)}
                              title="Excluir permanentemente"
                              className="rounded p-1.5 text-gray-400 hover:bg-red-50 hover:text-red-600 dark:hover:bg-red-900/30 dark:hover:text-red-400 transition-colors"
                            >
                              <Trash2 size={15} />
                            </button>
                          </div>
                        </td>
                      )}
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>

          <Pagination
            page={page}
            totalPages={totalPages}
            totalCount={totalCount}
            pageSize={PAGE_SIZE}
            onPageChange={setPage}
          />
        </div>
      )}
    </div>
  );
}
