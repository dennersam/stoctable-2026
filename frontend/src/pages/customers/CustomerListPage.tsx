import { useState, useEffect, useCallback } from 'react';
import { Link } from 'react-router-dom';
import toast from 'react-hot-toast';
import { customerService } from '@/services/customerService';
import type { Customer } from '@/types/customer';

export function CustomerListPage() {
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [searching, setSearching] = useState(false);

  const loadCustomers = useCallback(async () => {
    try {
      setLoading(true);
      const data = await customerService.getAll();
      setCustomers(Array.isArray(data) ? data : (data as any).items ?? []);
    } catch {
      toast.error('Erro ao carregar clientes.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { loadCustomers(); }, [loadCustomers]);

  const handleSearch = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!search.trim()) { loadCustomers(); return; }
    try {
      setSearching(true);
      const data = await customerService.getAll({ search: search.trim() });
      setCustomers(Array.isArray(data) ? data : (data as any).items ?? []);
    } catch {
      toast.error('Erro na busca.');
    } finally {
      setSearching(false);
    }
  };

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold text-gray-900">Clientes</h1>
        <Link
          to="/customers/new"
          className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700"
        >
          Novo cliente
        </Link>
      </div>

      <form onSubmit={handleSearch} className="flex gap-2">
        <input
          type="text"
          value={search}
          onChange={e => setSearch(e.target.value)}
          placeholder="Buscar por nome, CPF/CNPJ, telefone..."
          className="flex-1 rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
        />
        <button
          type="submit"
          disabled={searching}
          className="rounded-md bg-gray-100 px-4 py-2 text-sm font-medium hover:bg-gray-200 disabled:opacity-50"
        >
          {searching ? 'Buscando...' : 'Buscar'}
        </button>
        {search && (
          <button
            type="button"
            onClick={() => { setSearch(''); loadCustomers(); }}
            className="rounded-md border px-4 py-2 text-sm hover:bg-gray-50"
          >
            Limpar
          </button>
        )}
      </form>

      {loading ? (
        <div className="py-12 text-center text-gray-500">Carregando...</div>
      ) : (
        <div className="overflow-hidden rounded-lg border border-gray-200">
          <table className="min-w-full divide-y divide-gray-200">
            <thead className="bg-gray-50">
              <tr>
                <th className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500">Nome</th>
                <th className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500">CPF/CNPJ</th>
                <th className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500">Telefone</th>
                <th className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500">Tipo</th>
                <th className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500">Status</th>
                <th className="px-4 py-3" />
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100 bg-white">
              {customers.length === 0 ? (
                <tr>
                  <td colSpan={6} className="py-8 text-center text-gray-400">
                    Nenhum cliente encontrado.
                  </td>
                </tr>
              ) : (
                customers.map(c => (
                  <tr key={c.id} className="hover:bg-gray-50">
                    <td className="px-4 py-3 font-medium text-gray-900">{c.fullName}</td>
                    <td className="px-4 py-3 text-sm text-gray-600">
                      {c.documentNumber
                        ? `${c.documentType}: ${c.documentNumber}`
                        : <span className="text-gray-400">—</span>}
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-600">
                      {c.mobile || c.phone || <span className="text-gray-400">—</span>}
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-600">
                      {c.customerTypeName || <span className="text-gray-400">—</span>}
                    </td>
                    <td className="px-4 py-3">
                      <span className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium ${
                        c.isActive ? 'bg-green-100 text-green-700' : 'bg-gray-100 text-gray-500'
                      }`}>
                        {c.isActive ? 'Ativo' : 'Inativo'}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-right text-sm">
                      <Link to={`/customers/${c.id}`} className="text-blue-600 hover:underline">
                        Ver / CRM
                      </Link>
                    </td>
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
