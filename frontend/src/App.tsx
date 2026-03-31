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
import { ProductListPage } from '@/pages/products/ProductListPage';
import { ProductFormPage } from '@/pages/products/ProductFormPage';
import { CustomerListPage } from '@/pages/customers/CustomerListPage';
import { QuotationListPage } from '@/pages/quotations/QuotationListPage';
import { QuotationEditorPage } from '@/pages/quotations/QuotationEditorPage';
import { CheckoutListPage } from '@/pages/checkout/CheckoutListPage';
import { CheckoutPage } from '@/pages/checkout/CheckoutPage';
import { InventoryPage } from '@/pages/inventory/InventoryPage';

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

          {/* Products */}
          <Route
            path="/products"
            element={
              <ProtectedRoute roles={['admin', 'atendente']}>
                <ProductListPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/products/new"
            element={
              <ProtectedRoute roles={['admin']}>
                <ProductFormPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/products/:id/edit"
            element={
              <ProtectedRoute roles={['admin']}>
                <ProductFormPage />
              </ProtectedRoute>
            }
          />

          {/* Customers */}
          <Route
            path="/customers"
            element={
              <ProtectedRoute roles={['admin', 'atendente']}>
                <CustomerListPage />
              </ProtectedRoute>
            }
          />

          {/* Quotations */}
          <Route
            path="/quotations"
            element={
              <ProtectedRoute roles={['admin', 'atendente']}>
                <QuotationListPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/quotations/new"
            element={
              <ProtectedRoute roles={['admin', 'atendente']}>
                <QuotationEditorPage />
              </ProtectedRoute>
            }
          />

          {/* Checkout */}
          <Route
            path="/checkout"
            element={
              <ProtectedRoute roles={['admin', 'caixa']}>
                <CheckoutListPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/checkout/:saleId"
            element={
              <ProtectedRoute roles={['admin', 'caixa']}>
                <CheckoutPage />
              </ProtectedRoute>
            }
          />

          {/* Inventory */}
          <Route
            path="/inventory"
            element={
              <ProtectedRoute roles={['admin']}>
                <InventoryPage />
              </ProtectedRoute>
            }
          />

          {/* Placeholders */}
          <Route
            path="/suppliers"
            element={
              <ProtectedRoute roles={['admin']}>
                <div className="text-gray-500 dark:text-gray-400 p-4">Fornecedores — em breve</div>
              </ProtectedRoute>
            }
          />
          <Route
            path="/reports"
            element={
              <ProtectedRoute roles={['admin']}>
                <div className="text-gray-500 dark:text-gray-400 p-4">Relatórios — em breve</div>
              </ProtectedRoute>
            }
          />
          <Route
            path="/admin"
            element={
              <ProtectedRoute roles={['admin']}>
                <div className="text-gray-500 dark:text-gray-400 p-4">Administração — em breve</div>
              </ProtectedRoute>
            }
          />
          <Route
            path="/admin/users"
            element={
              <ProtectedRoute roles={['admin']}>
                <div className="text-gray-500 dark:text-gray-400 p-4">Usuários — em breve</div>
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
