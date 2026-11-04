import type { RouteObject } from 'react-router'
import { PERFIS_POR_FAMILIA } from './caminhos'
import { IrParaOInicio } from './IrParaOInicio'
import { GuardaDePerfil } from './GuardaDePerfil'
import { GuardaDeSessao } from './GuardaDeSessao'
import { Pagina404 } from './paginas/Pagina404'
import { PortalLayout } from './PortalLayout'
import { RaizDoApp } from './RaizDoApp'
import {
  ROTAS_EU,
  ROTAS_GESTOR,
  ROTAS_PUBLICAS,
  ROTAS_RH,
  type RotaDoPortal,
} from './tabelaDeRotas'

function paraRota(rota: RotaDoPortal): RouteObject {
  return {
    path: rota.caminho,
    element: rota.elemento,
    children: rota.filhas?.map(paraRota),
  }
}

function familia(
  prefixo: string,
  perfis: (typeof PERFIS_POR_FAMILIA)[keyof typeof PERFIS_POR_FAMILIA],
  rotas: readonly RotaDoPortal[],
): RouteObject {
  return {
    path: prefixo,
    element: <GuardaDePerfil perfis={perfis} />,
    children: [...rotas.map(paraRota), { path: '*', element: <Pagina404 /> }],
  }
}

export function criarRotas(): RouteObject[] {
  return [
    {
      element: <RaizDoApp />,
      children: [
        ...ROTAS_PUBLICAS.map(paraRota),
        {
          element: <GuardaDeSessao />,
          children: [
            { path: '/', element: <IrParaOInicio /> },
            {
              element: <PortalLayout />,
              children: [
                familia('/rh', PERFIS_POR_FAMILIA.rh, ROTAS_RH),
                familia('/gestor', PERFIS_POR_FAMILIA.gestor, ROTAS_GESTOR),
                familia('/eu', PERFIS_POR_FAMILIA.eu, ROTAS_EU),
                { path: '*', element: <Pagina404 /> },
              ],
            },
          ],
        },
      ],
    },
  ]
}
