import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { useEffect } from 'react';
import { Toaster } from 'react-hot-toast';

import { hydrateAuth, useAuthStore } from '@/store/authStore';
import { ProtectedRoute } from '@/components/base/ProtectedRoute';
import { Layout } from '@/components/base/Layout';

import { LoginPage } from '@/pages/auth/LoginPage';
import { AdminDashboard } from '@/pages/dashboard/AdminDashboard';
import { AtendenteDashboard } from '@/pages/dashboard/AtendenteDashboard';
import { CaixaDashboard } from '@/pages/dashboard/CaixaDashboard';

function DashboardRouter() {
  const { user } = useAuthStore();
  if (!user) return null;
  if (user.role === 'admin') return <AdminDashboard />;
  if (user.role === 'atendente') return <AtendenteDashboard />;
  return <CaixaDashboard />;
}

function App() {
  useEffect(() => {
    hydrateAuth();
  }, []);

  return (
    <BrowserRouter>
      <Toaster position="top-right" />
      <Routes>
        {/* Public */}
        <Route path="/login" element={<LoginPage />} />

        {/* Protected */}
        <Route
          element={
            <ProtectedRoute>
              <Layout />
            </ProtectedRoute>
          }
        >
          <Route path="/" element={<Navigate to="/dashboard" replace />} />
          <Route path="/dashboard" element={<DashboardRouter />} />

          <Route
            path="/products"
            element={
              <ProtectedRoute roles={['admin', 'atendente']}>
                <div className="text-gray-500">Produtos — Sprint 5</div>
              </ProtectedRoute>
            }
          />
          <Route
            path="/customers"
            element={
              <ProtectedRoute roles={['admin', 'atendente']}>
                <div className="text-gray-500">Clientes — Sprint 5</div>
              </ProtectedRoute>
            }
          />
          <Route
            path="/quotations"
            element={
              <ProtectedRoute roles={['admin', 'atendente']}>
                <div className="text-gray-500">Orçamentos — Sprint 5</div>
              </ProtectedRoute>
            }
          />
          <Route
            path="/quotations/new"
            element={
              <ProtectedRoute roles={['admin', 'atendente']}>
                <div className="text-gray-500">Novo Orçamento — Sprint 5</div>
              </ProtectedRoute>
            }
          />
          <Route
            path="/checkout"
            element={
              <ProtectedRoute roles={['admin', 'caixa']}>
                <div className="text-gray-500">Caixa — Sprint 5</div>
              </ProtectedRoute>
            }
          />
          <Route
            path="/checkout/:saleId"
            element={
              <ProtectedRoute roles={['admin', 'caixa']}>
                <div className="text-gray-500">Checkout — Sprint 5</div>
              </ProtectedRoute>
            }
          />
          <Route
            path="/suppliers"
            element={
              <ProtectedRoute roles={['admin']}>
                <div className="text-gray-500">Fornecedores — Sprint 5</div>
              </ProtectedRoute>
            }
          />
          <Route
            path="/inventory"
            element={
              <ProtectedRoute roles={['admin']}>
                <div className="text-gray-500">Estoque — Sprint 5</div>
              </ProtectedRoute>
            }
          />
          <Route
            path="/reports"
            element={
              <ProtectedRoute roles={['admin']}>
                <div className="text-gray-500">Relatórios — Sprint 5</div>
              </ProtectedRoute>
            }
          />
          <Route
            path="/admin"
            element={
              <ProtectedRoute roles={['admin']}>
                <div className="text-gray-500">Administração — Sprint 5</div>
              </ProtectedRoute>
            }
          />
          <Route
            path="/admin/users"
            element={
              <ProtectedRoute roles={['admin']}>
                <div className="text-gray-500">Usuários — Sprint 5</div>
              </ProtectedRoute>
            }
          />
        </Route>

        <Route path="*" element={<Navigate to="/dashboard" replace />} />
      </Routes>
    </BrowserRouter>
  );
}

export default App;
