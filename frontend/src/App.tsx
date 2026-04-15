import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { Toaster } from 'react-hot-toast';

import { useAuthStore } from '@/store/authStore';
import { ProtectedRoute } from '@/components/base/ProtectedRoute';
import { Layout } from '@/components/base/Layout';

import { LoginPage } from '@/pages/auth/LoginPage';
import { AdminDashboard } from '@/pages/dashboard/AdminDashboard';
import { AtendenteDashboard } from '@/pages/dashboard/AtendenteDashboard';
import { CaixaDashboard } from '@/pages/dashboard/CaixaDashboard';
import { ProductListPage } from '@/pages/products/ProductListPage';
import { CustomerListPage } from '@/pages/customers/CustomerListPage';
import { CustomerDetailPage } from '@/pages/customers/CustomerDetailPage';
import { SupplierListPage } from '@/pages/suppliers/SupplierListPage';
import { QuotationListPage } from '@/pages/quotations/QuotationListPage';
import { QuotationEditorPage } from '@/pages/quotations/QuotationEditorPage';
import { QuotationDetailPage } from '@/pages/quotations/QuotationDetailPage';
import { CheckoutListPage } from '@/pages/checkout/CheckoutListPage';
import { CheckoutPage } from '@/pages/checkout/CheckoutPage';
import { InventoryPage } from '@/pages/inventory/InventoryPage';
import { ManufacturerListPage } from '@/pages/manufacturers/ManufacturerListPage';

function DashboardRouter() {
  const { user } = useAuthStore();
  if (!user) return null;
  if (user.role === 'admin') return <AdminDashboard />;
  if (user.role === 'atendente') return <AtendenteDashboard />;
  return <CaixaDashboard />;
}

function App() {
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

          {/* Customers */}
          <Route
            path="/customers"
            element={
              <ProtectedRoute roles={['admin', 'atendente']}>
                <CustomerListPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/customers/:id"
            element={
              <ProtectedRoute roles={['admin', 'atendente']}>
                <CustomerDetailPage />
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
          <Route
            path="/quotations/:id"
            element={
              <ProtectedRoute roles={['admin', 'atendente', 'caixa']}>
                <QuotationDetailPage />
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

          {/* Manufacturers */}
          <Route
            path="/manufacturers"
            element={
              <ProtectedRoute roles={['admin']}>
                <ManufacturerListPage />
              </ProtectedRoute>
            }
          />

          {/* Suppliers */}
          <Route
            path="/suppliers"
            element={
              <ProtectedRoute roles={['admin']}>
                <SupplierListPage />
              </ProtectedRoute>
            }
          />

          {/* Placeholders */}
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

        <Route path="*" element={<Navigate to="/login" replace />} />
      </Routes>
    </BrowserRouter>
  );
}

export default App;
