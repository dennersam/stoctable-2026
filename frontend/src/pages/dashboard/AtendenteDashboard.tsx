import { useNavigate } from 'react-router-dom';

export function AtendenteDashboard() {
  const navigate = useNavigate();

  return (
    <div>
      <h1 className="text-2xl font-bold text-gray-900 mb-6">Atendimento ao Cliente</h1>
      <div className="flex gap-4">
        <button
          onClick={() => navigate('/quotations/new')}
          className="rounded-lg bg-brand-600 px-6 py-4 text-white font-medium hover:bg-brand-700"
        >
          Novo Orçamento
        </button>
        <button
          onClick={() => navigate('/quotations')}
          className="rounded-lg border bg-white px-6 py-4 text-gray-700 font-medium hover:bg-gray-50"
        >
          Ver Orçamentos
        </button>
      </div>
    </div>
  );
}
