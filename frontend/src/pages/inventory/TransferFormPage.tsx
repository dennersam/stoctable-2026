import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import { Search, Trash2 } from 'lucide-react';
import { productService } from '@/services/productService';
import { stockTransferService } from '@/services/stockTransferService';
import { useBranchStore } from '@/store/branchStore';
import type { Product } from '@/types/product';
import { Button } from '@/components/ui/Button';
import { Field } from '@/components/ui/Field';
import { fieldClass as inputCls } from '@/components/ui/fieldClass';
import { PageHeader } from '@/components/ui/PageHeader';

interface Row {
  productId: string;
  name: string;
  sku: string;
  available: number;
  quantity: number;
}

/**
 * Nova transferência.
 *
 * A origem é sempre a loja da sessão e nem aparece como campo — é a filial do
 * token. O saldo mostrado em cada linha também é o desta loja, que é o que
 * limita o quanto dá para mandar.
 */
export function TransferFormPage() {
  const navigate = useNavigate();
  const { branches, activeBranchId } = useBranchStore();

  const [destination, setDestination] = useState('');
  const [notes, setNotes] = useState('');
  const [rows, setRows] = useState<Row[]>([]);
  const [search, setSearch] = useState('');
  const [results, setResults] = useState<Product[]>([]);
  const [saving, setSaving] = useState(false);

  const origin = branches.find((b) => b.id === activeBranchId);
  const destinations = branches.filter((b) => b.id !== activeBranchId);

  async function handleSearch() {
    if (search.trim().length < 2) return;
    try {
      setResults(await productService.search(search.trim()));
    } catch {
      toast.error('Não foi possível buscar produtos.');
    }
  }

  function addProduct(p: Product) {
    setResults([]);
    setSearch('');

    if (rows.some((r) => r.productId === p.id)) {
      toast.error('Este produto já está na lista.');
      return;
    }

    const available = p.stockQuantity - p.stockReserved;
    if (available <= 0) {
      toast.error(`${p.name} não tem saldo disponível nesta loja.`);
      return;
    }

    setRows((prev) => [
      ...prev,
      { productId: p.id, name: p.name, sku: p.sku, available, quantity: 1 },
    ]);
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();

    if (!destination) {
      toast.error('Escolha a loja de destino.');
      return;
    }
    if (rows.length === 0) {
      toast.error('Adicione ao menos um produto.');
      return;
    }

    setSaving(true);
    try {
      const created = await stockTransferService.create({
        destinationBranchId: destination,
        items: rows.map((r) => ({ productId: r.productId, quantity: r.quantity })),
        notes: notes.trim() || undefined,
      });
      toast.success(`Transferência ${created.transferNumber} criada. Envie quando a carga sair.`);
      navigate('/transfers');
    } catch (err) {
      const msg = (err as { response?: { data?: { detail?: string } } })?.response?.data?.detail;
      toast.error(msg ?? 'Não foi possível criar a transferência.');
      setSaving(false);
    }
  }

  return (
    <form onSubmit={handleSubmit} className="mx-auto max-w-3xl space-y-6">
      <PageHeader
        title="Nova transferência"
        subtitle={<>Saindo de <span className="font-medium">{origin?.name ?? 'esta loja'}</span>. O estoque só é baixado quando você enviar.</>}
      />

      <Field label="Loja de destino" required>
        <select value={destination} onChange={(e) => setDestination(e.target.value)} className={inputCls}>
          <option value="">Selecione…</option>
          {destinations.map((b) => (
            <option key={b.id} value={b.id}>
              {b.name}
            </option>
          ))}
        </select>
        {destinations.length === 0 && (
          <span className="mt-1 block text-xs text-amber-700 dark:text-amber-400">
            Sua conta só tem acesso a uma loja, então não há para onde transferir.
          </span>
        )}
      </Field>

      <div className="space-y-3">
        <p className="text-sm font-medium text-gray-700 dark:text-gray-300">Produtos</p>

        <div className="flex gap-2">
          <input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter') {
                e.preventDefault();
                handleSearch();
              }
            }}
            placeholder="Buscar por nome, SKU ou código de barras…"
            className={inputCls}
          />
          <Button type="button" variant="outline" onClick={handleSearch} aria-label="Buscar" className="shrink-0">
            <Search size={16} />
            <span className="hidden sm:inline">Buscar</span>
          </Button>
        </div>

        {results.length > 0 && (
          <ul className="max-h-52 divide-y divide-gray-100 overflow-y-auto rounded-md border border-gray-200 dark:divide-white/10 dark:border-white/10">
            {results.map((p) => (
              <li key={p.id}>
                <button
                  type="button"
                  onClick={() => addProduct(p)}
                  className="flex min-h-11 w-full items-center justify-between gap-3 px-3 py-2 text-left text-sm hover:bg-gray-50 dark:hover:bg-white/5"
                >
                  <span className="min-w-0 text-gray-900 dark:text-white">{p.name}</span>
                  <span className="ml-3 shrink-0 text-xs text-gray-500 dark:text-gray-400">
                    {p.sku} · disp. {p.stockQuantity - p.stockReserved}
                  </span>
                </button>
              </li>
            ))}
          </ul>
        )}

        {rows.length > 0 && (
          <div className="overflow-x-auto rounded-md border border-line bg-surface">
            <table className="w-full text-sm">
              <thead className="bg-gray-50 text-left text-xs font-medium text-gray-500 dark:bg-white/5 dark:text-gray-400">
                <tr>
                  <th className="px-3 py-2">Produto</th>
                  <th className="hidden px-3 py-2 text-right sm:table-cell">Disponível</th>
                  <th className="px-3 py-2 text-right">Enviar</th>
                  <th className="px-3 py-2" />
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100 dark:divide-white/10">
                {rows.map((r) => (
                  <tr key={r.productId}>
                    <td className="px-3 py-2">
                      <div className="text-gray-900 dark:text-white">{r.name}</div>
                      <div className="text-xs text-gray-500 dark:text-gray-400">{r.sku}<span className="sm:hidden"> · disp. {r.available}</span></div>
                    </td>
                    <td className="hidden px-3 py-2 text-right tabular-nums text-gray-500 sm:table-cell dark:text-gray-400">
                      {r.available}
                    </td>
                    <td className="px-3 py-2 text-right">
                      <input
                        type="number"
                        inputMode="decimal"
                        aria-label={`Quantidade de ${r.name}`}
                        min={1}
                        max={r.available}
                        step="0.01"
                        value={r.quantity}
                        onChange={(e) =>
                          setRows((prev) =>
                            prev.map((x) =>
                              x.productId === r.productId
                                ? {
                                    ...x,
                                    quantity: Math.min(
                                      x.available,
                                      Math.max(0, Number(e.target.value) || 0)
                                    ),
                                  }
                                : x
                            )
                          )
                        }
                        className={inputCls + ' w-20 text-right tabular-nums'}
                      />
                    </td>
                    <td className="px-3 py-2 text-right">
                      <button
                        type="button"
                        onClick={() =>
                          setRows((prev) => prev.filter((x) => x.productId !== r.productId))
                        }
                        aria-label={`Remover ${r.name}`}
                        className="inline-flex h-11 w-11 items-center justify-center rounded text-gray-500 hover:bg-red-50 hover:text-red-700 md:h-8 md:w-8 dark:text-gray-400 dark:hover:bg-red-900/20"
                      >
                        <Trash2 size={16} />
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      <Field label="Observações">
        <textarea
          value={notes}
          onChange={(e) => setNotes(e.target.value)}
          rows={2}
          className={inputCls + ' resize-none'}
          placeholder="Transportadora, responsável, etc. (opcional)"
        />
      </Field>

      <div className="flex justify-end gap-2 max-sm:[&>*]:flex-1">
        <Button type="button" variant="outline" onClick={() => navigate('/transfers')}>Cancelar</Button>
        <Button type="submit" disabled={saving}>{saving ? 'Criando…' : 'Criar transferência'}</Button>
      </div>
    </form>
  );
}
