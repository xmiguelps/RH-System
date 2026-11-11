import createClient from 'openapi-react-query'
import { criarClienteHttp, type ClienteHttp } from './clienteApi'
import { URL_DA_API } from './config'

// Páginas públicas (senha, formulário do candidato, certificado): nunca leva Authorization nem renova.
export const clienteHttpPublico: ClienteHttp = criarClienteHttp({ urlBase: URL_DA_API })

export const $apiPublica = createClient(clienteHttpPublico)
