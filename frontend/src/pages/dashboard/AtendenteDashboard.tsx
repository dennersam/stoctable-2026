import { useNavigate } from 'react-router-dom';

export function AtendenteDashboard() {
  const navigate = useNavigate();

  return (
    <div className="space-y-5">
      <h1 className="text-2xl font-bold text-gray-900 dark:text-white">Atendimento ao Cliente</h1>
      <div className="flex gap-4">
        <button
          onClick={() => navigate('/quotations/new')}
          className="rounded-lg bg-brand-600 px-6 py-4 text-white font-medium hover:bg-brand-700"
        >
          Novo Orçamento
        </button>
        <button
          onClick={() => navigate('/quotations')}
          className="rounded-lg border border-gray-200 dark:border-brand-800/50 bg-white dark:bg-brand-900/20 px-6 py-4 text-gray-700 dark:text-gray-200 font-medium hover:bg-gray-50 dark:hover:bg-brand-800/30"
        >
          Ver Orçamentos
        </button>
      </div>
    </div>
  );
}
