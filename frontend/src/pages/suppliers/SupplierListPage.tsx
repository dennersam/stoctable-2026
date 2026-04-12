import { useState, useEffect, useRef } from 'react';
import { Link } from 'react-router-dom';
import toast from 'react-hot-toast';
import { Pencil, PowerOff, Trash2 } from 'lucide-react';
import { supplierService, type Supplier } from '@/services/supplierService';
import { Pagination } from '@/components/base/Pagination';
import { useAuthStore } from '@/store/authStore';

const PAGE_SIZE = 20;

export function SupplierListPage() {
  const { user } = useAuthStore();
  const isAdmin = user?.role === 'admin';

  const [suppliers, setSuppliers] = useState<Supplier[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(1);
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const loadSuppliers = async (p: number, s: string) => {
    setLoading(true);
    try {
      const data = await supplierService.getAll({ page: p, pageSize: PAGE_SIZE, search: s || undefined });
      setSuppliers(data.items);
      setTotalCount(data.totalCount);
      setTotalPages(data.totalPages);
    } catch {
      toast.error('Erro ao carregar fornecedores.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadSuppliers(page, search);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page]);

  useEffect(() => {
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => {
      setPage(1);
      loadSuppliers(1, search);
    }, 400);
    return () => { if (debounceRef.current) clearTimeout(debounceRef.current); };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [search]);

  const handleDeactivate = async (s: Supplier) => {
    if (!confirm(`Desativar fornecedor "${s.companyName}"?`)) return;
    try {
      await supplierService.deactivate(s.id);
      toast.success('Fornecedor desativado.');
      loadSuppliers(page, search);
    } catch {
      toast.error('Erro ao desativar fornecedor.');
    }
  };

  const handleHardDelete = async (s: Supplier) => {
    if (!confirm(`Excluir permanentemente o fornecedor "${s.companyName}"?\n\nEsta ação não pode ser desfeita.`)) return;
    try {
      await supplierService.hardDelete(s.id);
      toast.success('Fornecedor excluído.');
      loadSuppliers(page, search);
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { detail?: string } } })?.response?.data?.detail;
      toast.error(msg ?? 'Não é possível excluir este fornecedor.');
    }
  };

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold text-gray-900 dark:text-white">Fornecedores</h1>
        <Link
          to="/suppliers/new"
          className="rounded-md bg-brand-600 hover:bg-brand-700 px-4 py-2 text-sm font-medium text-white"
        >
          Novo fornecedor
        </Link>
      </div>

      <input
        value={search}
        onChange={e => setSearch(e.target.value)}
        placeholder="Buscar por razão social, fantasia, CNPJ ou telefone..."
        className="w-full max-w-sm rounded-md border border-gray-300 dark:border-brand-700/50 bg-white dark:bg-brand-900/30 text-gray-900 dark:text-white placeholder-gray-400 dark:placeholder-brand-300/40 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500"
      />

      {loading ? (
        <div className="py-12 text-center text-gray-500 dark:text-gray-400">Carregando...</div>
      ) : (
        <>
          <div className="overflow-x-auto rounded-lg border border-gray-200 dark:border-brand-800/50">
            <table className="min-w-full divide-y divide-gray-200 dark:divide-brand-800/40">
              <thead className="bg-gray-50 dark:bg-brand-900/40">
                <tr>
                  {['Razão social', 'Nome fantasia', 'CNPJ', 'Telefone', 'Contato', 'Status', ''].map(h => (
                    <th key={h} className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500 dark:text-brand-300/70">
                      {h}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100 dark:divide-brand-800/40 bg-white dark:bg-brand-950">
                {suppliers.length === 0 ? (
                  <tr>
                    <td colSpan={7} className="py-8 text-center text-gray-400 dark:text-gray-500">
                      Nenhum fornecedor encontrado.
                    </td>
                  </tr>
                ) : suppliers.map(s => (
                  <tr key={s.id} className="hover:bg-gray-50 dark:hover:bg-brand-800/20 transition-colors">
                    <td className="px-4 py-3 font-medium text-gray-900 dark:text-white">{s.companyName}</td>
                    <td className="px-4 py-3 text-sm text-gray-600 dark:text-gray-300">
                      {s.tradeName ?? <span className="text-gray-400">—</span>}
                    </td>
                    <td className="px-4 py-3 text-sm font-mono text-gray-600 dark:text-gray-300">
                      {s.cnpj ?? <span className="text-gray-400">—</span>}
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-600 dark:text-gray-300">
                      {s.phone ?? <span className="text-gray-400">—</span>}
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-600 dark:text-gray-300">
                      {s.contactPerson ?? <span className="text-gray-400">—</span>}
                    </td>
                    <td className="px-4 py-3">
                      <span className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium ${
                        s.isActive
                          ? 'bg-green-100 dark:bg-green-900/40 text-green-700 dark:text-green-400'
                          : 'bg-gray-100 dark:bg-gray-700 text-gray-500 dark:text-gray-400'
                      }`}>
                        {s.isActive ? 'Ativo' : 'Inativo'}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-right whitespace-nowrap">
                      <div className="inline-flex items-center gap-1">
                        <Link
                          to={`/suppliers/${s.id}/edit`}
                          title="Editar"
                          className="rounded p-1.5 text-gray-400 hover:bg-brand-50 hover:text-brand-600 dark:hover:bg-brand-900/30 dark:hover:text-brand-400 transition-colors"
                        >
                          <Pencil size={15} />
                        </Link>
                        {s.isActive && (
                          <button
                            onClick={() => handleDeactivate(s)}
                            title="Desativar"
                            className="rounded p-1.5 text-gray-400 hover:bg-amber-50 hover:text-amber-600 dark:hover:bg-amber-900/30 dark:hover:text-amber-400 transition-colors"
                          >
                            <PowerOff size={15} />
                          </button>
                        )}
                        {isAdmin && (
                          <button
                            onClick={() => handleHardDelete(s)}
                            title="Excluir permanentemente"
                            className="rounded p-1.5 text-gray-400 hover:bg-red-50 hover:text-red-600 dark:hover:bg-red-900/30 dark:hover:text-red-400 transition-colors"
                          >
                            <Trash2 size={15} />
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
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
        </>
      )}
    </div>
  );
}
