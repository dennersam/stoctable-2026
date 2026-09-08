import { ChevronDown } from 'lucide-react';

const perguntas = [
  {
    pergunta: 'Meus dados ficam misturados com os de outras empresas?',
    resposta:
      'Não. Cada empresa recebe um banco de dados próprio, criado no momento do cadastro. Não existe consulta que alcance os dados de outra empresa, porque não é o mesmo banco.',
  },
  {
    pergunta: 'Como funciona com mais de uma loja?',
    resposta:
      'Produtos, clientes e fornecedores são da empresa e ficam disponíveis em todas as lojas. Estoque, vendas, caixa e orçamentos são de cada filial. Quem tem acesso a mais de uma troca de loja dentro do sistema, sem sair da conta.',
  },
  {
    pergunta: 'Preciso instalar alguma coisa?',
    resposta:
      'Não. O Stoctable roda no navegador, em qualquer computador da loja. Não há instalação, servidor local nem backup manual.',
  },
  {
    pergunta: 'Consigo trazer os dados do meu sistema atual?',
    resposta:
      'Sim. Já migramos cadastros de produtos, clientes, fornecedores e usuários de sistemas antigos. Fale com a gente antes de começar para combinarmos o formato.',
  },
  {
    pergunta: 'Quem pode ver e alterar o quê?',
    resposta:
      'Há três perfis: administrador, atendente e caixa. Cada um enxerga apenas as telas do seu trabalho, e toda alteração fica registrada em auditoria com autor, horário e o que mudou.',
  },
  {
    pergunta: 'O que acontece se eu digitar o CNPJ errado no cadastro?',
    resposta:
      'O cadastro valida o CNPJ e busca a razão social na base da Receita Federal, então o erro aparece antes de você concluir. Se a consulta estiver indisponível, dá para preencher os dados manualmente.',
  },
];

export function Faq() {
  return (
    <section id="duvidas" className="border-t border-gray-200 py-20 dark:border-white/10 lg:py-24">
      <div className="mx-auto max-w-3xl px-4 sm:px-6">
        <h2 className="text-3xl font-bold tracking-tight sm:text-4xl">Perguntas frequentes</h2>

        <div className="mt-10 divide-y divide-gray-200 dark:divide-white/10">
          {perguntas.map(({ pergunta, resposta }) => (
            // <details> nativo: acessível por teclado e funciona sem JavaScript,
            // sem precisar de biblioteca de acordeão.
            <details key={pergunta} className="group py-5">
              <summary className="flex cursor-pointer list-none items-center justify-between gap-4 rounded-md text-left font-medium focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-500">
                {pergunta}
                <ChevronDown
                  size={18}
                  className="shrink-0 text-gray-400 transition-transform group-open:rotate-180"
                />
              </summary>
              <p className="mt-3 leading-relaxed text-gray-600 dark:text-gray-300">{resposta}</p>
            </details>
          ))}
        </div>
      </div>
    </section>
  );
}
