import axios from 'axios';
import { env } from '@/config/env';

/**
 * Cliente HTTP das páginas públicas — consulta de CNPJ, cadastro de empresa e
 * acompanhamento do provisionamento.
 *
 * É uma instância separada, sem nenhum interceptor, de propósito. O cliente
 * autenticado (`lib/api.ts`) reage a 401 tentando renovar a sessão e, se falhar,
 * dá `window.location.href = '/login'`. Numa página de marketing isso seria
 * desastroso: um refresh token velho no localStorage arrancaria o visitante da
 * landing page e o jogaria num formulário de login que ele nunca pediu.
 *
 * Também não manda Authorization nem X-Branch-Id: estes endpoints são anônimos
 * e não pertencem a nenhuma empresa — quem se cadastra ainda não tem uma.
 */
const publicApi = axios.create({
  baseURL: `${env.apiBaseUrl}/api`,
  headers: { 'Content-Type': 'application/json' },
});

export default publicApi;
