import { useState, useEffect, useRef } from 'react';
import toast from 'react-hot-toast';
import { manufacturerService } from '@/services/manufacturerService';
import type { Manufacturer } from '@/types/manufacturer';
import { Pagination } from '@/components/base/Pagination';

const PAGE_SIZE = 20;

const inputCls =
  'w-full rounded-md border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 text-gray-900 dark:text-white placeholder-gray-400 dark:placeholder-gray-500 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500';

// ─── Modal de cadastro / edição ───────────────────────────────────────────────

interface ModalProps {
  manufacturer?: Manufacturer;
  onClose: () => void;
  onSaved: () => void;
}

function ManufacturerModal({ manufacturer, onClose, onSaved }: ModalProps) {
  const isEdit = Boolean(manufacturer);
  const [name, setName] = useState(manufacturer?.name ?? '');
  const [notes, setNotes] = useState(manufacturer?.notes ?? '');
  const [saving, setSaving] = useState(false);

  async function handleSave() {
    if (!name.trim()) { toast.error('Nome é obrigatório.'); return; }
    setSaving(true);
    try {
      if (isEdit && manufacturer) {
        await manufacturerService.update(manufacturer.id, { name: name.trim(), notes: notes.trim() || undefined });
        toast.success('Fabricante atualizado.');
      } else {
        await manufacturerService.create({ name: name.trim(), notes: notes.trim() || undefined });
        toast.success('Fabricante cadastrado.');
      }
      onSaved();
      onClose();
    } catch {
      toast.error('Erro ao salvar fabricante.');
    } finally {
      setSaving(false);
    }
  }

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4"
      onClick={(e) => { if (e.target === e.currentTarget) onClose(); }}
    >
      <div className="w-full max-w-sm rounded-xl bg-white dark:bg-gray-900 shadow-xl">
        <div className="flex items-center justify-between border-b border-gray-200 dark:border-gray-700 px-5 py-4">
          <h2 className="font-semibold text-gray-900 dark:text-white">
            {isEdit ? 'Editar Fabricante' : 'Novo Fabricante'}
          </h2>
          <button
            onClick={onClose}
            className="rounded-md p-1 text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800 hover:text-gray-600"
          >
            ✕
          </button>
        </div>
        <div className="p-5 space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              Nome <span className="text-red-500">*</span>
            </label>
            <input
              autoFocus
              value={name}
              onChange={(e) => setName(e.target.value)}
              onKeyDown={(e) => e.key === 'Enter' && handleSave()}
              placeholder="Ex: Pirelli, Honda, NGK..."
              className={inputCls}
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              Observações
            </label>
            <textarea
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              rows={2}
              placeholder="Informações adicionais (opcional)"
              className={`${inputCls} resize-none`}
            />
          </div>
          <div className="flex gap-2 justify-end pt-1">
            <button
              onClick={onClose}
              className="rounded-md border border-gray-300 dark:border-gray-600 px-4 py-2 text-sm text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-800"
            >
              Cancelar
            </button>
            <button
              onClick={handleSave}
              disabled={saving}
              className="rounded-md bg-blue-600 hover:bg-blue-700 px-4 py-2 text-sm font-medium text-white disabled:opacity-50"
            >
              {saving ? 'Salvando...' : isEdit ? 'Salvar' : 'Cadastrar'}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

// ─── main page ───────────────────────────────────────────────────────────────

export function ManufacturerListPage() {
  const [manufacturers, setManufacturers] = useState<Manufacturer[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(1);
  const [modal, setModal] = useState<'new' | Manufacturer | null>(null);
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const loadManufacturers = async (p: number, s: string) => {
    setLoading(true);
    try {
      const data = await manufacturerService.getAll({ page: p, pageSize: PAGE_SIZE, search: s || undefined });
      setManufacturers(data.items);
      setTotalCount(data.totalCount);
      setTotalPages(data.totalPages);
    } catch {
      toast.error('Erro ao carregar fabricantes.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadManufacturers(page, search);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page]);

  useEffect(() => {
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => {
      setPage(1);
      loadManufacturers(1, search);
    }, 400);
    return () => { if (debounceRef.current) clearTimeout(debounceRef.current); };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [search]);

  const handleDeactivate = async (m: Manufacturer) => {
    if (!confirm(`Desativar fabricante "${m.name}"?`)) return;
    try {
      await manufacturerService.deactivate(m.id);
      toast.success('Fabricante desativado.');
      loadManufacturers(page, search);
    } catch {
      toast.error('Erro ao desativar fabricante.');
    }
  };

  return (
    <>
      <div className="space-y-4">
        {/* Header */}
        <div className="flex items-center justify-between">
          <h1 className="text-2xl font-bold text-gray-900 dark:text-white">Fabricantes</h1>
          <button
            onClick={() => setModal('new')}
            className="rounded-md bg-blue-600 hover:bg-blue-700 px-4 py-2 text-sm font-medium text-white"
          >
            Novo fabricante
          </button>
        </div>

        {/* Search */}
        <input
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder="Buscar por nome..."
          className="w-full max-w-sm rounded-md border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 text-gray-900 dark:text-white placeholder-gray-400 dark:placeholder-gray-500 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
        />

        {/* Table */}
        {loading ? (
          <div className="py-12 text-center text-gray-500 dark:text-gray-400">Carregando...</div>
        ) : (
          <>
            <div className="overflow-hidden rounded-lg border border-gray-200 dark:border-gray-700">
              <table className="min-w-full divide-y divide-gray-200 dark:divide-gray-700">
                <thead className="bg-gray-50 dark:bg-gray-800">
                  <tr>
                    {['Nome', 'Observações', 'Status', ''].map((h) => (
                      <th key={h} className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500 dark:text-gray-400">
                        {h}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100 dark:divide-gray-700 bg-white dark:bg-gray-900">
                  {manufacturers.length === 0 ? (
                    <tr>
                      <td colSpan={4} className="py-8 text-center text-gray-400 dark:text-gray-500">
                        Nenhum fabricante encontrado.
                      </td>
                    </tr>
                  ) : (
                    manufacturers.map((m) => (
                      <tr key={m.id} className="hover:bg-gray-50 dark:hover:bg-gray-800/40 transition-colors">
                        <td className="px-4 py-3 font-medium text-gray-900 dark:text-white">{m.name}</td>
                        <td className="px-4 py-3 text-sm text-gray-500 dark:text-gray-400 max-w-xs truncate">
                          {m.notes ?? '—'}
                        </td>
                        <td className="px-4 py-3">
                          <span className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium ${
                            m.isActive
                              ? 'bg-green-100 dark:bg-green-900/40 text-green-700 dark:text-green-400'
                              : 'bg-gray-100 dark:bg-gray-700 text-gray-500 dark:text-gray-400'
                          }`}>
                            {m.isActive ? 'Ativo' : 'Inativo'}
                          </span>
                        </td>
                        <td className="px-4 py-3 text-right text-sm">
                          <button
                            onClick={() => setModal(m)}
                            className="mr-3 text-blue-600 dark:text-blue-400 hover:underline"
                          >
                            Editar
                          </button>
                          {m.isActive && (
                            <button
                              onClick={() => handleDeactivate(m)}
                              className="text-red-500 dark:text-red-400 hover:underline"
                            >
                              Desativar
                            </button>
                          )}
                        </td>
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
          </>
        )}
      </div>

      {modal !== null && (
        <ManufacturerModal
          manufacturer={modal === 'new' ? undefined : modal}
          onClose={() => setModal(null)}
          onSaved={() => loadManufacturers(page, search)}
        />
      )}
    </>
  );
}
