import { useEffect } from 'react';
import { Hero } from './sections/Hero';
import { Features } from './sections/Features';
import { HowItWorks } from './sections/HowItWorks';
import { Faq } from './sections/Faq';
import { Cta } from './sections/Cta';

export function LandingPage() {
  useEffect(() => {
    const anterior = document.title;
    document.title = 'Stoctable — gestão para lojas de peças e acessórios';
    return () => {
      document.title = anterior;
    };
  }, []);

  return (
    <>
      <Hero />
      <Features />
      <HowItWorks />
      <Faq />
      <Cta />
    </>
  );
}
