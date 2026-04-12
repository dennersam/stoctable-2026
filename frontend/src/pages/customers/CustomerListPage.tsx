import { useState, useEffect, useRef } from 'react';
import { Link } from 'react-router-dom';
import toast from 'react-hot-toast';
import { Eye, Pencil, PowerOff, Trash2 } from 'lucide-react';
import { customerService } from '@/services/customerService';
import type { Customer } from '@/types/customer';
import { useAuthStore } from '@/store/authStore';
import { Pagination } from '@/components/base/Pagination';

const PAGE_SIZE = 20;

export function CustomerListPage() {
  const { user } = useAuthStore();
  const isAdmin = user?.role === 'admin';

  const [customers, setCustomers] = useState<Customer[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(1);

  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const loadCustomers = async (currentPage: number, currentSearch: string) => {
    try {
      setLoading(true);
      const data = await customerService.getAll({
        page: currentPage,
        pageSize: PAGE_SIZE,
        search: currentSearch || undefined,
      });
      setCustomers(data.items);
      setTotalCount(data.totalCount);
      setTotalPages(data.totalPages);
    } catch {
      toast.error('Erro ao carregar clientes.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadCustomers(page, search);
  }, [page]); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => {
      setPage(1);
      loadCustomers(1, search);
    }, 400);
    return () => { if (debounceRef.current) clearTimeout(debounceRef.current); };
  }, [search]); // eslint-disable-line react-hooks/exhaustive-deps

  const handleDeactivate = async (id: string, name: string) => {
    if (!confirm(`Desativar cliente "${name}"?`)) return;
    try {
      await customerService.deactivate(id);
      toast.success('Cliente desativado.');
      loadCustomers(page, search);
    } catch {
      toast.error('Erro ao desativar cliente.');
    }
  };

  const handleHardDelete = async (id: string, name: string) => {
    if (!confirm(`Excluir permanentemente o cliente "${name}"?\n\nEsta ação não pode ser desfeita.`)) return;
    try {
      await customerService.hardDelete(id);
      toast.success('Cliente excluído.');
      loadCustomers(page, search);
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { detail?: string } } })?.response?.data?.detail;
      toast.error(msg ?? 'Não é possível excluir este cliente.');
    }
  };

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold text-gray-900 dark:text-white">Clientes</h1>
        <Link
          to="/customers/new"
          className="rounded-md bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700"
        >
          Novo cliente
        </Link>
      </div>

      <input
        type="text"
        value={search}
        onChange={e => setSearch(e.target.value)}
        placeholder="Buscar por nome, CPF/CNPJ, telefone, cidade..."
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
                  {['Nome', 'CPF/CNPJ', 'Telefone', 'Cidade', 'Status'].map(h => (
                    <th key={h} className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500 dark:text-brand-300/70">{h}</th>
                  ))}
                  {isAdmin && <th className="px-4 py-3" />}
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100 dark:divide-brand-800/40 bg-white dark:bg-brand-950">
                {customers.length === 0 ? (
                  <tr>
                    <td colSpan={isAdmin ? 6 : 5} className="py-8 text-center text-gray-400 dark:text-gray-500">
                      Nenhum cliente encontrado.
                    </td>
                  </tr>
                ) : customers.map(c => (
                  <tr key={c.id} className="hover:bg-gray-50 dark:hover:bg-brand-800/20 transition-colors">
                    <td className="px-4 py-3 font-medium text-gray-900 dark:text-white">{c.fullName}</td>
                    <td className="px-4 py-3 text-sm text-gray-600 dark:text-gray-300">
                      {c.documentNumber ? `${c.documentType}: ${c.documentNumber}` : <span className="text-gray-400">—</span>}
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-600 dark:text-gray-300">
                      {c.mobile || c.phone || <span className="text-gray-400">—</span>}
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-600 dark:text-gray-300">
                      {c.city ? `${c.city}${c.state ? ` / ${c.state}` : ''}` : <span className="text-gray-400">—</span>}
                    </td>
                    <td className="px-4 py-3">
                      <span className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium ${
                        c.isActive
                          ? 'bg-green-100 dark:bg-green-900/40 text-green-700 dark:text-green-400'
                          : 'bg-gray-100 dark:bg-gray-700 text-gray-500 dark:text-gray-400'
                      }`}>
                        {c.isActive ? 'Ativo' : 'Inativo'}
                      </span>
                    </td>
                    {isAdmin && (
                      <td className="px-4 py-3 text-right whitespace-nowrap">
                        <div className="inline-flex items-center gap-1">
                          <Link
                            to={`/customers/${c.id}`}
                            title="Ver detalhes"
                            className="rounded p-1.5 text-gray-400 hover:bg-brand-50 hover:text-brand-600 dark:hover:bg-brand-900/30 dark:hover:text-brand-400 transition-colors"
                          >
                            <Eye size={15} />
                          </Link>
                          <Link
                            to={`/customers/${c.id}/edit`}
                            title="Editar"
                            className="rounded p-1.5 text-gray-400 hover:bg-brand-50 hover:text-brand-600 dark:hover:bg-brand-900/30 dark:hover:text-brand-400 transition-colors"
                          >
                            <Pencil size={15} />
                          </Link>
                          {c.isActive && (
                            <button
                              onClick={() => handleDeactivate(c.id, c.fullName)}
                              title="Desativar"
                              className="rounded p-1.5 text-gray-400 hover:bg-amber-50 hover:text-amber-600 dark:hover:bg-amber-900/30 dark:hover:text-amber-400 transition-colors"
                            >
                              <PowerOff size={15} />
                            </button>
                          )}
                          <button
                            onClick={() => handleHardDelete(c.id, c.fullName)}
                            title="Excluir permanentemente"
                            className="rounded p-1.5 text-gray-400 hover:bg-red-50 hover:text-red-600 dark:hover:bg-red-900/30 dark:hover:text-red-400 transition-colors"
                          >
                            <Trash2 size={15} />
                          </button>
                        </div>
                      </td>
                    )}
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
        </div>
      )}
    </div>
  );
}
