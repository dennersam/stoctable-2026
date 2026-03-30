-- ============================================================
-- Stoctable — V001 — Schema inicial PostgreSQL
-- Aplicado a cada banco de filial: stoctable_branch_{id}
-- ============================================================

-- Extensão para geração de UUIDs
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- ============================================================
-- TIPOS ENUM
-- ============================================================
CREATE TYPE user_role         AS ENUM ('admin', 'atendente', 'caixa');
CREATE TYPE quotation_status  AS ENUM ('draft', 'finalized', 'converted', 'cancelled');
CREATE TYPE sale_status       AS ENUM ('pending_payment', 'partially_paid', 'paid', 'cancelled');
CREATE TYPE payment_status    AS ENUM ('pending', 'completed', 'refunded');
CREATE TYPE movement_type     AS ENUM (
    'purchase', 'sale', 'adjustment_in', 'adjustment_out',
    'reservation', 'reservation_release', 'transfer_in', 'transfer_out'
);
CREATE TYPE audit_action      AS ENUM ('create', 'update', 'delete');

-- ============================================================
-- FILIAIS (tabela de referência replicada em cada banco)
-- Equivalente ao TABLOJA do SIC 6
-- ============================================================
CREATE TABLE branches (
    id          UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    name        VARCHAR(100) NOT NULL,
    cnpj        VARCHAR(18),
    address     VARCHAR(255),
    phone       VARCHAR(20),
    is_active   BOOLEAN     NOT NULL DEFAULT true,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_by  VARCHAR(100) NOT NULL DEFAULT 'system',
    updated_at  TIMESTAMPTZ,
    updated_by  VARCHAR(100)
);

-- ============================================================
-- USUÁRIOS
-- Equivalente ao TABUSER do SIC 6 (senha em BCrypt, não plain text)
-- ============================================================
CREATE TABLE users (
    id                          UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    username                    VARCHAR(50)  NOT NULL,
    email                       VARCHAR(150) NOT NULL,
    password_hash               VARCHAR(255) NOT NULL,
    full_name                   VARCHAR(150) NOT NULL,
    role                        user_role    NOT NULL DEFAULT 'atendente',
    is_active                   BOOLEAN      NOT NULL DEFAULT true,
    last_login_at               TIMESTAMPTZ,
    refresh_token               VARCHAR(500),
    refresh_token_expires_at    TIMESTAMPTZ,
    created_at                  TIMESTAMPTZ  NOT NULL DEFAULT now(),
    created_by                  VARCHAR(100) NOT NULL DEFAULT 'system',
    updated_at                  TIMESTAMPTZ,
    updated_by                  VARCHAR(100),
    CONSTRAINT uq_users_username UNIQUE (username),
    CONSTRAINT uq_users_email    UNIQUE (email)
);

-- ============================================================
-- FORNECEDORES
-- Equivalente ao TABFOR do SIC 6
-- ============================================================
CREATE TABLE suppliers (
    id              UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    company_name    VARCHAR(150) NOT NULL,
    trade_name      VARCHAR(150),
    cnpj            VARCHAR(18),
    address         VARCHAR(255),
    phone           VARCHAR(20),
    email           VARCHAR(150),
    contact_person  VARCHAR(100),
    is_active       BOOLEAN      NOT NULL DEFAULT true,
    notes           TEXT,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT now(),
    created_by      VARCHAR(100) NOT NULL DEFAULT 'system',
    updated_at      TIMESTAMPTZ,
    updated_by      VARCHAR(100),
    CONSTRAINT uq_suppliers_cnpj UNIQUE (cnpj)
);

-- ============================================================
-- CATEGORIAS DE PRODUTO (hierárquica)
-- ============================================================
CREATE TABLE product_categories (
    id          UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    name        VARCHAR(100) NOT NULL,
    parent_id   UUID         REFERENCES product_categories(id),
    is_active   BOOLEAN      NOT NULL DEFAULT true,
    created_at  TIMESTAMPTZ  NOT NULL DEFAULT now(),
    created_by  VARCHAR(100) NOT NULL DEFAULT 'system',
    updated_at  TIMESTAMPTZ,
    updated_by  VARCHAR(100)
);

-- ============================================================
-- PRODUTOS
-- Equivalente ao TABEST1 do SIC 6
-- ============================================================
CREATE TABLE products (
    id              UUID          PRIMARY KEY DEFAULT gen_random_uuid(),
    sku             VARCHAR(50)   NOT NULL,           -- TABEST1.codigo
    barcode         VARCHAR(50),                      -- TABEST1.cean
    name            VARCHAR(255)  NOT NULL,            -- TABEST1.produto
    manufacturer    VARCHAR(100),                     -- TABEST1.fabricante
    category_id     UUID          REFERENCES product_categories(id),
    supplier_id     UUID          REFERENCES suppliers(id),
    cost_price      NUMERIC(12,2) NOT NULL DEFAULT 0, -- TABEST1.precocusto
    sale_price      NUMERIC(12,2) NOT NULL DEFAULT 0, -- TABEST1.precovendas
    stock_quantity  NUMERIC(10,3) NOT NULL DEFAULT 0, -- TABEST1.quantidade
    stock_reserved  NUMERIC(10,3) NOT NULL DEFAULT 0, -- reservas ativas
    stock_minimum   NUMERIC(10,3) NOT NULL DEFAULT 0, -- TABEST1.estminimo
    unit            VARCHAR(10)   NOT NULL DEFAULT 'UN',
    icms_rate       NUMERIC(5,2),                     -- TABEST1.icms
    ipi_rate        NUMERIC(5,2),                     -- TABEST1.ipi
    cst             VARCHAR(10),                      -- TABEST1.cst
    ncm             VARCHAR(10),                      -- NCM/NBM
    image_url       VARCHAR(500),                     -- Azure Blob Storage
    is_active       BOOLEAN       NOT NULL DEFAULT true,
    notes           TEXT,
    created_at      TIMESTAMPTZ   NOT NULL DEFAULT now(),
    created_by      VARCHAR(100)  NOT NULL DEFAULT 'system',
    updated_at      TIMESTAMPTZ,
    updated_by      VARCHAR(100),
    CONSTRAINT uq_products_sku UNIQUE (sku)
);

CREATE INDEX idx_products_barcode  ON products(barcode)     WHERE barcode IS NOT NULL;
CREATE INDEX idx_products_category ON products(category_id) WHERE category_id IS NOT NULL;
CREATE INDEX idx_products_supplier ON products(supplier_id) WHERE supplier_id IS NOT NULL;
CREATE INDEX idx_products_name     ON products USING gin(to_tsvector('portuguese', name));

-- ============================================================
-- TIPOS DE CLIENTE
-- Equivalente ao TABCLIM do SIC 6
-- ============================================================
CREATE TABLE customer_types (
    id              UUID          PRIMARY KEY DEFAULT gen_random_uuid(),
    name            VARCHAR(50)   NOT NULL,
    discount_pct    NUMERIC(5,2)  NOT NULL DEFAULT 0,
    credit_limit    NUMERIC(12,2) NOT NULL DEFAULT 0,
    is_active       BOOLEAN       NOT NULL DEFAULT true,
    created_at      TIMESTAMPTZ   NOT NULL DEFAULT now(),
    created_by      VARCHAR(100)  NOT NULL DEFAULT 'system',
    updated_at      TIMESTAMPTZ,
    updated_by      VARCHAR(100)
);

-- ============================================================
-- CLIENTES
-- Equivalente ao TABCLI do SIC 6
-- ============================================================
CREATE TABLE customers (
    id                  UUID          PRIMARY KEY DEFAULT gen_random_uuid(),
    full_name           VARCHAR(150)  NOT NULL,           -- TABCLI.nome
    document_type       VARCHAR(5)    NOT NULL DEFAULT 'CPF',
    document_number     VARCHAR(18),                      -- TABCLI.cgc (CPF/CNPJ)
    email               VARCHAR(150),
    phone               VARCHAR(20),
    mobile              VARCHAR(20),                      -- TABCLI.celular
    address             VARCHAR(255),
    city                VARCHAR(100),
    state               CHAR(2),
    zip_code            VARCHAR(10),
    customer_type_id    UUID          REFERENCES customer_types(id),
    credit_limit        NUMERIC(12,2) NOT NULL DEFAULT 0, -- TABCLI.limitecred
    is_active           BOOLEAN       NOT NULL DEFAULT true,
    notes               TEXT,
    created_at          TIMESTAMPTZ   NOT NULL DEFAULT now(),
    created_by          VARCHAR(100)  NOT NULL DEFAULT 'system',
    updated_at          TIMESTAMPTZ,
    updated_by          VARCHAR(100),
    CONSTRAINT uq_customers_document UNIQUE (document_number)
);

CREATE INDEX idx_customers_document ON customers(document_number) WHERE document_number IS NOT NULL;
CREATE INDEX idx_customers_name     ON customers USING gin(to_tsvector('portuguese', full_name));

-- ============================================================
-- NOTAS CRM DE CLIENTES (histórico)
-- ============================================================
CREATE TABLE customer_crm_notes (
    id          UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id UUID        NOT NULL REFERENCES customers(id) ON DELETE CASCADE,
    note        TEXT        NOT NULL,
    note_type   VARCHAR(20) NOT NULL DEFAULT 'general', -- general | complaint | followup
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_by  VARCHAR(100) NOT NULL DEFAULT 'system'
);

CREATE INDEX idx_crm_notes_customer ON customer_crm_notes(customer_id);

-- ============================================================
-- FORMAS DE PAGAMENTO
-- ============================================================
CREATE TABLE payment_methods (
    id                      UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    name                    VARCHAR(50) NOT NULL,
    requires_installments   BOOLEAN     NOT NULL DEFAULT false,
    max_installments        INT         NOT NULL DEFAULT 1,
    is_active               BOOLEAN     NOT NULL DEFAULT true,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_by              VARCHAR(100) NOT NULL DEFAULT 'system',
    updated_at              TIMESTAMPTZ,
    updated_by              VARCHAR(100)
);

-- ============================================================
-- ORÇAMENTOS
-- Equivalente ao TABEST3A do SIC 6 (antes da venda)
-- ============================================================
CREATE TABLE quotations (
    id                      UUID             PRIMARY KEY DEFAULT gen_random_uuid(),
    quotation_number        VARCHAR(20)      NOT NULL,
    customer_id             UUID             REFERENCES customers(id),
    salesperson_id          UUID             REFERENCES users(id),
    status                  quotation_status NOT NULL DEFAULT 'draft',
    subtotal                NUMERIC(12,2)    NOT NULL DEFAULT 0,
    discount_pct            NUMERIC(5,2)     NOT NULL DEFAULT 0,
    discount_amount         NUMERIC(12,2)    NOT NULL DEFAULT 0,
    total_amount            NUMERIC(12,2)    NOT NULL DEFAULT 0,
    notes                   TEXT,
    valid_until             DATE,
    finalized_at            TIMESTAMPTZ,
    cancelled_at            TIMESTAMPTZ,
    cancellation_reason     TEXT,
    converted_to_sale_id    UUID,
    created_at              TIMESTAMPTZ      NOT NULL DEFAULT now(),
    created_by              VARCHAR(100)     NOT NULL DEFAULT 'system',
    updated_at              TIMESTAMPTZ,
    updated_by              VARCHAR(100),
    CONSTRAINT uq_quotations_number UNIQUE (quotation_number)
);

CREATE INDEX idx_quotations_customer    ON quotations(customer_id)    WHERE customer_id IS NOT NULL;
CREATE INDEX idx_quotations_salesperson ON quotations(salesperson_id) WHERE salesperson_id IS NOT NULL;
CREATE INDEX idx_quotations_status      ON quotations(status);

-- ============================================================
-- ITENS DO ORÇAMENTO
-- Equivalente ao TABEST3B do SIC 6
-- ============================================================
CREATE TABLE quotation_items (
    id              UUID          PRIMARY KEY DEFAULT gen_random_uuid(),
    quotation_id    UUID          NOT NULL REFERENCES quotations(id) ON DELETE CASCADE,
    product_id      UUID          NOT NULL REFERENCES products(id),
    quantity        NUMERIC(10,3) NOT NULL,
    unit_price      NUMERIC(12,2) NOT NULL,
    discount_pct    NUMERIC(5,2)  NOT NULL DEFAULT 0,
    line_total      NUMERIC(12,2) NOT NULL,
    created_at      TIMESTAMPTZ   NOT NULL DEFAULT now(),
    created_by      VARCHAR(100)  NOT NULL DEFAULT 'system',
    updated_at      TIMESTAMPTZ,
    updated_by      VARCHAR(100)
);

CREATE INDEX idx_quotation_items_quotation ON quotation_items(quotation_id);
CREATE INDEX idx_quotation_items_product   ON quotation_items(product_id);

-- ============================================================
-- VENDAS (convertidas a partir de orçamentos)
-- ============================================================
CREATE TABLE sales (
    id              UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    sale_number     VARCHAR(20) NOT NULL,
    quotation_id    UUID        REFERENCES quotations(id),
    customer_id     UUID        REFERENCES customers(id),
    salesperson_id  UUID        REFERENCES users(id),
    cashier_id      UUID        REFERENCES users(id),
    status          sale_status NOT NULL DEFAULT 'pending_payment',
    subtotal        NUMERIC(12,2) NOT NULL DEFAULT 0,
    discount_amount NUMERIC(12,2) NOT NULL DEFAULT 0,
    total_amount    NUMERIC(12,2) NOT NULL DEFAULT 0,
    amount_paid     NUMERIC(12,2) NOT NULL DEFAULT 0,
    notes           TEXT,
    completed_at    TIMESTAMPTZ,
    created_at      TIMESTAMPTZ   NOT NULL DEFAULT now(),
    created_by      VARCHAR(100)  NOT NULL DEFAULT 'system',
    updated_at      TIMESTAMPTZ,
    updated_by      VARCHAR(100),
    CONSTRAINT uq_sales_number UNIQUE (sale_number)
);

CREATE INDEX idx_sales_customer    ON sales(customer_id)    WHERE customer_id IS NOT NULL;
CREATE INDEX idx_sales_salesperson ON sales(salesperson_id) WHERE salesperson_id IS NOT NULL;
CREATE INDEX idx_sales_cashier     ON sales(cashier_id)     WHERE cashier_id IS NOT NULL;
CREATE INDEX idx_sales_status      ON sales(status);

-- ============================================================
-- ITENS DA VENDA
-- ============================================================
CREATE TABLE sale_items (
    id              UUID          PRIMARY KEY DEFAULT gen_random_uuid(),
    sale_id         UUID          NOT NULL REFERENCES sales(id) ON DELETE CASCADE,
    product_id      UUID          NOT NULL REFERENCES products(id),
    quantity        NUMERIC(10,3) NOT NULL,
    unit_price      NUMERIC(12,2) NOT NULL,
    discount_pct    NUMERIC(5,2)  NOT NULL DEFAULT 0,
    line_total      NUMERIC(12,2) NOT NULL,
    created_at      TIMESTAMPTZ   NOT NULL DEFAULT now(),
    created_by      VARCHAR(100)  NOT NULL DEFAULT 'system',
    updated_at      TIMESTAMPTZ,
    updated_by      VARCHAR(100)
);

CREATE INDEX idx_sale_items_sale    ON sale_items(sale_id);
CREATE INDEX idx_sale_items_product ON sale_items(product_id);

-- ============================================================
-- PAGAMENTOS (suporte a pagamento dividido)
-- Equivalente ao TABCR1/TABCAR1/TABCX1 do SIC 6
-- ============================================================
CREATE TABLE payments (
    id                  UUID           PRIMARY KEY DEFAULT gen_random_uuid(),
    sale_id             UUID           NOT NULL REFERENCES sales(id),
    payment_method_id   UUID           NOT NULL REFERENCES payment_methods(id),
    amount              NUMERIC(12,2)  NOT NULL,
    installments        INT            NOT NULL DEFAULT 1,
    status              payment_status NOT NULL DEFAULT 'pending',
    transaction_ref     VARCHAR(100),
    paid_at             TIMESTAMPTZ,
    created_at          TIMESTAMPTZ    NOT NULL DEFAULT now(),
    created_by          VARCHAR(100)   NOT NULL DEFAULT 'system',
    updated_at          TIMESTAMPTZ,
    updated_by          VARCHAR(100)
);

CREATE INDEX idx_payments_sale          ON payments(sale_id);
CREATE INDEX idx_payments_method        ON payments(payment_method_id);

-- ============================================================
-- MOVIMENTAÇÕES DE ESTOQUE (trilha de auditoria completa)
-- ============================================================
CREATE TABLE inventory_movements (
    id              UUID          PRIMARY KEY DEFAULT gen_random_uuid(),
    product_id      UUID          NOT NULL REFERENCES products(id),
    movement_type   movement_type NOT NULL,
    quantity        NUMERIC(10,3) NOT NULL,        -- positivo = entrada, negativo = saída
    quantity_before NUMERIC(10,3) NOT NULL,
    quantity_after  NUMERIC(10,3) NOT NULL,
    reference_type  VARCHAR(50),                   -- 'sale' | 'quotation' | 'adjustment'
    reference_id    UUID,
    notes           TEXT,
    created_at      TIMESTAMPTZ   NOT NULL DEFAULT now(),
    created_by      VARCHAR(100)  NOT NULL DEFAULT 'system'
);

CREATE INDEX idx_inventory_movements_product   ON inventory_movements(product_id);
CREATE INDEX idx_inventory_movements_reference ON inventory_movements(reference_type, reference_id)
    WHERE reference_id IS NOT NULL;

-- ============================================================
-- RESERVAS DE ESTOQUE (Opção B)
-- Reserva criada ao finalizar orçamento, liberada ao cancelar ou converter
-- ============================================================
CREATE TABLE stock_reservations (
    id              UUID          PRIMARY KEY DEFAULT gen_random_uuid(),
    product_id      UUID          NOT NULL REFERENCES products(id),
    quotation_id    UUID          NOT NULL REFERENCES quotations(id),
    quantity        NUMERIC(10,3) NOT NULL,
    reserved_at     TIMESTAMPTZ   NOT NULL DEFAULT now(),
    released_at     TIMESTAMPTZ,
    is_active       BOOLEAN       NOT NULL DEFAULT true,
    created_at      TIMESTAMPTZ   NOT NULL DEFAULT now(),
    created_by      VARCHAR(100)  NOT NULL DEFAULT 'system'
);

CREATE INDEX idx_reservations_product   ON stock_reservations(product_id)   WHERE is_active = true;
CREATE INDEX idx_reservations_quotation ON stock_reservations(quotation_id);

-- ============================================================
-- LOGS DE AUDITORIA
-- Capturados pelo AuditSaveChangesInterceptor (EF Core)
-- ============================================================
CREATE TABLE audit_logs (
    id          UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    entity_name VARCHAR(100) NOT NULL,
    entity_id   VARCHAR(100) NOT NULL,
    action      audit_action NOT NULL,
    user_id     UUID,
    username    VARCHAR(100),
    ip_address  VARCHAR(45),
    old_values  JSONB,
    new_values  JSONB,
    occurred_at TIMESTAMPTZ  NOT NULL DEFAULT now()
);

CREATE INDEX idx_audit_logs_entity      ON audit_logs(entity_name, entity_id);
CREATE INDEX idx_audit_logs_user        ON audit_logs(user_id)      WHERE user_id IS NOT NULL;
CREATE INDEX idx_audit_logs_occurred_at ON audit_logs(occurred_at DESC);

-- ============================================================
-- DADOS INICIAIS (SEED)
-- ============================================================

-- Formas de pagamento padrão
INSERT INTO payment_methods (id, name, requires_installments, max_installments, is_active, created_by) VALUES
    (gen_random_uuid(), 'Dinheiro',         false, 1,  true, 'seed'),
    (gen_random_uuid(), 'PIX',              false, 1,  true, 'seed'),
    (gen_random_uuid(), 'Cartão Débito',    false, 1,  true, 'seed'),
    (gen_random_uuid(), 'Cartão Crédito',   true,  12, true, 'seed'),
    (gen_random_uuid(), 'Boleto',           false, 1,  true, 'seed'),
    (gen_random_uuid(), 'Cheque',           false, 1,  true, 'seed');

-- Tipos de cliente padrão
INSERT INTO customer_types (id, name, discount_pct, credit_limit, is_active, created_by) VALUES
    (gen_random_uuid(), 'Varejo',   0,    0,     true, 'seed'),
    (gen_random_uuid(), 'Atacado',  5,    5000,  true, 'seed'),
    (gen_random_uuid(), 'Oficina',  8,    10000, true, 'seed'),
    (gen_random_uuid(), 'Revendedor', 10, 20000, true, 'seed');
