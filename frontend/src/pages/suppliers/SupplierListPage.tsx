import { useState, useEffect, useCallback } from 'react';
import { Link } from 'react-router-dom';
import toast from 'react-hot-toast';
import { supplierService, type Supplier } from '@/services/supplierService';

export function SupplierListPage() {
  const [suppliers, setSuppliers] = useState<Supplier[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const data = await supplierService.getAll();
      setSuppliers(data);
    } catch {
      toast.error('Erro ao carregar fornecedores.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { load(); }, [load]);

  const filtered = suppliers.filter(s =>
    !search ||
    s.companyName.toLowerCase().includes(search.toLowerCase()) ||
    (s.tradeName ?? '').toLowerCase().includes(search.toLowerCase()) ||
    (s.cnpj ?? '').includes(search)
  );

  const handleDeactivate = async (s: Supplier) => {
    if (!confirm(`Desativar fornecedor "${s.companyName}"?`)) return;
    try {
      await supplierService.deactivate(s.id);
      toast.success('Fornecedor desativado.');
      load();
    } catch {
      toast.error('Erro ao desativar fornecedor.');
    }
  };

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold text-gray-900 dark:text-white">Fornecedores</h1>
        <Link
          to="/suppliers/new"
          className="rounded-md bg-blue-600 hover:bg-blue-700 px-4 py-2 text-sm font-medium text-white"
        >
          Novo fornecedor
        </Link>
      </div>

      <input
        value={search}
        onChange={e => setSearch(e.target.value)}
        placeholder="Buscar por razão social, fantasia ou CNPJ..."
        className="w-full max-w-sm rounded-md border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 text-gray-900 dark:text-white placeholder-gray-400 dark:placeholder-gray-500 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
      />

      {loading ? (
        <div className="py-12 text-center text-gray-500 dark:text-gray-400">Carregando...</div>
      ) : (
        <div className="overflow-x-auto rounded-lg border border-gray-200 dark:border-gray-700">
          <table className="min-w-full divide-y divide-gray-200 dark:divide-gray-700">
            <thead className="bg-gray-50 dark:bg-gray-800">
              <tr>
                {['Razão social', 'Nome fantasia', 'CNPJ', 'Telefone', 'Contato', 'Status', ''].map(h => (
                  <th key={h} className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500 dark:text-gray-400">
                    {h}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100 dark:divide-gray-700 bg-white dark:bg-gray-900">
              {filtered.length === 0 ? (
                <tr>
                  <td colSpan={7} className="py-8 text-center text-gray-400 dark:text-gray-500">
                    Nenhum fornecedor encontrado.
                  </td>
                </tr>
              ) : filtered.map(s => (
                <tr key={s.id} className="hover:bg-gray-50 dark:hover:bg-gray-800/40 transition-colors">
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
                  <td className="px-4 py-3 text-right text-sm whitespace-nowrap">
                    <Link to={`/suppliers/${s.id}/edit`} className="mr-3 text-blue-600 dark:text-blue-400 hover:underline">
                      Editar
                    </Link>
                    {s.isActive && (
                      <button onClick={() => handleDeactivate(s)} className="text-red-500 dark:text-red-400 hover:underline">
                        Desativar
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
