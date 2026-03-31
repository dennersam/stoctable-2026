import { useState, useEffect, useCallback } from 'react';
import { Link } from 'react-router-dom';
import toast from 'react-hot-toast';
import { productService } from '@/services/productService';
import type { Product } from '@/types/product';
import { useAuthStore } from '@/store/authStore';

export function ProductListPage() {
  const { user } = useAuthStore();
  const isAdmin = user?.role === 'admin';

  const [products, setProducts] = useState<Product[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [searching, setSearching] = useState(false);

  const loadProducts = useCallback(async () => {
    try {
      setLoading(true);
      const data = await productService.getAll();
      setProducts(Array.isArray(data) ? data : (data as any).items ?? []);
    } catch {
      toast.error('Erro ao carregar produtos.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { loadProducts(); }, [loadProducts]);

  const handleSearch = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!search.trim()) { loadProducts(); return; }
    try {
      setSearching(true);
      const data = await productService.search(search.trim());
      setProducts(data);
    } catch {
      toast.error('Erro na busca.');
    } finally {
      setSearching(false);
    }
  };

  const handleDeactivate = async (id: string, name: string) => {
    if (!confirm(`Desativar produto "${name}"?`)) return;
    try {
      await productService.delete(id);
      toast.success('Produto desativado.');
      loadProducts();
    } catch {
      toast.error('Erro ao desativar produto.');
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
            className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700"
          >
            Novo produto
          </Link>
        )}
      </div>

      <form onSubmit={handleSearch} className="flex gap-2">
        <input
          type="text"
          value={search}
          onChange={e => setSearch(e.target.value)}
          placeholder="Buscar por SKU, nome, código de barras..."
          className="flex-1 rounded-md border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 text-gray-900 dark:text-white placeholder-gray-400 dark:placeholder-gray-500 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
        />
        <button
          type="submit"
          disabled={searching}
          className="rounded-md bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-200 px-4 py-2 text-sm font-medium hover:bg-gray-200 dark:hover:bg-gray-600 disabled:opacity-50"
        >
          {searching ? 'Buscando...' : 'Buscar'}
        </button>
        {search && (
          <button
            type="button"
            onClick={() => { setSearch(''); loadProducts(); }}
            className="rounded-md border border-gray-300 dark:border-gray-600 text-gray-700 dark:text-gray-300 px-4 py-2 text-sm hover:bg-gray-50 dark:hover:bg-gray-800"
          >
            Limpar
          </button>
        )}
      </form>

      {loading ? (
        <div className="py-12 text-center text-gray-500 dark:text-gray-400">Carregando...</div>
      ) : (
        <div className="overflow-hidden rounded-lg border border-gray-200 dark:border-gray-700">
          <table className="min-w-full divide-y divide-gray-200 dark:divide-gray-700">
            <thead className="bg-gray-50 dark:bg-gray-800">
              <tr>
                {['SKU', 'Nome', 'Preço', 'Estoque disp.', 'Status'].map(h => (
                  <th key={h} className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500 dark:text-gray-400">
                    {h}
                  </th>
                ))}
                {isAdmin && <th className="px-4 py-3" />}
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100 dark:divide-gray-700 bg-white dark:bg-gray-900">
              {products.length === 0 ? (
                <tr>
                  <td colSpan={isAdmin ? 6 : 5} className="py-8 text-center text-gray-400 dark:text-gray-500">
                    Nenhum produto encontrado.
                  </td>
                </tr>
              ) : (
                products.map(p => (
                  <tr key={p.id} className="hover:bg-gray-50 dark:hover:bg-gray-800/50">
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
                      <td className="px-4 py-3 text-right text-sm">
                        <Link to={`/products/${p.id}/edit`} className="mr-3 text-blue-600 dark:text-blue-400 hover:underline">
                          Editar
                        </Link>
                        {p.isActive && (
                          <button
                            onClick={() => handleDeactivate(p.id, p.name)}
                            className="text-red-500 dark:text-red-400 hover:underline"
                          >
                            Desativar
                          </button>
                        )}
                      </td>
                    )}
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
