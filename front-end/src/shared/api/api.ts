import createClient from 'openapi-react-query'
import { tokenDeAcesso } from '../../features/autenticacao/armazenamentoDaSessao'
import { criarClienteHttp, type ClienteHttp } from './clienteApi'
import { URL_DA_API } from './config'
import { renovadorDeAcesso } from './renovadorDeAcesso'

export const clienteHttp: ClienteHttp = criarClienteHttp({
  urlBase: URL_DA_API,
  obterToken: tokenDeAcesso.obter,
  aoExpirarAcesso: renovadorDeAcesso.renovar,
})

export const $api = createClient(clienteHttp)
