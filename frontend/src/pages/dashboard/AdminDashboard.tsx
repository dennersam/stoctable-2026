export function AdminDashboard() {
  return (
    <div>
      <h1 className="text-2xl font-bold text-gray-900 mb-6">Dashboard Administrativo</h1>
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {['Vendas Hoje', 'Orçamentos Abertos', 'Produtos com Estoque Baixo', 'Caixa do Dia'].map((label) => (
          <div key={label} className="rounded-lg border bg-white p-6 shadow-sm">
            <p className="text-sm text-gray-500">{label}</p>
            <p className="mt-1 text-2xl font-bold text-gray-900">—</p>
          </div>
        ))}
      </div>
    </div>
  );
}
