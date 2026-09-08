import { Link } from 'react-router-dom';
import { ArrowRight } from 'lucide-react';
import { Button } from '@/components/ui/Button';

export function Cta() {
  return (
    <section className="border-t border-gray-200 py-20 dark:border-white/10">
      <div className="mx-auto max-w-4xl px-4 sm:px-6">
        <div className="rounded-2xl bg-brand-600 px-6 py-12 text-center sm:px-12">
          <h2 className="text-3xl font-bold tracking-tight text-white sm:text-4xl">
            Pronto para organizar sua loja?
          </h2>
          <p className="mx-auto mt-4 max-w-xl text-lg text-brand-100">
            Crie sua conta com o CNPJ da empresa. Seu ambiente fica pronto em
            menos de um minuto.
          </p>

          <div className="mt-8 flex flex-col justify-center gap-3 sm:flex-row">
            <Button asChild size="lg" variant="secondary">
              <Link to="/cadastro">
                Cadastre-se
                <ArrowRight size={18} />
              </Link>
            </Button>
            <Button
              asChild
              size="lg"
              variant="outline"
              className="border-white/40 text-white hover:bg-white/10"
            >
              <Link to="/login">Já tenho conta</Link>
            </Button>
          </div>
        </div>
      </div>
    </section>
  );
}
