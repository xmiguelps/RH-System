import { setupServer } from 'msw/node'
import { handlers } from './gerado/handlers'

export const servidor = setupServer(...handlers)
