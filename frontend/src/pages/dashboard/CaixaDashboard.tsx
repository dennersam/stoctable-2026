import { useNavigate } from 'react-router-dom';

export function CaixaDashboard() {
  const navigate = useNavigate();

  return (
    <div>
      <h1 className="text-2xl font-bold text-gray-900 mb-6">Caixa</h1>
      <div className="flex gap-4">
        <button
          onClick={() => navigate('/checkout')}
          className="rounded-lg bg-green-600 px-6 py-4 text-white font-medium hover:bg-green-700"
        >
          Processar Pagamento
        </button>
      </div>
    </div>
  );
}
