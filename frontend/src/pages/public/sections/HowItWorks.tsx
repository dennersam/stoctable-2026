const passos = [
  {
    numero: '1',
    titulo: 'Cadastre sua empresa',
    descricao:
      'Informe o CNPJ e nós buscamos os dados na Receita. Adicione suas filiais e crie o usuário administrador.',
  },
  {
    numero: '2',
    titulo: 'Preparamos seu ambiente',
    descricao:
      'Seu banco de dados é criado automaticamente, separado do de qualquer outra empresa. Leva menos de um minuto.',
  },
  {
    numero: '3',
    titulo: 'Comece a vender',
    descricao:
      'Cadastre produtos e clientes, convide sua equipe e escolha em qual loja cada pessoa trabalha.',
  },
];

export function HowItWorks() {
  return (
    <section
      id="como-funciona"
      className="border-t border-gray-200 bg-gray-50 py-20 dark:border-white/10 dark:bg-white/5 lg:py-24"
    >
      <div className="mx-auto max-w-6xl px-4 sm:px-6">
        <div className="max-w-2xl">
          <h2 className="text-3xl font-bold tracking-tight sm:text-4xl">Como funciona</h2>
          <p className="mt-4 text-lg text-gray-600 dark:text-gray-300">
            Do cadastro à primeira venda, sem instalar nada.
          </p>
        </div>

        <ol className="mt-12 grid gap-8 md:grid-cols-3">
          {passos.map((passo) => (
            <li key={passo.numero} className="relative">
              <div className="flex h-11 w-11 items-center justify-center rounded-full bg-brand-600 text-lg font-semibold text-white">
                {passo.numero}
              </div>
              <h3 className="mt-4 text-lg font-semibold">{passo.titulo}</h3>
              <p className="mt-2 leading-relaxed text-gray-600 dark:text-gray-300">
                {passo.descricao}
              </p>
            </li>
          ))}
        </ol>
      </div>
    </section>
  );
}
