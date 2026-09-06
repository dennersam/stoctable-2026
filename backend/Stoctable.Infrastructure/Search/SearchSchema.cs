namespace Stoctable.Infrastructure.Search;

/// <summary>
/// DDL de suporte à busca textual. Compartilhado entre a migration e o
/// PostgresFixture dos testes — o fixture usa EnsureCreated, que não roda
/// migrations, então precisa aplicar este DDL explicitamente.
/// </summary>
public static class SearchSchema
{
    public const string FunctionName = "f_search_norm";

    public const string Up = """
        CREATE EXTENSION IF NOT EXISTS unaccent;
        CREATE EXTENSION IF NOT EXISTS pg_trgm;

        -- Wrapper IMMUTABLE: unaccent() de 1 argumento é apenas STABLE (faz
        -- lookup do dicionário em runtime) e não pode ser usada em expressão
        -- de índice. A forma de 2 argumentos, com o dicionário explícito,
        -- pode — e é o que torna os índices GIN abaixo possíveis.
        CREATE OR REPLACE FUNCTION f_search_norm(text)
        RETURNS text
        LANGUAGE sql IMMUTABLE PARALLEL SAFE STRICT
        AS $$ SELECT lower(public.unaccent('public.unaccent', $1)) $$;

        CREATE INDEX IF NOT EXISTS ix_products_search_name
            ON products USING gin (f_search_norm(name) gin_trgm_ops);
        CREATE INDEX IF NOT EXISTS ix_products_search_sku
            ON products USING gin (f_search_norm(sku) gin_trgm_ops);
        CREATE INDEX IF NOT EXISTS ix_products_search_barcode
            ON products USING gin (f_search_norm(barcode) gin_trgm_ops);
        CREATE INDEX IF NOT EXISTS ix_manufacturers_search_name
            ON manufacturers USING gin (f_search_norm(name) gin_trgm_ops);
        CREATE INDEX IF NOT EXISTS ix_customers_search_full_name
            ON customers USING gin (f_search_norm(full_name) gin_trgm_ops);
        CREATE INDEX IF NOT EXISTS ix_customers_search_document
            ON customers USING gin (f_search_norm(document_number) gin_trgm_ops);
        CREATE INDEX IF NOT EXISTS ix_suppliers_search_company_name
            ON suppliers USING gin (f_search_norm(company_name) gin_trgm_ops);
        CREATE INDEX IF NOT EXISTS ix_suppliers_search_trade_name
            ON suppliers USING gin (f_search_norm(trade_name) gin_trgm_ops);
        """;

    public const string Down = """
        DROP INDEX IF EXISTS ix_products_search_name;
        DROP INDEX IF EXISTS ix_products_search_sku;
        DROP INDEX IF EXISTS ix_products_search_barcode;
        DROP INDEX IF EXISTS ix_manufacturers_search_name;
        DROP INDEX IF EXISTS ix_customers_search_full_name;
        DROP INDEX IF EXISTS ix_customers_search_document;
        DROP INDEX IF EXISTS ix_suppliers_search_company_name;
        DROP INDEX IF EXISTS ix_suppliers_search_trade_name;
        DROP FUNCTION IF EXISTS f_search_norm(text);
        """;
}
